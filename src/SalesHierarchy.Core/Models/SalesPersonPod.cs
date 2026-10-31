
namespace SalesHierarchy.Core.Models
{
    /// <summary>
    /// Many-to-many link between a salesperson and a Pod. The legacy Pod.salespersonId
    /// column is retained for compatibility, but this table is the source of truth for
    /// multi-Pod assignments.
    /// </summary>
    public class SalesPersonPod
    {
        public int SalesPersonId { get; set; }
        public int PodId { get; set; }
    }
}
