using System.ComponentModel.DataAnnotations;
using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Web.ViewModels
{
    /// <summary>
    /// Form model for creating/editing a salesperson together with the many-to-many
    /// Pod assignments. Kept separate from the persistence model so submitted Pod IDs
    /// and manager options are explicit and cannot be over-posted into other fields.
    /// </summary>
    public class SalesPersonFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(200, ErrorMessage = "Name must be 200 characters or fewer.")]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Designation is required.")]
        [StringLength(100, ErrorMessage = "Designation must be 100 characters or fewer.")]
        [Display(Name = "Designation")]
        public string? Designation { get; set; }

        [Display(Name = "Reports to")]
        public int? ManagerId { get; set; }

        [Display(Name = "Assigned Pods")]
        public List<int> PodIds { get; set; } = new();

        public IReadOnlyList<SalesPerson> Managers { get; set; } = Array.Empty<SalesPerson>();
        public IReadOnlyList<Pod> AvailablePods { get; set; } = Array.Empty<Pod>();
        public IReadOnlyDictionary<int, PodStation> Stations { get; set; } =
            new Dictionary<int, PodStation>();
        public IReadOnlyList<string> DesignationOptions { get; set; } = Array.Empty<string>();
    }
}
