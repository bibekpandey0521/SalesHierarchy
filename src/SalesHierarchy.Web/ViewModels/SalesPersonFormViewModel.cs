using System.ComponentModel.DataAnnotations;
using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Web.ViewModels
{
    /// <summary>
    /// Form model for creating/editing a salesperson together with the many-to-many
    /// POD assignments. Kept separate from the persistence model so submitted POD IDs
    /// and manager options are explicit and cannot be over-posted into other fields.
    /// </summary>
    public class SalesPersonFormViewModel
    {
        public int ID { get; set; }

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

        [Display(Name = "Assigned PODs")]
        public List<int> PODIds { get; set; } = new();

        public IReadOnlyList<SalesPerson> Managers { get; set; } = Array.Empty<SalesPerson>();
        public IReadOnlyList<POD> AvailablePODs { get; set; } = Array.Empty<POD>();
        public IReadOnlyDictionary<int, PODStation> Stations { get; set; } =
            new Dictionary<int, PODStation>();
        public IReadOnlyList<string> DesignationOptions { get; set; } = Array.Empty<string>();
    }
}
