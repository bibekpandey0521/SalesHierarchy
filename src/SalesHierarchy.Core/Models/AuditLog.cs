using System;

namespace SalesHierarchy.Core.Models
{
    /// <summary>
    /// Append-only record of business and account changes. Tokens and passwords are
    /// never written to this table.
    /// </summary>
    public class AuditLog
    {
        public long ID { get; set; }
        public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
        public string? UserEmail { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? Summary { get; set; }
        public string? Details { get; set; }

        /// <summary>
        /// Optional salesperson this event belongs to (for example the owner of a
        /// reassigned POD or the subject of an account change). Used to scope history
        /// to a manager's own subtree without parsing the details payload.
        /// </summary>
        public int? RelatedSalesPersonId { get; set; }
    }
}
