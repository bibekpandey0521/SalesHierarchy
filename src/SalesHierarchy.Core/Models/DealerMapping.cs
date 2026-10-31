using System;
using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Core.Models
{
    public class DealerMapping
    {
        public int UniqueID { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a salesperson.")]
        [Display(Name = "Salesperson")]
        public int SalesPersonId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a dealer.")]
        [Display(Name = "Dealer")]
        public int DealerId { get; set; }
    }
}
