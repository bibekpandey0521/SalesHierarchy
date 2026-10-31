using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Infrastructure.Data;

namespace SalesHierarchy.Infrastructure.Repositories
{
    public class PodRepository : IPodRepository
    {
        private readonly SqlConnectionFactory _factory;

        public PodRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public IEnumerable<Pod> GetAll()
        {
            var list = new List<Pod>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT Id, OSSPODID, Name, PODStationId, salespersonId FROM Pod ORDER BY Name", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public Pod? GetById(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT Id, OSSPODID, Name, PODStationId, salespersonId FROM Pod WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public IEnumerable<Pod> GetBySalesPersonId(int salespersonId)
        {
            var list = new List<Pod>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"SELECT p.Id, p.OSSPODID, p.Name, p.PODStationId, p.salespersonId
                  FROM Pod AS p
                  INNER JOIN dbo.SalesPersonPod AS sp ON sp.PodId = p.Id
                  WHERE sp.SalesPersonId = @sp
                  ORDER BY p.Name", conn);
            cmd.Parameters.AddWithValue("@sp", salespersonId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public IEnumerable<Pod> GetByStationId(int stationId)
        {
            var list = new List<Pod>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT Id, OSSPODID, Name, PODStationId, salespersonId FROM Pod WHERE PODStationId = @station ORDER BY Name", conn);
            cmd.Parameters.AddWithValue("@station", stationId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public IReadOnlyCollection<int> GetExistingIds(IEnumerable<int> podIds)
        {
            var ids = podIds.Distinct().Where(id => id > 0).ToList();
            if (ids.Count == 0)
            {
                return Array.Empty<int>();
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand { Connection = conn };
            var parameterNames = new List<string>();
            var index = 0;
            foreach (var id in ids)
            {
                var name = "@pod" + index++;
                parameterNames.Add(name);
                cmd.Parameters.Add(name, SqlDbType.Int).Value = id;
            }

            cmd.CommandText = $"SELECT Id FROM dbo.Pod WHERE Id IN ({string.Join(", ", parameterNames)})";
            var found = new List<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                found.Add(reader.GetInt32(0));
            }
            return found;
        }

        public bool ExistsByOssId(int ossId, int? excludeId = null)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Pod WHERE OSSPODID = @oss AND (@excludeId IS NULL OR Id <> @excludeId)", conn);
            cmd.Parameters.AddWithValue("@oss", ossId);
            cmd.Parameters.AddWithValue("@excludeId", (object?)excludeId ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool HasPodsForStation(int podStationId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM Pod WHERE PODStationId = @id", conn);
            cmd.Parameters.AddWithValue("@id", podStationId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool HasPodsForSalesPerson(int salespersonId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"SELECT
                  (
                      SELECT COUNT(1) FROM dbo.SalesPersonPod WHERE SalesPersonId = @id
                  ) +
                  (
                      SELECT COUNT(1) FROM dbo.Pod WHERE salespersonId = @id
                  )", conn);
            cmd.Parameters.AddWithValue("@id", salespersonId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool HasPodAssignments(SqlConnection conn, int podId)
        {
            using var cmd = new SqlCommand(
                @"SELECT
                  (
                      SELECT COUNT(1) FROM dbo.SalesPersonPod WHERE PodId = @pod
                  ) +
                  (
                      SELECT COUNT(1) FROM dbo.Pod WHERE Id = @pod AND salespersonId IS NOT NULL
                  )", conn);
            cmd.Parameters.AddWithValue("@pod", podId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public int Add(Pod pod)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "INSERT INTO Pod (OSSPODID, Name, PODStationId, salespersonId) VALUES (@oss, @name, NULL, NULL); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                conn);
            cmd.Parameters.AddWithValue("@oss", pod.OSSPODID);
            cmd.Parameters.AddWithValue("@name", pod.Name);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(Pod pod)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            // Station and ownership are deliberately excluded: they are managed by the
            // assignment page and SalesPersonPod respectively.
            using var cmd = new SqlCommand("UPDATE Pod SET OSSPODID = @oss, Name = @name WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", pod.Id);
            cmd.Parameters.AddWithValue("@oss", pod.OSSPODID);
            cmd.Parameters.AddWithValue("@name", pod.Name);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            if (HasPodAssignments(conn, id))
            {
                throw new InvalidOperationException(
                    $"Cannot delete Pod Id {id}: it is assigned to a salesperson. Remove the ownership assignment first.");
            }
            using var cmd = new SqlCommand("DELETE FROM Pod WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        public void AssignPodsToStation(int stationId, IReadOnlyCollection<int> podIds)
        {
            var selected = (podIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                using (var stationCheck = new SqlCommand(
                    "SELECT COUNT(1) FROM dbo.PodStation WHERE Id = @station", conn, tx))
                {
                    stationCheck.Parameters.AddWithValue("@station", stationId);
                    if (Convert.ToInt32(stationCheck.ExecuteScalar()) == 0)
                    {
                        throw new InvalidOperationException("The selected Pod station no longer exists.");
                    }
                }

                ValidatePodIds(conn, tx, selected);

                // Remove Pods that currently belong to this station but are no longer selected.
                var unassignSql = "UPDATE dbo.Pod SET PODStationId = NULL WHERE PODStationId = @station";
                using (var unassign = new SqlCommand(unassignSql, conn, tx))
                {
                    unassign.Parameters.AddWithValue("@station", stationId);
                    if (selected.Count > 0)
                    {
                        unassign.CommandText += $" AND Id NOT IN ({AddIntParameters(unassign, selected, "@selected")})";
                    }
                    unassign.ExecuteNonQuery();
                }

                if (selected.Count > 0)
                {
                    using var assign = new SqlCommand { Connection = conn, Transaction = tx };
                    var selectedList = AddIntParameters(assign, selected, "@selected");
                    assign.CommandText = $"UPDATE dbo.Pod SET PODStationId = @station WHERE Id IN ({selectedList})";
                    assign.Parameters.AddWithValue("@station", stationId);
                    assign.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void ClearStationAssignments(int stationId)
        {
            AssignPodsToStation(stationId, Array.Empty<int>());
        }

        private static void ValidatePodIds(SqlConnection conn, SqlTransaction tx, IReadOnlyList<int> podIds)
        {
            if (podIds.Count == 0)
            {
                return;
            }

            using var cmd = new SqlCommand { Connection = conn, Transaction = tx };
            var names = AddIntParameters(cmd, podIds, "@check");
            cmd.CommandText = $"SELECT COUNT(1) FROM dbo.Pod WHERE Id IN ({names})";

            if (Convert.ToInt32(cmd.ExecuteScalar()) != podIds.Count)
            {
                throw new InvalidOperationException("One or more selected Pods no longer exist.");
            }
        }

        private static string AddIntParameters(SqlCommand cmd, IReadOnlyList<int> ids, string prefix)
        {
            var names = new List<string>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                var name = $"{prefix}{i}";
                names.Add(name);
                cmd.Parameters.Add(name, SqlDbType.Int).Value = ids[i];
            }
            return string.Join(", ", names);
        }

        private static Pod Map(SqlDataReader reader)
        {
            return new Pod
            {
                Id = (int)reader["Id"],
                OSSPODID = (int)reader["OSSPODID"],
                Name = reader["Name"].ToString() ?? string.Empty,
                PODStationId = reader["PODStationId"] == DBNull.Value ? null : (int)reader["PODStationId"],
                salespersonId = reader["salespersonId"] == DBNull.Value ? null : (int)reader["salespersonId"]
            };
        }
    }
}
