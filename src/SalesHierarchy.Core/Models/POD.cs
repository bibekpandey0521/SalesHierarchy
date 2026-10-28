using System;
namespace SalesHierarchy.Core.Models
{
    public class POD
    {
        public int ID { get; set; }
        public string? OSSPODID { get; set; }
        public string Name { get; set; } = string.Empty;
        public int PODStationId { get; set; }
        public int? salespersonId { get; set; }
    }
}
