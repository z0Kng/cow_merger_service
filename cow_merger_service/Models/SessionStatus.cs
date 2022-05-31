namespace cow_merger_service.Models
{
    public class SessionStatus
    {
        
        public SessionState State { get; set; }
        public string ImageName { get; set; }
        public int OriginalImageVersion { get; set; } = -1;
        public int NewImageVersion { get; set; } = -1;
        public int MergedBlocks { get; set; }
        public int TotalBlocks { get; set; }
       
    }
}
