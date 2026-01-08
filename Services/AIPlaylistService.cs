using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;
using MusicWeb.Models.ViewModels;

namespace MusicWeb.Services;

public class AIPlaylistService : IAIPlaylistService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IMusicService _musicService;

    public AIPlaylistService(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IMusicService musicService)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _musicService = musicService;
    }

    public async Task<SmartPlaylistResult> GeneratePreviewAsync(string prompt, string userId)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new SmartPlaylistResult
            {
                Success = false,
                Error = "Vui lòng nhập mô tả playlist"
            };
        }

        try
        {
            // Step 1: Parse prompt using Gemini
            var criteria = await ParsePromptWithGeminiAsync(prompt);

            // Step 2: Query database with parsed criteria
            var songs = await QuerySongsAsync(criteria, userId);

            if (songs.Count == 0)
            {
                return new SmartPlaylistResult
                {
                    Success = true,
                    SuggestedName = criteria.SuggestedName ?? "Playlist mới",
                    Criteria = criteria,
                    Songs = new List<SongCardViewModel>(),
                    TotalMatches = 0,
                    Error = "Không tìm thấy bài hát phù hợp với yêu cầu của bạn"
                };
            }

            return new SmartPlaylistResult
            {
                Success = true,
                SuggestedName = criteria.SuggestedName ?? "Playlist mới",
                Criteria = criteria,
                Songs = songs,
                TotalMatches = songs.Count
            };
        }
        catch (Exception ex)
        {
            return new SmartPlaylistResult
            {
                Success = false,
                Error = $"Đã xảy ra lỗi: {ex.Message}"
            };
        }
    }

    public async Task<Playlist?> CreatePlaylistFromPreviewAsync(
        string playlistName, 
        List<int> songIds, 
        string userId)
    {
        if (string.IsNullOrWhiteSpace(playlistName) || songIds.Count == 0)
        {
            return null;
        }

        try
        {
            // Use existing MusicService to create playlist
            var playlist = await _musicService.CreatePlaylistAsync(playlistName.Trim(), userId);

            // Add each song to the playlist using existing method
            foreach (var songId in songIds)
            {
                await _musicService.AddSongToPlaylistAsync(playlist.Id, songId, userId);
            }

            return playlist;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ParsedCriteria> ParsePromptWithGeminiAsync(string prompt)
    {
        var apiKey = _configuration["GeminiApi:ApiKey"];
        var model = _configuration["GeminiApi:Model"] ?? "gemini-2.5-flash-lite";

        if (string.IsNullOrEmpty(apiKey))
        {
            // Fallback: use prompt as keyword search
            return new ParsedCriteria
            {
                Keywords = new List<string> { prompt },
                SuggestedName = $"Playlist - {prompt.Substring(0, Math.Min(30, prompt.Length))}"
            };
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = $@"Bạn là AI assistant giúp tạo playlist nhạc Việt. Phân tích yêu cầu người dùng và trích xuất thông tin.

CÁC THỂ LOẠI CÓ SẴN TRONG DATABASE (chỉ sử dụng các tên này):
- EDM Sôi Động
- Acoustic Chill
- Lofi Học Bài
- Bolero Trữ Tình
- Nhạc Âu Mỹ
- Nhạc Hàn Quốc
- Nhạc Hoa Ngữ
- Nhạc Việt
- Nhạc Thiếu Nhi
- Nhạc Hip Hop
- Nhạc Rock
- Nhạc R&B
- Nhạc Jazz

ÁNH XẠ MOOD SANG THỂ LOẠI:
- Buồn, tâm trạng, chill, nhẹ nhàng, sâu lắng → Bolero Trữ Tình, Acoustic Chill, Lofi Học Bài
- Vui, sôi động, party, năng lượng, nhảy → EDM Sôi Động, Nhạc Việt
- Mạnh mẽ, ngầu, underground, rap → Nhạc Hip Hop
- Học bài, tập trung, làm việc → Lofi Học Bài, Acoustic Chill
- Nhạc nước ngoài, US-UK → Nhạc Âu Mỹ
- Kpop, Hàn → Nhạc Hàn Quốc
- Cpop, Hoa → Nhạc Hoa Ngữ

Yêu cầu từ user: ""{prompt}""

Trả về JSON thuần túy (KHÔNG markdown, KHÔNG code block):
{{
  ""genres"": [""tên thể loại CHÍNH XÁC từ danh sách trên""],
  ""artists"": [""tên nghệ sĩ/ca sĩ nếu có""],
  ""keywords"": [""từ khóa trong tên bài hát nếu có""],
  ""suggestedName"": ""Tên playlist gợi ý ngắn gọn""
}}

Chỉ trả về JSON, không thêm text."
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 256
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}",
                content);

            if (!response.IsSuccessStatusCode)
            {
                // Fallback on API error
                return CreateFallbackCriteria(prompt);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return ParseGeminiResponse(responseJson, prompt);
        }
        catch
        {
            // Fallback on network error
            return CreateFallbackCriteria(prompt);
        }
    }

    private ParsedCriteria ParseGeminiResponse(string responseJson, string originalPrompt)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            // Navigate Gemini response structure
            var text = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(text))
            {
                return CreateFallbackCriteria(originalPrompt);
            }

            // Clean up the response (remove markdown code blocks if present)
            text = text.Trim();
            if (text.StartsWith("```json"))
            {
                text = text.Substring(7);
            }
            if (text.StartsWith("```"))
            {
                text = text.Substring(3);
            }
            if (text.EndsWith("```"))
            {
                text = text.Substring(0, text.Length - 3);
            }
            text = text.Trim();

            // Parse the JSON from Gemini
            var criteria = JsonSerializer.Deserialize<ParsedCriteria>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return criteria ?? CreateFallbackCriteria(originalPrompt);
        }
        catch
        {
            return CreateFallbackCriteria(originalPrompt);
        }
    }

    private ParsedCriteria CreateFallbackCriteria(string prompt)
    {
        return new ParsedCriteria
        {
            Keywords = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
            SuggestedName = $"Playlist - {prompt.Substring(0, Math.Min(30, prompt.Length))}"
        };
    }

    private async Task<List<SongCardViewModel>> QuerySongsAsync(ParsedCriteria criteria, string userId)
    {
        // Start with base query - only public songs
        var query = _context.Songs
            .AsNoTracking()
            .Include(s => s.Artist)
            .Include(s => s.SongGenres)
                .ThenInclude(sg => sg.Genre)
            .Include(s => s.UserRatings)
            .Where(s => s.IsPublic);

        // Build filter conditions
        var hasGenreFilter = criteria.Genres.Any();
        var hasArtistFilter = criteria.Artists.Any();
        var hasKeywordFilter = criteria.Keywords.Any();

        if (hasGenreFilter || hasArtistFilter || hasKeywordFilter)
        {
            // Use OR logic - match if ANY criteria is satisfied
            query = query.Where(s =>
                // Genre filter - match if song has any of the requested genres
                (criteria.Genres.Count > 0 && s.SongGenres.Any(sg => 
                    criteria.Genres.Any(g => sg.Genre.Name.ToLower().Contains(g.ToLower())))) ||
                // Artist filter - match if artist name contains any requested artist
                (criteria.Artists.Count > 0 && 
                    criteria.Artists.Any(a => s.Artist.Name.ToLower().Contains(a.ToLower()))) ||
                // Keyword filter - match if title contains any keyword
                (criteria.Keywords.Count > 0 && 
                    criteria.Keywords.Any(k => s.Title.ToLower().Contains(k.ToLower())))
            );
        }

        // Execute query
        var songs = await query
            .OrderByDescending(s => s.ViewCount)
            .ToListAsync();

        // Get user favorites and ratings
        var favoriteIds = new HashSet<int>();
        var userRatings = new Dictionary<int, decimal>();

        if (!string.IsNullOrEmpty(userId))
        {
            favoriteIds = (await _context.FavoriteSongs
                .Where(f => f.UserId == userId)
                .Select(f => f.SongId)
                .ToListAsync()).ToHashSet();

            userRatings = await _context.UserSongRatings
                .Where(r => r.UserId == userId)
                .ToDictionaryAsync(r => r.SongId, r => r.Rating);
        }

        // Map to view models
        return songs.Select(song => new SongCardViewModel(
            song.Id,
            song.Title,
            song.Artist.Name,
            song.CoverUrl ?? string.Empty,
            $"{(int)song.Duration.TotalMinutes}:{song.Duration.Seconds:00}",
            favoriteIds.Contains(song.Id),
            song.AudioUrl ?? string.Empty,
            userRatings.TryGetValue(song.Id, out var rating) ? rating : null,
            song.SongGenres.FirstOrDefault()?.Genre.Name ?? "Unknown",
            song.ReleaseDate,
            song.ViewCount,
            song.UserRatings.Any() ? (decimal?)song.UserRatings.Average(r => (double)r.Rating) : null,
            song.IsPublic,
            song.SongGenres.FirstOrDefault()?.GenreId,
            song.IsPremium,
            song.PremiumStatus
        )).ToList();
    }
}
