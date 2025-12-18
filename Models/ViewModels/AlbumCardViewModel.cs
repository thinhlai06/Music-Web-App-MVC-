
namespace MusicWeb.Models.ViewModels;

public class AlbumCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    
    public AlbumCardViewModel(int id, string title, string artistName, string coverUrl)
    {
        Id = id;
        Title = title;
        ArtistName = artistName;
        CoverUrl = coverUrl;
    }

    public AlbumCardViewModel() { }
}
