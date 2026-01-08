namespace MusicWeb.Models.ViewModels;

/// <summary>Kết quả phân tích từ Gemini</summary>
public class ParsedCriteria
{
    public List<string> Genres { get; set; } = new();
    public List<string> Artists { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public string? SuggestedName { get; set; }
}

/// <summary>Response trả về sau khi preview</summary>
public class SmartPlaylistResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string SuggestedName { get; set; } = string.Empty;
    public ParsedCriteria? Criteria { get; set; }
    public List<SongCardViewModel> Songs { get; set; } = new();
    public int TotalMatches { get; set; }
}
