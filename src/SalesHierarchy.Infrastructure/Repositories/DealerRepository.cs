using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Infrastructure.Data;

namespace SalesHierarchy.Infrastructure.Repositories
{
    public class DealerRepository : IDealerRepository
    {
        private readonly SqlConnectionFactory _factory;

        public DealerRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public IEnumerable<Dealer> GetAll()
        {
            var list = new List<Dealer>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT UniqueID, Name, Code FROM Dealer ORDER BY Name", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public Dealer? GetById(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT UniqueID, Name, Code FROM Dealer WHERE UniqueID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public bool ExistsByCode(string code, int? excludeId = null)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Dealer WHERE Code = @code AND (@excludeId IS NULL OR UniqueID <> @excludeId)", conn);
            cmd.Parameters.AddWithValue("@code", code);
            cmd.Parameters.AddWithValue("@excludeId", (object?)excludeId ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public bool HasMappings(int dealerId)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT COUNT(1) FROM salesdealermapping WHERE DealerId = @id", conn);
            cmd.Parameters.AddWithValue("@id", dealerId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public int Add(Dealer dealer)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO Dealer (Name, Code) VALUES (@name, @code); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@name", dealer.Name);
            cmd.Parameters.AddWithValue("@code", (object?)dealer.Code ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(Dealer dealer)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE Dealer SET Name = @name, Code = @code WHERE UniqueID = @id", conn);
            cmd.Parameters.AddWithValue("@id", dealer.UniqueID);
            cmd.Parameters.AddWithValue("@name", dealer.Name);
            cmd.Parameters.AddWithValue("@code", (object?)dealer.Code ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM Dealer WHERE UniqueID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static Dealer Map(SqlDataReader reader)
        {
            return new Dealer
            {
                UniqueID = (int)reader["UniqueID"],
                Name = reader["Name"].ToString() ?? string.Empty,
                Code = reader["Code"] as string
            };
        }
    }
}
