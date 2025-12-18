namespace MusicWeb.Models.Entities;

public class Artist
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? UserId { get; set; }
    public string? AvatarUrl { get; set; }
    public ICollection<Song> Songs { get; set; } = new List<Song>();
}

