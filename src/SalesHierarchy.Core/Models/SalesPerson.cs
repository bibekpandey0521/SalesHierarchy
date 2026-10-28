using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Core.Models
{
    public class SalesPerson
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(200, ErrorMessage = "Name must be 200 characters or fewer.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Designation is required.")]
        [StringLength(100, ErrorMessage = "Designation must be 100 characters or fewer.")]
        public string? Designation { get; set; }

        [Display(Name = "Reporting manager")]
        public int? ManagerId { get; set; }
    }
}
