namespace MusicWeb.Models.ViewModels;

public class SearchResultsViewModel
{
    public List<SongCardViewModel> Songs { get; set; } = new();
    public List<ArtistResultViewModel> Artists { get; set; } = new();
    public List<PlaylistCardViewModel> Playlists { get; set; } = new();
    public List<AlbumResultViewModel> Albums { get; set; } = new();
    public List<UserResultViewModel> Users { get; set; } = new();
    public bool HasResults => Songs.Any() || Artists.Any() || Playlists.Any() || Albums.Any() || Users.Any();
}

public record ArtistResultViewModel(int Id, string Name, string? AvatarUrl, string? Bio);

public record AlbumResultViewModel(int Id, string Title, string ArtistName, string? CoverUrl);

public record UserResultViewModel(string Id, string DisplayName, string? Email, string? AvatarUrl);
