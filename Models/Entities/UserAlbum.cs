namespace MusicWeb.Models.Entities;

using System.ComponentModel.DataAnnotations.Schema;

public class UserAlbum
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public bool IsPublic { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public ApplicationUser Owner { get; set; } = null!;
    public ICollection<UserAlbumSong> UserAlbumSongs { get; set; } = new List<UserAlbumSong>();
}
