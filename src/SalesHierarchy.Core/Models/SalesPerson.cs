using System;
namespace SalesHierarchy.Core.Models
{
    public class SalesPerson
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public int? ManagerId { get; set; }
    }
}
