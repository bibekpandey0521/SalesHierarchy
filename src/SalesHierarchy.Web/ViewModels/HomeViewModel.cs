using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Web.ViewModels
{
    public class HomeViewModel
    {
        public bool IsAuthenticated { get; set; }
        public bool IsAdmin { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;

        public int EmployeeCount { get; set; }
        public int StationCount { get; set; }
        public int PodCount { get; set; }
        public int AssignedPods { get; set; }
        public int Levels { get; set; }
        public int DealerCount { get; set; }
        public int NewEmployeeCount { get; set; }

        public IReadOnlyList<GrowthPoint> Growth { get; set; } = Array.Empty<GrowthPoint>();
        public IReadOnlyList<AuditLog> Activity { get; set; } = Array.Empty<AuditLog>();
        public IReadOnlyList<PodRow> Pods { get; set; } = Array.Empty<PodRow>();
        public IReadOnlyList<SalesPerson> RecentEmployees { get; set; } = Array.Empty<SalesPerson>();
    }

    public class GrowthPoint
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class PodRow
    {
        public POD Pod { get; set; } = null!;
        public string StationName { get; set; } = string.Empty;
        public string? SalesPersonName { get; set; }

        public bool IsAssigned => Pod.salespersonId.HasValue && !string.IsNullOrWhiteSpace(SalesPersonName);
    }

    public class HierarchyNode
    {
        public SalesPerson Person { get; set; } = null!;
        public List<HierarchyNode> Children { get; set; } = new();
        public bool IsCurrent { get; set; }
    }
}
