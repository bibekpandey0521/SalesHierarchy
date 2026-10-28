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

        public int Add(SalesPerson person)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO SalesPerson (Name, Designation, ManagerId) VALUES (@name, @designation, @managerId); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@name", person.Name);
            cmd.Parameters.AddWithValue("@designation", (object?)person.Designation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@managerId", (object?)person.ManagerId ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
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
