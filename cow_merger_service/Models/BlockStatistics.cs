using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace cow_merger_service.Models
{
    public class BlockStatistics
    {
        public int BlockNumber { get; set; }
        public uint Modifications { get; set; }
    }
}
