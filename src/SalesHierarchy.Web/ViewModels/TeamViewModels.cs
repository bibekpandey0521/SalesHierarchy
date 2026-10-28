using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Web.ViewModels
{
    public class CreateTeamAccountViewModel
    {
        public int SalesPersonId { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(256)]
        [Display(Name = "Login email")]
        public string Email { get; set; } = string.Empty;
    }
}
