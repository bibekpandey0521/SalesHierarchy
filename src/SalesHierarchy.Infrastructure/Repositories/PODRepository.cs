using System;
using System.Collections.Generic;
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
            using var cmd = new SqlCommand("SELECT ID, OSSPODID, Name, PODStationId, salespersonId FROM POD WHERE salespersonId = @sp ORDER BY Name", conn);
            cmd.Parameters.AddWithValue("@sp", salespersonId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
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
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM POD WHERE salespersonId = @id", conn);
            cmd.Parameters.AddWithValue("@id", salespersonId);
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
