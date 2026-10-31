using System;
using System.Collections.Generic;
using System.Text.Json;
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
                @"INSERT INTO dbo.AuditLog (OccurredUtc, UserEmail, [Action], EntityName, EntityId, Summary, Details, RelatedSalesPersonId)
                  VALUES (@occurred, @email, @action, @entity, @entityId, @summary, @details, @related);", conn);

            cmd.Parameters.AddWithValue("@occurred", log.OccurredUtc);
            cmd.Parameters.AddWithValue("@email", (object?)log.UserEmail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@action", log.Action);
            cmd.Parameters.AddWithValue("@entity", log.EntityName);
            cmd.Parameters.AddWithValue("@entityId", (object?)log.EntityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@summary", (object?)log.Summary ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@details", (object?)log.Details ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@related", (object?)log.RelatedSalesPersonId ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public IEnumerable<AuditLog> GetRecent(int take = 200, string? entityName = null, string? action = null, string? search = null)
        {
            var list = new List<AuditLog>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();
            using var cmd = new SqlCommand(
                @"SELECT TOP (@take) Id, OccurredUtc, UserEmail, [Action], EntityName, EntityId, Summary, Details, RelatedSalesPersonId
                  FROM dbo.AuditLog
                  WHERE (@entity IS NULL OR EntityName = @entity)
                    AND (@action IS NULL OR [Action] = @action)
                    AND (@search IS NULL
                         OR Summary LIKE '%' + @search + '%'
                         OR UserEmail LIKE '%' + @search + '%'
                         OR EntityId LIKE '%' + @search + '%')
                  ORDER BY OccurredUtc DESC, Id DESC;", conn);

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
                @"SELECT TOP (@take) Id, OccurredUtc, UserEmail, [Action], EntityName, EntityId, Summary, Details, RelatedSalesPersonId
                  FROM dbo.AuditLog
                  WHERE EntityName = @entity AND EntityId = @entityId
                  ORDER BY OccurredUtc DESC, Id DESC;", conn);

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

        public IEnumerable<AuditLog> GetForTeam(IReadOnlyCollection<int> salesPersonIds, IReadOnlyCollection<string> userEmails, int take = 300)
        {
            if (salesPersonIds.Count == 0)
            {
                return Array.Empty<AuditLog>();
            }

            var list = new List<AuditLog>();
            using var conn = (SqlConnection)_factory.CreateConnection();
            conn.Open();

            // IDs and emails are passed as JSON parameters, so the SQL text is static.
            var ids = JsonSerializer.Serialize(salesPersonIds.Distinct());
            var emails = JsonSerializer.Serialize(
                userEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase));

            using var cmd = new SqlCommand(
                @"SELECT TOP (@take) Id, OccurredUtc, UserEmail, [Action], EntityName, EntityId, Summary, Details, RelatedSalesPersonId
                  FROM dbo.AuditLog
                  WHERE RelatedSalesPersonId IN (SELECT value FROM OPENJSON(@ids) WITH (value int '$'))
                     OR ([EntityName] = 'SalesPerson'
                         AND TRY_CONVERT(INT, EntityId) IN (SELECT value FROM OPENJSON(@ids) WITH (value int '$')))
                     OR UserEmail IN (SELECT value FROM OPENJSON(@emails) WITH (value nvarchar(256) '$'))
                  ORDER BY OccurredUtc DESC, Id DESC;", conn);

            cmd.Parameters.AddWithValue("@take", take < 1 ? 1 : take);
            cmd.Parameters.AddWithValue("@ids", ids);
            cmd.Parameters.AddWithValue("@emails", emails);

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
                Id = (long)reader["Id"],
                OccurredUtc = (DateTime)reader["OccurredUtc"],
                UserEmail = reader["UserEmail"] as string,
                Action = reader["Action"].ToString() ?? string.Empty,
                EntityName = reader["EntityName"].ToString() ?? string.Empty,
                EntityId = reader["EntityId"] as string,
                Summary = reader["Summary"] as string,
                Details = reader["Details"] as string,
                RelatedSalesPersonId = reader["RelatedSalesPersonId"] == DBNull.Value
                    ? null
                    : (int)reader["RelatedSalesPersonId"]
            };
        }
    }
}
