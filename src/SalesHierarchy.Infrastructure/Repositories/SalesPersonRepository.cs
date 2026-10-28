using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
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
            using var cmd = new SqlCommand("SELECT ID, Name, Designation, ManagerId FROM SalesPerson", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SalesPerson
                {
                    ID = (int)reader["ID"],
                    Name = reader["Name"].ToString() ?? string.Empty,
                    Designation = reader["Designation"] as string,
                    ManagerId = reader["ManagerId"] == DBNull.Value ? null : (int)reader["ManagerId"]
                });
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
            if (reader.Read())
            {
                return new SalesPerson
                {
                    ID = (int)reader["ID"],
                    Name = reader["Name"].ToString() ?? string.Empty,
                    Designation = reader["Designation"] as string,
                    ManagerId = reader["ManagerId"] == DBNull.Value ? null : (int)reader["ManagerId"]
                };
            }
            return null;
        }

        public int Add(SalesPerson person)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO SalesPerson (Name, Designation, ManagerId) VALUES (@name, @designation, @managerId); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@name", person.Name);
            cmd.Parameters.AddWithValue("@designation", (object?)person.Designation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@managerId", (object?)person.ManagerId ?? DBNull.Value);
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        public bool Update(SalesPerson person)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE SalesPerson SET Name = @name, Designation = @designation, ManagerId = @managerId WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", person.ID);
            cmd.Parameters.AddWithValue("@name", person.Name);
            cmd.Parameters.AddWithValue("@designation", (object?)person.Designation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@managerId", (object?)person.ManagerId ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM SalesPerson WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        public IEnumerable<SalesPerson> GetDirectReports(int managerId)
        {
            var list = new List<SalesPerson>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT ID, Name, Designation, ManagerId FROM SalesPerson WHERE ManagerId = @managerId", conn);
            cmd.Parameters.AddWithValue("@managerId", managerId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SalesPerson
                {
                    ID = (int)reader["ID"],
                    Name = reader["Name"].ToString() ?? string.Empty,
                    Designation = reader["Designation"] as string,
                    ManagerId = managerId
                });
            }
            return list;
        }
    }
}
