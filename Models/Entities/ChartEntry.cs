namespace MusicWeb.Models.Entities;

public class ChartEntry
{
    public int Id { get; set; }
    public DateTime ChartDate { get; set; } = DateTime.UtcNow;
    public string ChartType { get; set; } = "weekly";
    public int Rank { get; set; }
    public double Percentage { get; set; }

    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
}

