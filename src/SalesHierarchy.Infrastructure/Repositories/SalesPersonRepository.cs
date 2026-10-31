using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Core.Validation;
using SalesHierarchy.Infrastructure.Data;

namespace SalesHierarchy.Infrastructure.Repositories
{
    public class SalesPersonRepository : ISalesPersonRepository
    {
        private readonly SqlConnectionFactory _factory;

        public SalesPersonRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public IEnumerable<SalesPerson> GetAll()
        {
            var list = new List<SalesPerson>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT Id, Name, Designation, ManagerId FROM SalesPerson ORDER BY Name", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public SalesPerson? GetById(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT Id, Name, Designation, ManagerId FROM SalesPerson WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public IEnumerable<SalesPerson> GetDirectReports(int managerId)
        {
            var list = new List<SalesPerson>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT Id, Name, Designation, ManagerId FROM SalesPerson WHERE ManagerId = @managerId ORDER BY Name", conn);
            cmd.Parameters.AddWithValue("@managerId", managerId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public bool HasSubordinates(int salesPersonId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM SalesPerson WHERE ManagerId = @id", conn);
            cmd.Parameters.AddWithValue("@id", salesPersonId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public IReadOnlyList<int> GetPodIds(int salesPersonId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"SELECT PodId
                  FROM dbo.SalesPersonPod
                  WHERE SalesPersonId = @sp
                  ORDER BY PodId", conn);
            cmd.Parameters.AddWithValue("@sp", salesPersonId);

            var ids = new List<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
            }
            return ids;
        }

        public IReadOnlyList<SalesPersonPod> GetAllPodAssignments()
        {
            var list = new List<SalesPersonPod>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT SalesPersonId, PodId FROM dbo.SalesPersonPod ORDER BY PodId, SalesPersonId", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SalesPersonPod
                {
                    SalesPersonId = reader.GetInt32(0),
                    PodId = reader.GetInt32(1)
                });
            }
            return list;
        }

        public bool AssignPod(int salesPersonId, int podId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                using (var check = new SqlCommand(
                    @"SELECT
                      (SELECT COUNT(1) FROM dbo.SalesPerson WHERE Id = @sp) +
                      (SELECT COUNT(1) FROM dbo.Pod WHERE Id = @pod)",
                    conn,
                    tx))
                {
                    check.Parameters.AddWithValue("@sp", salesPersonId);
                    check.Parameters.AddWithValue("@pod", podId);
                    if (Convert.ToInt32(check.ExecuteScalar()) != 2)
                    {
                        throw new InvalidOperationException("The selected salesperson or Pod no longer exists.");
                    }
                }

                InsertMappingIfMissing(conn, tx, salesPersonId, podId);
                SyncLegacyOwner(conn, tx, salesPersonId, podId);
                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public bool RemovePod(int salesPersonId, int podId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                int removed;
                using (var delete = new SqlCommand(
                    "DELETE FROM dbo.SalesPersonPod WHERE SalesPersonId = @sp AND PodId = @pod",
                    conn,
                    tx))
                {
                    delete.Parameters.AddWithValue("@sp", salesPersonId);
                    delete.Parameters.AddWithValue("@pod", podId);
                    removed = delete.ExecuteNonQuery();
                }

                SyncLegacyOwnerAfterRemoval(conn, tx, salesPersonId, podId);
                tx.Commit();
                return removed > 0;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public bool ReplacePodOwners(int podId, IReadOnlyCollection<int> salesPersonIds)
        {
            var owners = PodAssignment.Normalize(salesPersonIds);
            if (owners.Any(id => id <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(salesPersonIds), "Salesperson identifiers must be positive.");
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                using (var podCheck = new SqlCommand("SELECT COUNT(1) FROM dbo.Pod WHERE Id = @pod", conn, tx))
                {
                    podCheck.Parameters.AddWithValue("@pod", podId);
                    if (Convert.ToInt32(podCheck.ExecuteScalar()) == 0)
                    {
                        throw new InvalidOperationException("The selected Pod no longer exists.");
                    }
                }

                if (owners.Count > 0)
                {
                    var names = new List<string>();
                    using var check = new SqlCommand { Connection = conn, Transaction = tx };
                    for (var i = 0; i < owners.Count; i++)
                    {
                        var name = "@owner" + i;
                        names.Add(name);
                        check.Parameters.Add(name, SqlDbType.Int).Value = owners[i];
                    }
                    check.CommandText = $"SELECT COUNT(1) FROM dbo.SalesPerson WHERE Id IN ({string.Join(", ", names)})";
                    if (Convert.ToInt32(check.ExecuteScalar()) != owners.Count)
                    {
                        throw new InvalidOperationException("One or more selected salespeople no longer exist.");
                    }
                }

                using (var delete = new SqlCommand("DELETE FROM dbo.SalesPersonPod WHERE PodId = @pod", conn, tx))
                {
                    delete.Parameters.AddWithValue("@pod", podId);
                    delete.ExecuteNonQuery();
                }

                foreach (var ownerId in owners)
                {
                    InsertMappingIfMissing(conn, tx, ownerId, podId);
                }

                using (var sync = new SqlCommand(
                    "UPDATE dbo.Pod SET salespersonId = @owner WHERE Id = @pod",
                    conn,
                    tx))
                {
                    sync.Parameters.AddWithValue("@owner", (object?)owners.FirstOrDefault() ?? DBNull.Value);
                    sync.Parameters.AddWithValue("@pod", podId);
                    sync.ExecuteNonQuery();
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public int Add(SalesPerson person)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO SalesPerson (Name, Designation, ManagerId) VALUES (@name, @designation, @managerId); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            AddPersonParameters(cmd, person);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public int AddWithPods(SalesPerson person, IReadOnlyCollection<int> podIds)
        {
            var normalizedPodIds = PodAssignment.Normalize(podIds);
            if (normalizedPodIds.Any(id => id <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(podIds), "Pod identifiers must be positive.");
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                ValidatePodIds(conn, tx, normalizedPodIds);

                int personId;
                using (var cmd = new SqlCommand(
                    "INSERT INTO SalesPerson (Name, Designation, ManagerId) VALUES (@name, @designation, @managerId); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    conn,
                    tx))
                {
                    AddPersonParameters(cmd, person);
                    personId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                foreach (var podId in normalizedPodIds)
                {
                    InsertMappingIfMissing(conn, tx, personId, podId);
                    SyncLegacyOwner(conn, tx, personId, podId);
                }

                tx.Commit();
                return personId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public bool Update(SalesPerson person)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE SalesPerson SET Name = @name, Designation = @designation, ManagerId = @managerId WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", person.Id);
            AddPersonParameters(cmd, person);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool UpdateWithPods(SalesPerson person, IReadOnlyCollection<int> podIds)
        {
            var submitted = PodAssignment.Normalize(podIds);
            if (submitted.Any(id => id <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(podIds), "Pod identifiers must be positive.");
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                ValidatePodIds(conn, tx, submitted);

                using (var cmd = new SqlCommand(
                    "UPDATE SalesPerson SET Name = @name, Designation = @designation, ManagerId = @managerId WHERE Id = @id",
                    conn,
                    tx))
                {
                    cmd.Parameters.AddWithValue("@id", person.Id);
                    AddPersonParameters(cmd, person);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        tx.Rollback();
                        return false;
                    }
                }

                var existing = GetPodIds(conn, tx, person.Id);
                var (_, toRemove) = PodAssignment.Diff(existing, submitted);

                foreach (var podId in toRemove)
                {
                    using (var delete = new SqlCommand(
                        "DELETE FROM dbo.SalesPersonPod WHERE SalesPersonId = @sp AND PodId = @pod",
                        conn,
                        tx))
                    {
                        delete.Parameters.AddWithValue("@sp", person.Id);
                        delete.Parameters.AddWithValue("@pod", podId);
                        delete.ExecuteNonQuery();
                    }

                    SyncLegacyOwnerAfterRemoval(conn, tx, person.Id, podId);
                }

                // Insert every submitted Id. SalesPersonPod is the source of truth;
                // the legacy owner column is synchronised where it is safe to do so.
                foreach (var podId in submitted)
                {
                    InsertMappingIfMissing(conn, tx, person.Id, podId);
                    SyncLegacyOwner(conn, tx, person.Id, podId);
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public bool Delete(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM SalesPerson WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static IReadOnlyList<int> GetPodIds(SqlConnection conn, SqlTransaction tx, int salesPersonId)
        {
            using var cmd = new SqlCommand(
                @"SELECT PodId
                  FROM dbo.SalesPersonPod
                  WHERE SalesPersonId = @sp",
                conn,
                tx);
            cmd.Parameters.AddWithValue("@sp", salesPersonId);

            var ids = new List<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
            }
            return ids;
        }

        private static void ValidatePodIds(
            SqlConnection conn,
            SqlTransaction tx,
            IReadOnlyCollection<int> podIds)
        {
            if (podIds.Count == 0)
            {
                return;
            }

            var parameterNames = new List<string>();
            using var cmd = new SqlCommand { Connection = conn, Transaction = tx };
            var index = 0;
            foreach (var podId in podIds)
            {
                var name = "@pod" + index++;
                parameterNames.Add(name);
                cmd.Parameters.Add(name, SqlDbType.Int).Value = podId;
            }

            cmd.CommandText = $"SELECT COUNT(1) FROM dbo.Pod WHERE Id IN ({string.Join(", ", parameterNames)})";
            var found = Convert.ToInt32(cmd.ExecuteScalar());
            if (found != podIds.Count)
            {
                throw new InvalidOperationException("One or more selected Pods no longer exist.");
            }
        }

        private static void InsertMappingIfMissing(
            SqlConnection conn,
            SqlTransaction tx,
            int salesPersonId,
            int podId)
        {
            using var cmd = new SqlCommand(
                @"INSERT INTO dbo.SalesPersonPod (SalesPersonId, PodId)
                  SELECT @sp, @pod
                  WHERE NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.SalesPersonPod
                      WHERE SalesPersonId = @sp AND PodId = @pod
                  )",
                conn,
                tx);
            cmd.Parameters.AddWithValue("@sp", salesPersonId);
            cmd.Parameters.AddWithValue("@pod", podId);
            cmd.ExecuteNonQuery();
        }

        private static void SyncLegacyOwner(
            SqlConnection conn,
            SqlTransaction tx,
            int salesPersonId,
            int podId)
        {
            // Keep the legacy single-owner column useful for older screens, but never
            // overwrite a different salesperson's existing assignment.
            using var cmd = new SqlCommand(
                @"UPDATE dbo.Pod
                  SET salespersonId = @sp
                  WHERE Id = @pod
                    AND (salespersonId IS NULL OR salespersonId = @sp)",
                conn,
                tx);
            cmd.Parameters.AddWithValue("@sp", salesPersonId);
            cmd.Parameters.AddWithValue("@pod", podId);
            cmd.ExecuteNonQuery();
        }

        private static void SyncLegacyOwnerAfterRemoval(
            SqlConnection conn,
            SqlTransaction tx,
            int salesPersonId,
            int podId)
        {
            // If this salesperson was the legacy owner, point the legacy column at the
            // remaining mapping (if any) or clear it. Explicitly sync the many-to-many
            // table as the source of truth.
            using var cmd = new SqlCommand(
                @"UPDATE dbo.Pod
                  SET salespersonId =
                  (
                      SELECT MIN(SalesPersonId)
                      FROM dbo.SalesPersonPod
                      WHERE PodId = @pod
                  )
                  WHERE Id = @pod
                    AND (salespersonId = @sp OR salespersonId IS NULL)",
                conn,
                tx);
            cmd.Parameters.AddWithValue("@sp", salesPersonId);
            cmd.Parameters.AddWithValue("@pod", podId);
            cmd.ExecuteNonQuery();
        }

        private static void AddPersonParameters(SqlCommand cmd, SalesPerson person)
        {
            cmd.Parameters.AddWithValue("@name", person.Name);
            cmd.Parameters.AddWithValue("@designation", (object?)person.Designation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@managerId", (object?)person.ManagerId ?? DBNull.Value);
        }

        private static SalesPerson Map(SqlDataReader reader)
        {
            return new SalesPerson
            {
                Id = (int)reader["Id"],
                Name = reader["Name"].ToString() ?? string.Empty,
                Designation = reader["Designation"] as string,
                ManagerId = reader["ManagerId"] == DBNull.Value ? null : (int)reader["ManagerId"]
            };
        }
    }
}
