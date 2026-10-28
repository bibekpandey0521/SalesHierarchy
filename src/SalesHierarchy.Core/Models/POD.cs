using System;
using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Core.Models
{
    public class POD
    {
        public int ID { get; set; }

        [StringLength(50, ErrorMessage = "OSSPODID must be 50 characters or fewer.")]
        [Display(Name = "OSS POD Id")]
        public string? OSSPODID { get; set; }

        [Required(ErrorMessage = "POD name is required.")]
        [StringLength(200, ErrorMessage = "POD name must be 200 characters or fewer.")]
        [Display(Name = "POD name")]
        public string Name { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Select a POD station.")]
        [Display(Name = "POD station")]
        public int PODStationId { get; set; }

        [Display(Name = "Assigned salesperson")]
        public int? salespersonId { get; set; }
    }
}
