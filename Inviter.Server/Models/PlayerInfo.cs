using NodaTime;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Inviter.Server.Models;

public class PlayerInfo
{
    public User User { get; }

    private readonly Action _finisher;
    private readonly WebSocket _socket;

    private bool _enabled;
    private bool _polling;
    private readonly byte[] _buffer = new byte[1024 * 4];
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private Instant _time;
    private Instant _pollTime;

    public event Action<PlayerInfo>? WantsToDisconnect;
    public event Action<PlayerInfo, ulong, ulong[]>? InviteSent;
    public event Action<PlayerInfo, string, int>? FriendsListRequested;
    public event Action<PlayerInfo, Guid, InviteStatus>? InviteStatusReceived;
    public event Action<PlayerInfo, string, Guid, Uri, Uri, int?>? InviteJoinRequestReceived;

    public PlayerInfo(User user, WebSocket socket, Action finisher, Instant startTime)
    {
        User = user;
        _socket = socket;
        _finisher = finisher;
        _pollTime = startTime;
        _time = startTime;
    }

    public void Disconnect()
    {
        Disable();
        _cancellationTokenSource.Dispose();
        WantsToDisconnect?.Invoke(this);
        _finisher?.Invoke();
    }

    public void Enable()
    {
        _enabled = true;
    }

    public void Disable()
    {
        _enabled = false;
    }

    public void Poll(Instant timeOfPoll)
    {
        _pollTime = timeOfPoll;
        DisconnectIfInactive();
        if (!_polling && _enabled)
            _ = Task.Run(Update);
    }

    private async void Update()
    {
        _polling = true;
        DisconnectIfInactive();
        var result = await _socket.ReceiveAsync(new ArraySegment<byte>(_buffer), _cancellationTokenSource.Token);
        DisconnectIfInactive();
        _polling = false;

        if (result is null || !_enabled)
            return;

        ArraySegment<byte> seg = new(_buffer, 0, result.Count);

        try
        {
            JsonDocument doc = JsonDocument.Parse(Encoding.UTF8.GetString(seg));

            if (!doc.RootElement.TryGetProperty("type", out var type))
                return;

            var eInt = type.GetInt32();
            if (eInt < 2)
                eInt = 1;

            EventType eventType = (EventType)type.GetInt32();
            if (eventType is EventType.PlayerSentInvite)
            {
                var target = doc.RootElement.GetProperty("target").GetUInt64();
                var membersProperty = doc.RootElement.GetProperty("members");
                var memberCount = membersProperty.GetArrayLength();
                List<ulong> ids = new();
                for (int i = 0; i < memberCount; i++)
                {
                    var element = membersProperty[i];
                    ids.Add(element.GetUInt64());
                }
                InviteSent?.Invoke(this, target, ids.ToArray());
            }
            else if (eventType is EventType.PlayerReceivedInvite)
            {
                // Packet is dedicated to the client.
            }
            else if (eventType is EventType.PlayerReceivedFriendsList)
            {
                // Packet is dedicated to the client.
            }
            else if (eventType is EventType.PlayerSentFriendsListInfoRequest)
            {
                var page = doc.RootElement.GetProperty("page").GetInt32();
                var search = doc.RootElement.GetProperty("search").GetString();
                if (page < 0)
                    page = 0;

                FriendsListRequested?.Invoke(this, search ?? string.Empty, page);
            }
            else if (eventType is EventType.PlayerSubmittedInviteStatus)
            {
                var iInt = doc.RootElement.GetProperty("status").GetInt32();
                var invite = doc.RootElement.GetProperty("invite").GetString();

                if (iInt < 1 || iInt > 3)
                    return;
                if (!Guid.TryParse(invite, out var inviteId))
                    return;

                InviteStatusReceived?.Invoke(this, inviteId, (InviteStatus)iInt);
            }
            else if (eventType is EventType.Ping)
            {
                _time = _pollTime;
            }
            else if (eventType is EventType.PlayerSentJoinRequest)
            {
                var code = doc.RootElement.GetProperty("code").GetString();
                var inviteStr = doc.RootElement.GetProperty("invite").GetString();
                var endPointStr = doc.RootElement.GetProperty("endPoint").GetString();
                var statusUrlStr = doc.RootElement.GetProperty("statusUrl").GetString();

                int? maxPartySize = doc.RootElement.TryGetProperty("maxPartySize", out var maxPartyElement) ? maxPartyElement.GetInt32() : null;

                if (code is null || !Guid.TryParse(inviteStr, out Guid inviteId) || !Uri.TryCreate(endPointStr, UriKind.Absolute, out Uri? endPoint) || !Uri.TryCreate(statusUrlStr, UriKind.Absolute, out Uri? statusUrl))
                {
                    // Invalid join request, bad URLs
                    return;
                }

                InviteJoinRequestReceived?.Invoke(this, code, inviteId, endPoint, statusUrl, maxPartySize);
            }
        }
        catch
        {
            return;
        }
    }

    public Task SendInvite(Invite invite)
    {
        return Send(new { type = EventType.PlayerReceivedInvite, invite });
    }

    public Task SendFriendsList(List<User> friends)
    {
        return Send(new { type = EventType.PlayerReceivedFriendsList, friends });
    }

    public Task SendInviteAcceptance(Invite invite)
    {
        return Send(new { type = EventType.PlayerReceivedInviteAcceptance, invite });
    }

    public Task SendInviteDenial(Invite invite)
    {
        return Send(new { type = EventType.PlayerReceivedInviteDenial, invite });
    }

    public Task SendPlayerJoinInfo(string code, Uri endPoint, Uri statusUrl, int? maxPartySize)
    {
        return Send(new { type = EventType.PlayerReceivedJoinInfo, code, endPoint, statusUrl, maxPartySize });
    }

    private Task Send(object value)
    {
        var json = JsonSerializer.Serialize(value, options: InviterProtocol.JSON);
        return _socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, default);
    }

    private void DisconnectIfInactive()
    {
        if (_time + Duration.FromMinutes(2) > _pollTime)
            return;

        Disconnect();
    }
}