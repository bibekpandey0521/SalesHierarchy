using System;
namespace SalesHierarchy.Core.Models
{
    public class Dealer
    {
        public int UniqueID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
    }
}
