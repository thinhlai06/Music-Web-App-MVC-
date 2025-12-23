using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MusicWeb.Models.Entities;
using MusicWeb.Services;

namespace MusicWeb.Controllers;

[Authorize]
public class FollowController : Controller
{
    private readonly IMusicService _musicService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FollowController(IMusicService musicService, UserManager<ApplicationUser> userManager)
    {
        _musicService = musicService;
        _userManager = userManager;
    }

    [HttpPost("/follow/{userId}")]
    public async Task<IActionResult> Follow(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var result = await _musicService.FollowUserAsync(currentUser.Id, userId);
        return Json(new { success = result });
    }

    [HttpPost("/unfollow/{userId}")]
    public async Task<IActionResult> Unfollow(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var result = await _musicService.UnfollowUserAsync(currentUser.Id, userId);
        return Json(new { success = result });
    }

    [HttpGet("/follow/list/followers/{userId}")]
    public async Task<IActionResult> GetFollowers(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var list = await _musicService.GetFollowersAsync(userId, currentUser?.Id);
        return Json(new { data = list });
    }

    [HttpGet("/follow/list/following/{userId}")]
    public async Task<IActionResult> GetFollowing(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var list = await _musicService.GetFollowingAsync(userId, currentUser?.Id);
        return Json(new { data = list });
    }

    [HttpPost("/follow/remove/{followerId}")]
    public async Task<IActionResult> RemoveFollower(string followerId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        var result = await _musicService.RemoveFollowerAsync(currentUser.Id, followerId);
        return Json(new { success = result });
    }
}
