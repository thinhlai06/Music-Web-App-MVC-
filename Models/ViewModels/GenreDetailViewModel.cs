using MusicWeb.Models.ViewModels;

namespace MusicWeb.Models.ViewModels;

public class GenreDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public List<SongCardViewModel> Songs { get; set; } = new();
}
