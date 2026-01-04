namespace MusicWeb.Models.Entities;

public class Playlist
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public bool IsPublic { get; set; } = false;

    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser Owner { get; set; } = null!;

    public ICollection<PlaylistSong> Songs { get; set; } = new List<PlaylistSong>();
}

