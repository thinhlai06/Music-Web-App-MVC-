namespace MusicWeb.Models.ViewModels;

public class UserAlbumCardViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public int SongCount { get; set; }
    public bool IsPublic { get; set; }
}

public class UserAlbumDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CoverUrl { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool IsOwner { get; set; }
    public List<SongCardViewModel> Songs { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
