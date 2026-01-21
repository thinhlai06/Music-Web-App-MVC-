using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicWeb.Data;
using MusicWeb.Models.Entities;
using MusicWeb.Services;

namespace MusicWeb.Controllers;

[Authorize]
public class UploadController : Controller
{
    private readonly IStorageService _storageService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMusicService _musicService;

    public UploadController(
        IStorageService storageService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMusicService musicService)
    {
        _storageService = storageService;
        _context = context;
        _userManager = userManager;
        _musicService = musicService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Index(UploadSongViewModel model)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var artist = await _context.Artists.FirstOrDefaultAsync(a => a.UserId == user.Id);
        if (artist == null)
        {
            artist = new Artist
            {
                Name = user.DisplayName ?? user.UserName ?? "Unknown Artist",
                UserId = user.Id,
                AvatarUrl = user.AvatarUrl
            };
            _context.Artists.Add(artist);
            await _context.SaveChangesAsync();
        }

        string safeUserName = (user.UserName ?? "user").Replace("@", "-").Replace(".", "-");
        foreach (char c in Path.GetInvalidFileNameChars()) { safeUserName = safeUserName.Replace(c, '-'); }
        string userFolder = $"{safeUserName}-{user.Id}";

        string audioUrl = string.Empty;
        if (model.AudioFile != null)
        {
            try 
            {
                audioUrl = await _storageService.UploadFileAsync(model.AudioFile.OpenReadStream(), model.AudioFile.FileName, "music", userFolder);
            }
            catch (Exception ex)
            {
                 return BadRequest(new { success = false, message = "Lỗi khi upload nhạc: " + ex.Message });
            }
        }
        else
        {
            return BadRequest(new { success = false, message = "Vui lòng chọn file nhạc" });
        }

        
        string? coverUrl = null;
        if (model.CoverFile != null)
        {
            try 
            {
                coverUrl = await _storageService.UploadFileAsync(model.CoverFile.OpenReadStream(), model.CoverFile.FileName, "covers", userFolder);
            }
             catch (Exception ex)
            {
                 return BadRequest(new { success = false, message = "Lỗi khi upload ảnh: " + ex.Message });
            }
        }

        // Handle Lyrics File (.lrc or .txt for Karaoke)
        string? lyricsUrl = null;
        if (model.LyricsFile != null)
        {
            try 
            {
                lyricsUrl = await _storageService.UploadFileAsync(model.LyricsFile.OpenReadStream(), model.LyricsFile.FileName, "lyrics", userFolder);
            }
            catch (Exception ex)
            {
                // Lyrics is optional, just log and continue
                Console.WriteLine($"[Upload] Lyrics upload failed: {ex.Message}");
            }
        }

        var song = new Song
        {
            Title = model.Title,
            ArtistId = artist.Id,
            AudioUrl = audioUrl,
            CoverUrl = coverUrl,
            LyricsUrl = lyricsUrl,
            Duration = TimeSpan.FromSeconds(180), 
            ReleaseDate = DateTime.UtcNow,
            Description = model.Description,
            IsPublic = model.IsPublic
        };

        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        if (model.GenreId > 0)
        {
            var songGenre = new SongGenre
            {
                SongId = song.Id,
                GenreId = model.GenreId
            };
            _context.SongGenres.Add(songGenre);
            await _context.SaveChangesAsync();
        }

        // Send Notifications to Followers
        var followers = await _context.UserFollows
            .Where(uf => uf.FolloweeId == user.Id)
            .Select(uf => uf.FollowerId)
            .ToListAsync();

        if (followers.Any())
        {
            var notifications = followers.Select(followerId => new Notification
            {
                UserId = followerId,
                Title = "Nhạc mới từ " + artist.Name,
                Message = $"{artist.Name} vừa ra mắt bài hát mới: {song.Title}",
                Link = $"/Song/Detail/{song.Id}", // Assume Detail or Play action exists, checking Home later
                Type = "NewRelease",
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true, message = "Upload bài hát thành công!" });
    }

    [HttpPost("/upload/toggle/{id:int}")]
    public async Task<IActionResult> ToggleVisibility(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

        var success = await _musicService.ToggleSongVisibilityAsync(id, user.Id);
        if (!success) return BadRequest(new { success = false, message = "Không thể thay đổi trạng thái" });

        return Ok(new { success = true });
    }

    [HttpPost("/upload/delete/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized(new { success = false, message = "Vui lòng đăng nhập" });

        var success = await _musicService.DeleteSongAsync(id, user.Id);
        if (!success) return BadRequest(new { success = false, message = "Không thể xóa bài hát (Bạn không phải chủ sở hữu hoặc lỗi hệ thống)" });

        return Ok(new { success = true, message = "Đã xóa bài hát" });
    }
}


public class UploadSongViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IFormFile AudioFile { get; set; } = null!;
    public IFormFile? CoverFile { get; set; }
    public IFormFile? LyricsFile { get; set; }
    public int GenreId { get; set; }
    public bool IsPublic { get; set; } = true;
}
