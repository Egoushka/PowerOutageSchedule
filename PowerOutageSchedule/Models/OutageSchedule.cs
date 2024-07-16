namespace PowerOutageSchedule.Models;

public class OutageSchedule
{
    public int GroupId { get; set; }
    public string OutageTimes { get; set; } // e.g., "09:00-12:00;15:00-18:00"
}
