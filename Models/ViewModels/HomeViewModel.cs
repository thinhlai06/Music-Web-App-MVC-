namespace MusicWeb.Models.ViewModels;

public class HomeViewModel
{
    public UserProfileViewModel? CurrentUser { get; set; }
    public List<SongCardViewModel> Recommendations { get; set; } = new();
    public List<SongCardViewModel> NewReleases { get; set; } = new();
    public List<ChartItemViewModel> Chart { get; set; } = new();
    public List<PlaylistCardViewModel> TrendingPlaylists { get; set; } = new();
    public List<GenreTileViewModel> Genres { get; set; } = new();
    public List<PlaylistCardViewModel> PersonalPlaylists { get; set; } = new();
    public List<SongCardViewModel> FavoriteSongs { get; set; } = new();
    public List<SongCardViewModel> RecentlyPlayed { get; set; } = new();
    public List<SongCardViewModel> UploadedSongs { get; set; } = new();
    public List<AlbumCardViewModel> Albums { get; set; } = new();
    public bool IsAdmin { get; set; }
}

public record UserProfileViewModel(string DisplayName, string Email, string? AvatarUrl, string UserId);

public record SongCardViewModel(
    int Id,
    string Title,
    string Artist,
    string CoverUrl,
    string Duration,
    bool IsFavorite,
    string AudioUrl,
    decimal? UserRating = null,
    string Genre = "",
    DateTime? ReleaseDate = null,
    int ViewCount = 0,
    decimal? AverageRating = null);

public record ChartItemViewModel(int SongId, string Title, string Artist, string CoverUrl, int Rank, double Percentage, int ViewCount);

public record PlaylistCardViewModel(int Id, string Title, string Subtitle, string CoverUrl);

public record GenreTileViewModel(int Id, string Title, string ImageUrl);
