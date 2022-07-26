using System.ComponentModel.DataAnnotations;

namespace cow_merger_service.Models
{
    public class BlockStatistics
    {
        [Required] public int BlockNumber { get; set; }

        [Required] public uint Modifications { get; set; }
    }
}