namespace MusicWeb.Models.Entities;

public class Album
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;
    public string? CoverUrl { get; set; }

    public int ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;

    public ICollection<Song> Songs { get; set; } = new List<Song>();
}

