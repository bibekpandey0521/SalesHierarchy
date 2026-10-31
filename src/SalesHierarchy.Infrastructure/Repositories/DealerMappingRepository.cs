using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Infrastructure.Data;

namespace SalesHierarchy.Infrastructure.Repositories
{
    public class DealerMappingRepository : IDealerMappingRepository
    {
        private readonly SqlConnectionFactory _factory;

        public DealerMappingRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public IEnumerable<DealerMapping> GetAll()
        {
            var list = new List<DealerMapping>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT UniqueID, SalesPersonId, DealerId FROM salesdealermapping ORDER BY UniqueID", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public IEnumerable<DealerMapping> GetBySalesPersonId(int salespersonId)
        {
            var list = new List<DealerMapping>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT UniqueID, SalesPersonId, DealerId FROM salesdealermapping WHERE SalesPersonId = @sp", conn);
            cmd.Parameters.AddWithValue("@sp", salespersonId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public DealerMapping? GetById(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT UniqueID, SalesPersonId, DealerId FROM salesdealermapping WHERE UniqueID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public bool Exists(int salespersonId, int dealerId, int? excludeId = null)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM salesdealermapping WHERE SalesPersonId = @sp AND DealerId = @d AND (@excludeId IS NULL OR UniqueID <> @excludeId)", conn);
            cmd.Parameters.AddWithValue("@sp", salespersonId);
            cmd.Parameters.AddWithValue("@d", dealerId);
            cmd.Parameters.AddWithValue("@excludeId", (object?)excludeId ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool HasMappingsForSalesPerson(int salespersonId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM salesdealermapping WHERE SalesPersonId = @id", conn);
            cmd.Parameters.AddWithValue("@id", salespersonId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public int Add(DealerMapping mapping)
        {
            if (Exists(mapping.SalesPersonId, mapping.DealerId))
            {
                return -1;
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO salesdealermapping (SalesPersonId, DealerId) VALUES (@sp, @d); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@sp", mapping.SalesPersonId);
            cmd.Parameters.AddWithValue("@d", mapping.DealerId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(DealerMapping mapping)
        {
            if (Exists(mapping.SalesPersonId, mapping.DealerId, mapping.UniqueID))
            {
                return false;
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE salesdealermapping SET SalesPersonId = @sp, DealerId = @d WHERE UniqueID = @id", conn);
            cmd.Parameters.AddWithValue("@id", mapping.UniqueID);
            cmd.Parameters.AddWithValue("@sp", mapping.SalesPersonId);
            cmd.Parameters.AddWithValue("@d", mapping.DealerId);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM salesdealermapping WHERE UniqueID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static DealerMapping Map(SqlDataReader reader)
        {
            return new DealerMapping
            {
                UniqueID = (int)reader["UniqueID"],
                SalesPersonId = (int)reader["SalesPersonId"],
                DealerId = (int)reader["DealerId"]
            };
        }
    }
}
