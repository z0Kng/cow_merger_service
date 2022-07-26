using System.ComponentModel.DataAnnotations;

namespace cow_merger_service.Models
{
    public class SessionStatus
    {
        [Required] public string ImageName { get; set; }

        [Required] public int MergedBlocks { get; set; }

        [Required] public int NewImageVersion { get; set; } = -1;

        [Required] public int OriginalImageVersion { get; set; } = -1;

        [Required] public SessionState State { get; set; }

        [Required] public int TotalBlocks { get; set; }
    }
}