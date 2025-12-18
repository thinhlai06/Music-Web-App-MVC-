using MusicWeb.Models.Entities;

namespace MusicWeb.Models.ViewModels;

public class PlaylistDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CoverUrl { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public List<SongCardViewModel> Songs { get; set; } = new();
}
