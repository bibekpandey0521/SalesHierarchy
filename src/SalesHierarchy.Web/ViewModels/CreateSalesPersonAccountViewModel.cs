using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Web.ViewModels
{
    public class CreateSalesPersonAccountViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Designation { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        public int? ManagerId { get; set; }
    }
}
