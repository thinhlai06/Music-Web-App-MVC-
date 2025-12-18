using MusicWeb.Models.Entities;

namespace MusicWeb.Models.ViewModels;

public class AlbumDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? ArtistAvatar { get; set; } // New property
    public string CoverUrl { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public DateTime ReleaseDate { get; set; }
    public List<SongCardViewModel> Songs { get; set; } = new();
}
