namespace MusicWeb.Models.Entities;

public class LyricLine
{
    public int Id { get; set; }
    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
    public double TimestampSeconds { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsHighlighted { get; set; }
}

