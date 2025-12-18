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

    public UploadController(
        IStorageService storageService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _storageService = storageService;
        _context = context;
        _userManager = userManager;
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

        // 1. Ensure Artist Profile exists for this User (Sync Logic)
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

        // Generate readable folder name: username-userid
        string safeUserName = (user.UserName ?? "user").Replace("@", "-").Replace(".", "-");
        foreach (char c in Path.GetInvalidFileNameChars()) { safeUserName = safeUserName.Replace(c, '-'); }
        string userFolder = $"{safeUserName}-{user.Id}";

        // 2. Upload Audio
        string audioUrl = string.Empty;
        if (model.AudioFile != null)
        {
            try 
            {
                // Pass userFolder to organize files by User
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

        // 3. Upload Cover (Optional)
        string? coverUrl = null;
        if (model.CoverFile != null)
        {
            try 
            {
                // Pass userFolder to organize files by User
                coverUrl = await _storageService.UploadFileAsync(model.CoverFile.OpenReadStream(), model.CoverFile.FileName, "covers", userFolder);
            }
             catch (Exception ex)
            {
                 // Log warning but continue? Or fail? Let's fail strictly for now.
                 return BadRequest(new { success = false, message = "Lỗi khi upload ảnh: " + ex.Message });
            }
        }

        // 4. Save Song
        var song = new Song
        {
            Title = model.Title,
            ArtistId = artist.Id,
            AudioUrl = audioUrl,
            CoverUrl = coverUrl,
            Duration = TimeSpan.FromSeconds(180), // TODO: Get actual duration? For now default 3 mins.
            ReleaseDate = DateTime.UtcNow,
            Description = model.Description
        };

        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Upload bài hát thành công!" });
    }
}

public class UploadSongViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IFormFile AudioFile { get; set; } = null!;
    public IFormFile? CoverFile { get; set; }
}
