using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MusicWeb.Models;
using MusicWeb.Models.Entities;
using MusicWeb.Services;

namespace MusicWeb.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IMusicService _musicService;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(
        ILogger<HomeController> logger,
        IMusicService musicService,
        UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _musicService = musicService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        var model = await _musicService.BuildHomeAsync(userId);
        model.IsAdmin = User.IsInRole("Admin");
        return View(model);
    }

    [HttpGet("/search")]
    public async Task<IActionResult> Search(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Json(new { success = true, data = new { } });
        }

        var userId = _userManager.GetUserId(User);
        var results = await _musicService.SearchAsync(term, userId);
        return Json(new { success = true, data = results });
    }

    [Authorize]
    [HttpPost("/favorites/{songId:int}")]
    public async Task<IActionResult> ToggleFavorite(int songId)
    {
        var userId = _userManager.GetUserId(User)!;
        var added = await _musicService.ToggleFavoriteAsync(songId, userId);
        return Json(new { success = true, isFavorite = added });
    }

    [HttpGet("/lyrics/{songId:int}")]
    public async Task<IActionResult> GetLyrics(int songId)
    {
        var (lyrics, title, artist) = await _musicService.GetLyricsAsync(songId);
        return Json(new { success = true, data = new { lyrics, title, artist } });
    }

    [HttpPost("/player/play/{songId:int}")]
    public async Task<IActionResult> RecordPlay(int songId)
    {
        var userId = _userManager.GetUserId(User);
        await _musicService.RecordPlayAsync(songId, userId);
        return Json(new { success = true });
    }

    [HttpGet("/album/{id:int}")]
    public async Task<IActionResult> GetAlbum(int id)
    {
        var userId = _userManager.GetUserId(User);
        var album = await _musicService.GetAlbumDetailAsync(id, userId);
        if (album == null) return NotFound();
        return PartialView("_AlbumDetailSection", album);
    }

    [HttpGet("/genre/{id:int}")]
    public async Task<IActionResult> GetGenre(int id)
    {
        var userId = _userManager.GetUserId(User);
        var genre = await _musicService.GetGenreDetailAsync(id, userId);
        if (genre == null) return NotFound();
        return PartialView("_GenreDetailSection", genre);
    }

    [Authorize]
    [HttpPost("/songs/{songId:int}/rating")]
    public async Task<IActionResult> SetSongRating(int songId, [FromBody] RatingRequest request)
    {
        if (request.Rating < 0 || request.Rating > 5)
        {
            return BadRequest(new { success = false, message = "Rating must be between 0 and 5" });
        }

        var userId = _userManager.GetUserId(User)!;
        var success = await _musicService.SetUserSongRatingAsync(songId, request.Rating, userId);
        
        if (success)
        {
            return Json(new { success = true, rating = request.Rating });
        }
        
        return BadRequest(new { success = false, message = "Failed to update rating" });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public record RatingRequest(decimal Rating);
