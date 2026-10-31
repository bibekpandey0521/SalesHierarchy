using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Infrastructure.Data;

namespace SalesHierarchy.Infrastructure.Repositories
{
    public class PodStationRepository : IPodStationRepository
    {
        private readonly SqlConnectionFactory _factory;

        public PodStationRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public IEnumerable<PodStation> GetAll()
        {
            var list = new List<PodStation>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT Id, OSSPODStationId, Name FROM PodStation ORDER BY Name", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }
            return list;
        }

        public PodStation? GetById(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("SELECT Id, OSSPODStationId, Name FROM PodStation WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public bool ExistsByOssId(int ossId, int? excludeId = null)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM PodStation WHERE OSSPODStationId = @oss AND (@excludeId IS NULL OR Id <> @excludeId)", conn);
            cmd.Parameters.AddWithValue("@oss", ossId);
            cmd.Parameters.AddWithValue("@excludeId", (object?)excludeId ?? System.DBNull.Value);
            return System.Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public int Add(PodStation station)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("INSERT INTO PodStation (OSSPODStationId, Name) VALUES (@osspod, @name); SELECT CAST(SCOPE_IDENTITY() AS INT);", conn);
            cmd.Parameters.AddWithValue("@osspod", station.OSSPODStationId);
            cmd.Parameters.AddWithValue("@name", station.Name);
            return System.Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool Update(PodStation station)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("UPDATE PodStation SET OSSPODStationId = @osspod, Name = @name WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", station.Id);
            cmd.Parameters.AddWithValue("@osspod", station.OSSPODStationId);
            cmd.Parameters.AddWithValue("@name", station.Name);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool Delete(int id)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM PodStation WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static PodStation Map(SqlDataReader reader)
        {
            return new PodStation
            {
                Id = (int)reader["Id"],
                OSSPODStationId = (int)reader["OSSPODStationId"],
                Name = reader["Name"].ToString() ?? string.Empty
            };
        }
    }
}
