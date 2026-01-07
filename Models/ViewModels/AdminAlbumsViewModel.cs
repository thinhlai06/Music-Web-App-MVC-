using MusicWeb.Models.Entities;

namespace MusicWeb.Models.ViewModels;

public class AdminAlbumsViewModel
{
    public List<Album> SystemAlbums { get; set; } = new();
    public List<UserAlbum> UserAlbums { get; set; } = new();
}
