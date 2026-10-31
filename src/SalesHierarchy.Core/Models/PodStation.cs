using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Core.Models
{
    public class PodStation
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "OSS Pod Station Id is required and must be a positive number.")]
        [Display(Name = "OSS Pod Station Id")]
        public int OSSPODStationId { get; set; }

        [Required(ErrorMessage = "Station name is required.")]
        [StringLength(150, ErrorMessage = "Station name must be 150 characters or fewer.")]
        [Display(Name = "Station name")]
        public string Name { get; set; } = string.Empty;
    }
}
