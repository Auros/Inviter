using Inviter.Server.Authorization;
using Inviter.Server.Models;
using Inviter.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using System.Security.Claims;

namespace Inviter.Server.Controllers;

[ApiController]
[Route("api/login")]
public class LoginController : ControllerBase
{
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly InviterContext _inviterContext;
    private readonly ISoriginService _soriginService;
    private readonly InviterSettings _inviterSettings;

    public LoginController(IClock clock, ILogger<LoginController> logger, InviterContext inviterContext, ISoriginService soriginService, InviterSettings inviterSettings)
    {
        _clock = clock;
        _logger = logger;
        _inviterContext = inviterContext;
        _inviterSettings = inviterSettings;
        _soriginService = soriginService;
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginBody body)
    {
        SoriginUser? soriginUser = await _soriginService.GetSoriginUser(body.Token);
        if (soriginUser is null)
        {
            _logger.LogError("Could not log in user via Sorigin.");
            return BadRequest();
        }

        User? user = await _inviterContext.Users.FirstOrDefaultAsync(u => u.ID == soriginUser.ID);
        string profilePicture = _inviterSettings.SoriginURL + soriginUser.ProfilePicture;
        if (user is null)
        {
            user = new()
            {
                ID = soriginUser.ID,
                Country = soriginUser.Country,
                Username = soriginUser.Username,
                ProfilePicture = profilePicture,
                LastSeen = _clock.GetCurrentInstant(),
            };
            _inviterContext.Users.Add(user);
            await _inviterContext.SaveChangesAsync();
        }
        else if (user.Username != soriginUser.Username || user.ProfilePicture != profilePicture || user.Country != soriginUser.Country)
        {
            user.Username = soriginUser.Username;
            user.ProfilePicture = profilePicture;
            user.Country = soriginUser.Country;
        }

        user.LastSeen = _clock.GetCurrentInstant();
        await _inviterContext.SaveChangesAsync();

        List<Claim> claims = new()
        {
            new(InviterExtensions.UserID, user.ID.ToString()),
            new(InviterExtensions.StatePrefix, ((int)user.State).ToString())
        };
        ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        AuthenticationProperties properties = new() { IsPersistent = true, ExpiresUtc = DateTime.UtcNow.AddHours(4), AllowRefresh = true };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new(identity), properties);
        return NoContent();
    }

    [HttpGet("@me")]
    [InviterAuthorize]
    public async Task<IActionResult> GetSelf()
    {
        ulong id = HttpContext.GetInviterID();
        User? user = await _inviterContext.Users.FirstOrDefaultAsync(u => u.ID == id);

        if (user is null)
            return NotFound();

        user.LastSeen = _clock.GetCurrentInstant();
        await _inviterContext.SaveChangesAsync();
        return Ok(user);
    }

    public class LoginBody
    {
        public string Token { get; set; } = null!;
    }
}
