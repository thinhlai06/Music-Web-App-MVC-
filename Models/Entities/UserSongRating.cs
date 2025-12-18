namespace MusicWeb.Models.Entities;

public class UserSongRating
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int SongId { get; set; }
    public Song Song { get; set; } = null!;

    public decimal Rating { get; set; }
    public DateTime RatedAt { get; set; } = DateTime.UtcNow;
}
