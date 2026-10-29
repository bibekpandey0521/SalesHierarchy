
namespace SalesHierarchy.Core.Models
{
    /// <summary>
    /// Many-to-many link between a salesperson and a POD. The legacy POD.salespersonId
    /// column is retained for compatibility, but this table is the source of truth for
    /// multi-POD assignments.
    /// </summary>
    public class SalesPersonPOD
    {
        public int SalesPersonId { get; set; }
        public int PODId { get; set; }
    }
}
