using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Infrastructure.Data;

namespace SalesHierarchy.Infrastructure.Repositories
{
    public class SalesDealerMappingRepository : ISalesDealerMappingRepository
    {
        private readonly SqlConnectionFactory _factory;

        public SalesDealerMappingRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public IEnumerable<SalesDealerMapping> GetAll()
        {
            var list = new List<SalesDealerMapping>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT UniqueID, salespersonid, dealerid FROM salesdealermapping ORDER BY UniqueID", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public IEnumerable<SalesDealerMapping> GetBySalesPersonId(int salespersonId)
        {
            var list = new List<SalesDealerMapping>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT UniqueID, salespersonid, dealerid FROM salesdealermapping WHERE salespersonid = @sp", conn);
            cmd.Parameters.AddWithValue("@sp", salespersonId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public SalesDealerMapping? GetById(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT UniqueID, salespersonid, dealerid FROM salesdealermapping WHERE UniqueID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public bool Exists(int salespersonId, int dealerId, int? excludeId = null)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM salesdealermapping WHERE salespersonid = @sp AND dealerid = @d AND (@excludeId IS NULL OR UniqueID <> @excludeId)", conn);
            cmd.Parameters.AddWithValue("@sp", salespersonId);
            cmd.Parameters.AddWithValue("@d", dealerId);
            cmd.Parameters.AddWithValue("@excludeId", (object?)excludeId ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool HasMappingsForSalesPerson(int salespersonId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM salesdealermapping WHERE salespersonid = @id", conn);
            cmd.Parameters.AddWithValue("@id", salespersonId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public int Add(SalesDealerMapping mapping)
        {
            if (Exists(mapping.salespersonid, mapping.dealerid))
            {
                return -1;
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO salesdealermapping (salespersonid, dealerid) VALUES (@sp, @d); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@sp", mapping.salespersonid);
            cmd.Parameters.AddWithValue("@d", mapping.dealerid);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(SalesDealerMapping mapping)
        {
            if (Exists(mapping.salespersonid, mapping.dealerid, mapping.UniqueID))
            {
                return false;
            }

            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE salesdealermapping SET salespersonid = @sp, dealerid = @d WHERE UniqueID = @id", conn);
            cmd.Parameters.AddWithValue("@id", mapping.UniqueID);
            cmd.Parameters.AddWithValue("@sp", mapping.salespersonid);
            cmd.Parameters.AddWithValue("@d", mapping.dealerid);
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

        private static SalesDealerMapping Map(SqlDataReader reader)
        {
            return new SalesDealerMapping
            {
                UniqueID = (int)reader["UniqueID"],
                salespersonid = (int)reader["salespersonid"],
                dealerid = (int)reader["dealerid"]
            };
        }
    }
}
