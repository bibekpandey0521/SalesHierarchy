using System.Collections.Generic;
using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Core.DTOs
{
    public class PublicSalesPersonDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public int? ManagerId { get; set; }
    }

    public class PublicPodDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int OSSPODID { get; set; }
    }

    public class PublicPodStationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
