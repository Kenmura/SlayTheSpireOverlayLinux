using Godot;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireTestSubjectVote.Godot.Net;

/// <summary>
/// Sends and receives votes over the game's own net service. Every client applies the same broadcast votes
/// to its own <see cref="VoteBook"/>, so all clients reach the same outcome without a host deciding.
/// Handlers are registered before the run launches (see RunLaunchPatch), because the game releases buffered
/// messages during launch and anything without a handler by then is dropped.
/// </summary>
public sealed class VoteNetwork
{
    private const string LogPrefix = "[STS2 TestSubjectVote]";

    private readonly MessageHandlerDelegate<VoteCastMessage> _onVote;
    private readonly MessageHandlerDelegate<VoteSkipMessage> _onSkip;
    private readonly MessageHandlerDelegate<ModHelloMessage> _onHello;
    private readonly Action<ulong> _onLeft;
    private readonly Action<RunLobbyPlayer> _onRejoined;
    private INetGameService? _service;
    private RunLobby? _lobby;

    public VoteNetwork()
    {
        _onVote = OnVote;
        _onSkip = OnSkip;
        _onHello = OnHello;
        _onLeft = playerId => Roster.MarkLeft(playerId);
        _onRejoined = OnRejoined;
    }

    public static VoteNetwork Instance { get; } = new();

    public PeerRoster Roster { get; private set; } = new();

    public VoteBook Book { get; private set; } = new();

    public bool IsHost => _service?.Type == NetGameType.Host;

    /// <summary>Starts a fresh run: new roster and book, handlers registered, hello announced.</summary>
    public void Attach(INetGameService service)
    {
        Detach();
        _service = service;
        Roster = new PeerRoster();
        Book = new VoteBook();
        Roster.MarkHasMod(service.NetId);
        if (IsHost) Roster.MarkHost(service.NetId);
        service.RegisterMessageHandler(_onVote);
        service.RegisterMessageHandler(_onSkip);
        service.RegisterMessageHandler(_onHello);
        if (RunManager.Instance.RunLobby is { } lobby)
        {
            _lobby = lobby;
            lobby.RemotePlayerDisconnected += _onLeft;
            lobby.PlayerRejoined += _onRejoined;
        }
        SendHello(isReply: false);
    }

    public void Detach()
    {
        if (_service == null) return;
        try
        {
            _service.UnregisterMessageHandler(_onVote);
            _service.UnregisterMessageHandler(_onSkip);
            _service.UnregisterMessageHandler(_onHello);
            if (_lobby != null)
            {
                _lobby.RemotePlayerDisconnected -= _onLeft;
                _lobby.PlayerRejoined -= _onRejoined;
            }
        }
        catch (Exception ex)
        {
            // The service can already be disposed when the run is torn down; nothing is left to unregister from.
            GD.Print($"{LogPrefix} detach skipped: {ex.Message}");
        }
        _lobby = null;
        _service = null;
    }

    /// <summary>Applies the local player's vote at once and tells everyone else.</summary>
    public void SendVote(PlayKey key, ulong voterId, bool allow)
    {
        Book.Cast(key, voterId, allow);
        _service?.SendMessage(new VoteCastMessage { PlayerId = key.PlayerId, ActionId = key.ActionId, VoterId = voterId, Allow = allow });
    }

    /// <summary>Host only: lets the stuck vote through here and on every other client.</summary>
    public void SendSkip(PlayKey key)
    {
        if (!IsHost) return;
        Book.ForceAllow(key);
        _service?.SendMessage(new VoteSkipMessage { PlayerId = key.PlayerId, ActionId = key.ActionId });
    }

    private void SendHello(bool isReply) =>
        _service?.SendMessage(new ModHelloMessage { PlayerId = _service.NetId, IsHost = IsHost, IsReply = isReply });

    private void OnVote(VoteCastMessage message, ulong senderId)
    {
        if (message.VoterId != senderId)
        {
            GD.PrintErr($"{LogPrefix} ignored a vote cast for player {message.VoterId} by player {senderId}");
            return;
        }
        Book.Cast(new PlayKey(message.PlayerId, message.ActionId), message.VoterId, message.Allow);
    }

    private void OnSkip(VoteSkipMessage message, ulong senderId)
    {
        if (!Roster.IsHost(senderId))
        {
            GD.PrintErr($"{LogPrefix} ignored a vote skip from non-host player {senderId}");
            return;
        }
        Book.ForceAllow(new PlayKey(message.PlayerId, message.ActionId));
    }

    private void OnHello(ModHelloMessage message, ulong senderId)
    {
        if (message.PlayerId != senderId || senderId == _service?.NetId) return;
        if (!ModProtocol.IsCompatible(message.ProtocolVersion))
        {
            GD.PrintErr($"{LogPrefix} player {senderId} runs mod protocol v{message.ProtocolVersion}, this is v{ModProtocol.Version}: votes are disabled until it matches");
            return;
        }
        Roster.MarkHasMod(message.PlayerId);
        Roster.MarkRejoined(message.PlayerId);
        if (message.IsHost) Roster.MarkHost(message.PlayerId);
        GD.Print($"{LogPrefix} player {message.PlayerId} has the mod");
        if (!message.IsReply) SendHello(isReply: true);
    }

    private void OnRejoined(RunLobbyPlayer player)
    {
        Roster.MarkRejoined(player.id);
        SendHello(isReply: false);
    }
}
