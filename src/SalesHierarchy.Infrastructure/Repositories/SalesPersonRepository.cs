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
            using var cmd = new SqlCommand("SELECT ID, Name, Designation, ManagerId FROM SalesPerson ORDER BY Name", conn);
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
            using var cmd = new SqlCommand("SELECT ID, Name, Designation, ManagerId FROM SalesPerson WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public IEnumerable<SalesPerson> GetDirectReports(int managerId)
        {
            var list = new List<SalesPerson>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT ID, Name, Designation, ManagerId FROM SalesPerson WHERE ManagerId = @managerId ORDER BY Name", conn);
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

        public IReadOnlyList<int> GetPODIds(int salesPersonId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"SELECT PODId
                  FROM dbo.SalesPersonPOD
                  WHERE SalesPersonId = @sp
                  UNION
                  SELECT ID
                  FROM dbo.POD
                  WHERE salespersonId = @sp
                  ORDER BY PODId", conn);
            cmd.Parameters.AddWithValue("@sp", salesPersonId);

            var ids = new List<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
            }
            return ids;
        }

        public int Add(SalesPerson person)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO SalesPerson (Name, Designation, ManagerId) VALUES (@name, @designation, @managerId); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            AddPersonParameters(cmd, person);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public int AddWithPODs(SalesPerson person, IReadOnlyCollection<int> podIds)
        {
            var normalizedPodIds = PODAssignment.Normalize(podIds);
            if (normalizedPodIds.Any(id => id <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(podIds), "POD identifiers must be positive.");
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                ValidatePODIds(conn, tx, normalizedPodIds);

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
            using var cmd = new SqlCommand("UPDATE SalesPerson SET Name = @name, Designation = @designation, ManagerId = @managerId WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", person.ID);
            AddPersonParameters(cmd, person);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool UpdateWithPODs(SalesPerson person, IReadOnlyCollection<int> podIds)
        {
            var submitted = PODAssignment.Normalize(podIds);
            if (submitted.Any(id => id <= 0))
            {
                throw new ArgumentOutOfRangeException(nameof(podIds), "POD identifiers must be positive.");
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                ValidatePODIds(conn, tx, submitted);

                using (var cmd = new SqlCommand(
                    "UPDATE SalesPerson SET Name = @name, Designation = @designation, ManagerId = @managerId WHERE ID = @id",
                    conn,
                    tx))
                {
                    cmd.Parameters.AddWithValue("@id", person.ID);
                    AddPersonParameters(cmd, person);
                    if (cmd.ExecuteNonQuery() == 0)
                    {
                        tx.Rollback();
                        return false;
                    }
                }

                var existing = GetPODIds(conn, tx, person.ID);
                var (_, toRemove) = PODAssignment.Diff(existing, submitted);

                foreach (var podId in toRemove)
                {
                    using (var delete = new SqlCommand(
                        "DELETE FROM dbo.SalesPersonPOD WHERE SalesPersonId = @sp AND PODId = @pod",
                        conn,
                        tx))
                    {
                        delete.Parameters.AddWithValue("@sp", person.ID);
                        delete.Parameters.AddWithValue("@pod", podId);
                        delete.ExecuteNonQuery();
                    }

                    SyncLegacyOwnerAfterRemoval(conn, tx, person.ID, podId);
                }

                // Insert every submitted ID. This also promotes a verified legacy-only
                // assignment into the many-to-many table when the form is saved.
                foreach (var podId in submitted)
                {
                    InsertMappingIfMissing(conn, tx, person.ID, podId);
                    SyncLegacyOwner(conn, tx, person.ID, podId);
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
            using var cmd = new SqlCommand("DELETE FROM SalesPerson WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static IReadOnlyList<int> GetPODIds(SqlConnection conn, SqlTransaction tx, int salesPersonId)
        {
            using var cmd = new SqlCommand(
                @"SELECT PODId
                  FROM dbo.SalesPersonPOD
                  WHERE SalesPersonId = @sp
                  UNION
                  SELECT ID
                  FROM dbo.POD
                  WHERE salespersonId = @sp",
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

        private static void ValidatePODIds(
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

            cmd.CommandText = $"SELECT COUNT(1) FROM dbo.POD WHERE ID IN ({string.Join(", ", parameterNames)})";
            var found = Convert.ToInt32(cmd.ExecuteScalar());
            if (found != podIds.Count)
            {
                throw new InvalidOperationException("One or more selected PODs no longer exist.");
            }
        }

        private static void InsertMappingIfMissing(
            SqlConnection conn,
            SqlTransaction tx,
            int salesPersonId,
            int podId)
        {
            using var cmd = new SqlCommand(
                @"INSERT INTO dbo.SalesPersonPOD (SalesPersonId, PODId)
                  SELECT @sp, @pod
                  WHERE NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.SalesPersonPOD
                      WHERE SalesPersonId = @sp AND PODId = @pod
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
                @"UPDATE dbo.POD
                  SET salespersonId = @sp
                  WHERE ID = @pod
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
                @"UPDATE dbo.POD
                  SET salespersonId =
                  (
                      SELECT MIN(SalesPersonId)
                      FROM dbo.SalesPersonPOD
                      WHERE PODId = @pod
                  )
                  WHERE ID = @pod
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
                ID = (int)reader["ID"],
                Name = reader["Name"].ToString() ?? string.Empty,
                Designation = reader["Designation"] as string,
                ManagerId = reader["ManagerId"] == DBNull.Value ? null : (int)reader["ManagerId"]
            };
        }
    }
}
