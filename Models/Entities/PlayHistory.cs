namespace MusicWeb.Models.Entities;

public class PlayHistory
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int SongId { get; set; }
    public Song Song { get; set; } = null!;
}

