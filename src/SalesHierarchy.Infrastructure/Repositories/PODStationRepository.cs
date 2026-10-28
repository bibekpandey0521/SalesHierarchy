using System.Collections.Generic;
using System.Linq;
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
            using var cmd = new SqlCommand("SELECT ID, OSSPODStationId, Name FROM PODStation ORDER BY Name", conn);
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

        public bool ExistsByName(string name, int? excludeId = null)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM PODStation WHERE Name = @name AND (@excludeId IS NULL OR ID <> @excludeId)", conn);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@excludeId", (object?)excludeId ?? System.DBNull.Value);
            return System.Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public int Add(PODStation station)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO PODStation (OSSPODStationId, Name) VALUES (@osspod, @name); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@osspod", (object?)station.OSSPODStationId ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("@name", station.Name);
            return System.Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(PODStation station)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE PODStation SET OSSPODStationId = @osspod, Name = @name WHERE ID = @id", conn);
            cmd.Parameters.AddWithValue("@id", station.ID);
            cmd.Parameters.AddWithValue("@osspod", (object?)station.OSSPODStationId ?? System.DBNull.Value);
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
