using System.Text.Json;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Web.Services
{
    /// <summary>
    /// Writes an append-only audit record for business and account operations.
    /// Audit failures never break the user operation; they are logged as warnings.
    /// Passwords, reset tokens, and invitation links must never be passed here.
    /// </summary>
    public class AuditService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private readonly IAuditLogRepository _repository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            IAuditLogRepository repository,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditService> logger)
        {
            _repository = repository;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public void Record(string action, string entityName, string? entityId, string summary, object? details = null, string? actorEmail = null)
        {
            try
            {
                var log = new AuditLog
                {
                    OccurredUtc = DateTime.UtcNow,
                    UserEmail = actorEmail ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name,
                    Action = Trim(action, 32)!,
                    EntityName = Trim(entityName, 64)!,
                    EntityId = Trim(entityId, 64),
                    Summary = Trim(summary, 400),
                    Details = details == null ? null : JsonSerializer.Serialize(details, JsonOptions)
                };

                _repository.Add(log);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log write failed for {Entity} {EntityId}.", entityName, entityId);
            }
        }

        private static string? Trim(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
