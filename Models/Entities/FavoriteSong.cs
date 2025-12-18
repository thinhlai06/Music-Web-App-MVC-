namespace MusicWeb.Models.Entities;

public class FavoriteSong
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int SongId { get; set; }
    public Song Song { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

