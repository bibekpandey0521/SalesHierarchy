using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Infrastructure.Data;

namespace SalesHierarchy.Infrastructure.Repositories
{
    /// <summary>
    /// ADO.NET persistence for the append-only audit trail (dbo.AuditLog).
    /// Passwords, reset tokens, and invitation links are never stored here.
    /// </summary>
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly SqlConnectionFactory _factory;

        public AuditLogRepository(SqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public void Add(AuditLog log)
        {
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"INSERT INTO dbo.AuditLog (OccurredUtc, UserEmail, [Action], EntityName, EntityId, Summary, Details)
                  VALUES (@occurred, @email, @action, @entity, @entityId, @summary, @details);", conn);

            cmd.Parameters.AddWithValue("@occurred", log.OccurredUtc);
            cmd.Parameters.AddWithValue("@email", (object?)log.UserEmail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@action", log.Action);
            cmd.Parameters.AddWithValue("@entity", log.EntityName);
            cmd.Parameters.AddWithValue("@entityId", (object?)log.EntityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@summary", (object?)log.Summary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@details", (object?)log.Details ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public IEnumerable<AuditLog> GetRecent(int take = 200, string? entityName = null, string? action = null, string? search = null)
        {
            var list = new List<AuditLog>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"SELECT TOP (@take) ID, OccurredUtc, UserEmail, [Action], EntityName, EntityId, Summary, Details
                  FROM dbo.AuditLog
                  WHERE (@entity IS NULL OR EntityName = @entity)
                    AND (@action IS NULL OR [Action] = @action)
                    AND (@search IS NULL
                         OR Summary LIKE '%' + @search + '%'
                         OR UserEmail LIKE '%' + @search + '%'
                         OR EntityId LIKE '%' + @search + '%')
                  ORDER BY OccurredUtc DESC, ID DESC;", conn);

            cmd.Parameters.AddWithValue("@take", take < 1 ? 1 : take);
            cmd.Parameters.AddWithValue("@entity", (object?)entityName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@action", (object?)action ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@search", (object?)search ?? DBNull.Value);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }

            return list;
        }

        public IEnumerable<AuditLog> GetForEntity(string entityName, string entityId, int take = 200)
        {
            var list = new List<AuditLog>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"SELECT TOP (@take) ID, OccurredUtc, UserEmail, [Action], EntityName, EntityId, Summary, Details
                  FROM dbo.AuditLog
                  WHERE EntityName = @entity AND EntityId = @entityId
                  ORDER BY OccurredUtc DESC, ID DESC;", conn);

            cmd.Parameters.AddWithValue("@take", take < 1 ? 1 : take);
            cmd.Parameters.AddWithValue("@entity", entityName);
            cmd.Parameters.AddWithValue("@entityId", entityId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(Map(reader));
            }

            return list;
        }

        private static AuditLog Map(SqlDataReader reader)
        {
            return new AuditLog
            {
                ID = (long)reader["ID"],
                OccurredUtc = (DateTime)reader["OccurredUtc"],
                UserEmail = reader["UserEmail"] as string,
                Action = reader["Action"].ToString() ?? string.Empty,
                EntityName = reader["EntityName"].ToString() ?? string.Empty,
                EntityId = reader["EntityId"] as string,
                Summary = reader["Summary"] as string,
                Details = reader["Details"] as string
            };
        }
    }
}
