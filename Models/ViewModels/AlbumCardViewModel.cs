
namespace MusicWeb.Models.ViewModels;

public class AlbumCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public bool IsUserAlbum { get; set; } = false;
    
    public AlbumCardViewModel(int id, string title, string artistName, string coverUrl, bool isUserAlbum = false)
    {
        Id = id;
        Title = title;
        ArtistName = artistName;
        CoverUrl = coverUrl;
        IsUserAlbum = isUserAlbum;
    }

    public AlbumCardViewModel() { }
}
