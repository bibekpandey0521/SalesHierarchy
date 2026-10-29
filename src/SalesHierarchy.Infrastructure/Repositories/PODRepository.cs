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
    public class PODRepository : IPODRepository
    {
        private readonly SqlConnectionFactory _factory;

        public PODRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public IEnumerable<POD> GetAll()
        {
            var list = new List<POD>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT ID, OSSPODID, Name, PODStationId, salespersonId FROM POD ORDER BY Name", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public POD? GetById(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT ID, OSSPODID, Name, PODStationId, salespersonId FROM POD WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public IEnumerable<POD> GetBySalesPersonId(int salespersonId)
        {
            var list = new List<POD>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"SELECT ID, OSSPODID, Name, PODStationId, salespersonId
                  FROM POD
                  WHERE ID IN
                  (
                      SELECT PODId FROM dbo.SalesPersonPOD WHERE SalesPersonId = @sp
                      UNION
                      SELECT ID FROM dbo.POD WHERE salespersonId = @sp
                  )
                  ORDER BY Name", conn);
            cmd.Parameters.AddWithValue("@sp", salespersonId);
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

            cmd.CommandText = $"SELECT ID FROM dbo.POD WHERE ID IN ({string.Join(", ", parameterNames)})";
            var found = new List<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                found.Add(reader.GetInt32(0));
            }
            return found;
        }

        public bool HasPodsForStation(int podStationId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM POD WHERE PODStationId = @id", conn);
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
                      SELECT COUNT(1) FROM dbo.SalesPersonPOD WHERE SalesPersonId = @id
                  ) +
                  (
                      SELECT COUNT(1) FROM dbo.POD WHERE salespersonId = @id
                  )", conn);
            cmd.Parameters.AddWithValue("@id", salespersonId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool HasPodAssignments(SqlConnection conn, int podId)
        {
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.SalesPersonPOD WHERE PODId = @pod", conn);
            cmd.Parameters.AddWithValue("@pod", podId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public int Add(POD pod)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO POD (OSSPODID, Name, PODStationId, salespersonId) VALUES (@oss, @name, @station, @sp); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@oss", (object?)pod.OSSPODID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name", pod.Name);
            cmd.Parameters.AddWithValue("@station", pod.PODStationId);
            cmd.Parameters.AddWithValue("@sp", (object?)pod.salespersonId ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(POD pod)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE POD SET OSSPODID = @oss, Name = @name, PODStationId = @station, salespersonId = @sp WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", pod.ID);
            cmd.Parameters.AddWithValue("@oss", (object?)pod.OSSPODID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name", pod.Name);
            cmd.Parameters.AddWithValue("@station", pod.PODStationId);
            cmd.Parameters.AddWithValue("@sp", (object?)pod.salespersonId ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            if (HasPodAssignments(conn, id))
            {
                throw new InvalidOperationException($"Cannot delete POD ID {id}: it is assigned to one or more salespeople via the many-to-many table. Reassign or remove those assignments first.");
            }
            using var cmd = new SqlCommand("DELETE FROM POD WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static POD Map(SqlDataReader reader)
        {
            return new POD
            {
                ID = (int)reader["ID"],
                OSSPODID = reader["OSSPODID"] as string,
                Name = reader["Name"].ToString() ?? string.Empty,
                PODStationId = (int)reader["PODStationId"],
                salespersonId = reader["salespersonId"] == DBNull.Value ? null : (int)reader["salespersonId"]
            };
        }
    }
}
