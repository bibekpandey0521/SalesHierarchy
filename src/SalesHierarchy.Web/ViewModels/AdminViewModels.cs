using SalesHierarchy.Web.Data;

namespace SalesHierarchy.Web.ViewModels
{
    public class UserAccountRow
    {
        public ApplicationUser User { get; set; } = null!;
        public bool HasPassword { get; set; }
        public bool IsDisabled { get; set; }
        public string? SalesPersonName { get; set; }

        public string Status
        {
            get
            {
                if (IsDisabled)
                {
                    return "Disabled";
                }

                return HasPassword ? "Active" : "Invitation pending";
            }
        }
    }
}
