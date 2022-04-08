using Inviter.Server.Models;
using Inviter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using System.Net.WebSockets;
using System.Text;

namespace Inviter.Server.Controllers;

[ApiController, Route("api/socket")]
public class SocketController : ControllerBase
{
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly PlayerService _playerService;
    private readonly ISoriginService _soriginService;
    private readonly InviterSettings _inviterSettings;
    private readonly IDbContextFactory<InviterContext> _inviterContextFactory;

    public SocketController(IClock clock, ILogger<SocketController> logger, PlayerService playerService, ISoriginService soriginService, InviterSettings inviterSettings, IDbContextFactory<InviterContext> inviterContextFactory)
    {
        _clock = clock;
        _logger = logger;
        _playerService = playerService;
        _soriginService = soriginService;
        _inviterSettings = inviterSettings;
        _inviterContextFactory = inviterContextFactory;
    }

    [HttpGet]
    public async Task Connect()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        // Accept the connection
        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        byte[] buffer = new byte[1024 * 4];

        // Cancel the connection if they don't do anything after 10 seconds. They should auth within this period of time.
        CancellationTokenSource timeout = new();
        timeout.CancelAfter(10_000);

        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        if (result is null)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Did not receive anything in the alloted amount of time. Terminating connection.", default);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        timeout.Dispose();

        // The first message they send should be the token by itself.
        ArraySegment<byte> seg = new(buffer, 0, result.Count);
        var token = Encoding.UTF8.GetString(seg);

        // Then process auth in the controller.
        SoriginUser? soriginUser = await _soriginService.GetSoriginUser(token);
        if (soriginUser is null)
        {
            const string errorMessage = "Could not log in user via Sorigin.";
            _logger.LogError(errorMessage);
            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, errorMessage, default);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var inviterContext = await _inviterContextFactory.CreateDbContextAsync(default);
        User? user = await inviterContext.Users.FirstOrDefaultAsync(u => u.ID == soriginUser.ID);
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
            inviterContext.Users.Add(user);
            await inviterContext.SaveChangesAsync();
        }
        else if (user.Username != soriginUser.Username || user.ProfilePicture != profilePicture || user.Country != soriginUser.Country)
        {
            user.Username = soriginUser.Username;
            user.ProfilePicture = profilePicture;
            user.Country = soriginUser.Country;
        }
        user.LastSeen = _clock.GetCurrentInstant();
        await inviterContext.SaveChangesAsync();
        await inviterContext.DisposeAsync();

        TaskCompletionSource<object> source = new();
        void Finished() => source.SetResult(new());

        PlayerInfo playerInfo = new(user, webSocket, Finished, _clock.GetCurrentInstant());
        _playerService.AddPlayer(playerInfo);

        await source.Task;
        _playerService.RemovePlayer(playerInfo);
    }
}