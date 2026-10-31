using System;
using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Core.Models
{
    public class Dealer
    {
        public int UniqueID { get; set; }

        [StringLength(150, ErrorMessage = "Dealer name must be 150 characters or fewer.")]
        [Display(Name = "Dealer name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Dealer code is required.")]
        [StringLength(50, ErrorMessage = "Dealer code must be 50 characters or fewer.")]
        [Display(Name = "Dealer code")]
        public string? Code { get; set; }
    }
}
