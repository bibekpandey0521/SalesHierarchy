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
            using var cmd = new SqlCommand("SELECT UniqueID, Name, Code FROM Dealer", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Dealer
                {
                    UniqueID = (int)reader["UniqueID"],
                    Name = reader["Name"].ToString() ?? string.Empty,
                    Code = reader["Code"] as string
                });
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
            if (reader.Read())
            {
                return new Dealer
                {
                    UniqueID = (int)reader["UniqueID"],
                    Name = reader["Name"].ToString() ?? string.Empty,
                    Code = reader["Code"] as string
                };
            }
            return null;
        }

        public int Add(Dealer dealer)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO Dealer (Name, Code) VALUES (@name, @code); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@name", dealer.Name);
            cmd.Parameters.AddWithValue("@code", (object?)dealer.Code ?? DBNull.Value);
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
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
    }
}
