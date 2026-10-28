using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Infrastructure.Data;

namespace SalesHierarchy.Infrastructure.Repositories
{
    public class PODStationRepository : IPODStationRepository
    {
        private readonly SqlConnectionFactory _factory;
        public PODStationRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public IEnumerable<PODStation> GetAll()
        {
            var list = new List<PODStation>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT ID, OSSPODStationId, Name FROM PODStation", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new PODStation
                {
                    ID = (int)reader["ID"],
                    OSSPODStationId = reader["OSSPODStationId"] as string,
                    Name = reader["Name"].ToString() ?? string.Empty
                });
            }
            return list;
        }

        public PODStation? GetById(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT ID, OSSPODStationId, Name FROM PODStation WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new PODStation
                {
                    ID = (int)reader["ID"],
                    OSSPODStationId = reader["OSSPODStationId"] as string,
                    Name = reader["Name"].ToString() ?? string.Empty
                };
            }
            return null;
        }

        public int Add(PODStation station)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO PODStation (OSSPODStationId, Name) VALUES (@osspod, @name); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@osspod", (object?)station.OSSPODStationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name", station.Name);
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        public bool Update(PODStation station)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE PODStation SET OSSPODStationId = @osspod, Name = @name WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", station.ID);
            cmd.Parameters.AddWithValue("@osspod", (object?)station.OSSPODStationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name", station.Name);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM PODStation WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
