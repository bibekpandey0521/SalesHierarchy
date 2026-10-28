using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Core.Models
{
    public class PODStation
    {
        public int ID { get; set; }

        [StringLength(50, ErrorMessage = "OSSPODStationId must be 50 characters or fewer.")]
        [Display(Name = "OSS POD Station Id")]
        public string? OSSPODStationId { get; set; }

        [Required(ErrorMessage = "Station name is required.")]
        [StringLength(200, ErrorMessage = "Station name must be 200 characters or fewer.")]
        [Display(Name = "Station name")]
        public string Name { get; set; } = string.Empty;
    }
}
