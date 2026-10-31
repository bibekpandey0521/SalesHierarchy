using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Web.ViewModels
{
    public class CreateAccountViewModel
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(200)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Designation is required.")]
        [StringLength(100)]
        public string Designation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256)]
        [Display(Name = "Login email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Reporting manager")]
        public int? ManagerId { get; set; }
    }
}
