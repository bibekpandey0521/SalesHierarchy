using System.ComponentModel.DataAnnotations;

namespace SalesHierarchy.Core.Models
{
    public class Pod
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "OSS Pod Id is required and must be a positive number.")]
        [Display(Name = "OSS Pod Id")]
        public int OSSPODID { get; set; }

        [Required(ErrorMessage = "Pod name is required.")]
        [StringLength(150, ErrorMessage = "Pod name must be 150 characters or fewer.")]
        [Display(Name = "Pod name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Station assignment is managed on the dedicated Pod assignment page, so a Pod
        /// can exist in master data without a station yet.
        /// </summary>
        [Display(Name = "Pod station")]
        public int? PODStationId { get; set; }

        /// <summary>
        /// Legacy single-owner column kept temporarily for compatibility. New ownership
        /// is stored in SalesPersonPod and this value is synchronised from that table.
        /// </summary>
        public int? salespersonId { get; set; }
    }
}
