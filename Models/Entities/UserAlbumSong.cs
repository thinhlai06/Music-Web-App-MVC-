namespace MusicWeb.Models.Entities;

public class UserAlbumSong
{
    public int UserAlbumId { get; set; }
    public int SongId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public int OrderIndex { get; set; } = 0;

    // Navigation properties
    public UserAlbum UserAlbum { get; set; } = null!;
    public Song Song { get; set; } = null!;
}
