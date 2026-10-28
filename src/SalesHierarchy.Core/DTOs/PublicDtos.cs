using System.Collections.Generic;
using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Core.DTOs
{
    public class PublicSalesPersonDto
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public int? ManagerId { get; set; }
    }

    public class PublicPODDto
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? OSSPODID { get; set; }
    }

    public class PublicPODStationDto
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
