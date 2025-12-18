namespace MusicWeb.Models.Entities;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TileImageUrl { get; set; }
    public ICollection<SongGenre> SongGenres { get; set; } = new List<SongGenre>();
}

