using MusicWeb.Models.ViewModels;

namespace MusicWeb.Models.ViewModels;

public class ArtistDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public long TotalViews { get; set; }
    public bool IsFollowing { get; set; }
    public string? UserId { get; set; }
    public List<SongCardViewModel> PopularSongs { get; set; } = new();
}
