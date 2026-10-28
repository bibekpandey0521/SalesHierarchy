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
            using var cmd = new SqlCommand("SELECT ID, OSSPODID, Name, PODStationId, salespersonId FROM POD", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new POD
                {
                    ID = (int)reader["ID"],
                    OSSPODID = reader["OSSPODID"] as string,
                    Name = reader["Name"].ToString() ?? string.Empty,
                    PODStationId = (int)reader["PODStationId"],
                    salespersonId = reader["salespersonId"] == DBNull.Value ? null : (int)reader["salespersonId"]
                });
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
            if (reader.Read())
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
            return null;
        }

        public IEnumerable<POD> GetBySalesPersonId(int salespersonId)
        {
            var list = new List<POD>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT ID, OSSPODID, Name, PODStationId, salespersonId FROM POD WHERE salespersonId = @sp", conn);
            cmd.Parameters.AddWithValue("@sp", salespersonId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new POD
                {
                    ID = (int)reader["ID"],
                    OSSPODID = reader["OSSPODID"] as string,
                    Name = reader["Name"].ToString() ?? string.Empty,
                    PODStationId = (int)reader["PODStationId"],
                    salespersonId = salespersonId
                });
            }
            return list;
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
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
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
    }
}
