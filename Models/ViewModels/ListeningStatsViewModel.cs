namespace MusicWeb.Models.ViewModels;

public class ListeningStatsViewModel
{
    // Summary stats
    public int TotalPlays { get; set; }
    public TimeSpan TotalListeningTime { get; set; }
    public int UniqueArtists { get; set; }
    public int UniqueSongs { get; set; }
    public int UniqueGenres { get; set; }
    
    // Top lists
    public List<TopArtistStat> TopArtists { get; set; } = new();
    public List<TopGenreStat> TopGenres { get; set; } = new();
    public List<TopSongStat> TopSongs { get; set; } = new();
    
    // Time-based stats
    public Dictionary<string, int> PlaysByDayOfWeek { get; set; } = new();
    public Dictionary<int, int> PlaysByHour { get; set; } = new();
    
    // Period info
    public string Period { get; set; } = "all";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public record TopArtistStat(int ArtistId, string Name, string? ImageUrl, int PlayCount, double Percentage);
public record TopGenreStat(int GenreId, string Name, int PlayCount, double Percentage);
public record TopSongStat(int SongId, string Title, string Artist, string? CoverUrl, int PlayCount, TimeSpan TotalDuration);
