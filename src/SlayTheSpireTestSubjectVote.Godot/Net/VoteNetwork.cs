using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireTestSubjectVote.Godot.Net;

/// <summary>
/// Sends and receives votes over the game's own net service. Every client applies the same broadcast votes
/// to its own <see cref="VoteBook"/>, so all clients reach the same outcome without a host deciding.
/// </summary>
public sealed class VoteNetwork
{
    private readonly MessageHandlerDelegate<VoteCastMessage> _onVote;
    private readonly MessageHandlerDelegate<ModHelloMessage> _onHello;
    private readonly Action<ulong> _onLeft;
    private INetGameService? _service;
    private MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.RunLobby? _lobby;

    public VoteNetwork()
    {
        _onVote = OnVote;
        _onHello = OnHello;
        _onLeft = playerId => Roster.MarkLeft(playerId);
    }

    public static VoteNetwork Instance { get; } = new();

    public PeerRoster Roster { get; private set; } = new();

    public VoteBook Book { get; private set; } = new();

    /// <summary>Raised after a vote for an open or buffered play was accepted (key, voter, allow).</summary>
    public event Action<PlayKey, ulong, bool>? VoteReceived;

    /// <summary>Starts a fresh run: new roster and book, handlers registered, hello announced.</summary>
    public void Attach(INetGameService service)
    {
        Detach();
        _service = service;
        Roster = new PeerRoster();
        Book = new VoteBook();
        Roster.MarkHasMod(service.NetId);
        service.RegisterMessageHandler(_onVote);
        service.RegisterMessageHandler(_onHello);
        if (RunManager.Instance.RunLobby is { } lobby)
        {
            _lobby = lobby;
            lobby.RemotePlayerDisconnected += _onLeft;
        }
        service.SendMessage(new ModHelloMessage { PlayerId = service.NetId });
    }

    public void Detach()
    {
        if (_service == null) return;
        _service.UnregisterMessageHandler(_onVote);
        _service.UnregisterMessageHandler(_onHello);
        if (_lobby != null) _lobby.RemotePlayerDisconnected -= _onLeft;
        _lobby = null;
        _service = null;
    }

    /// <summary>Applies the local player's vote at once and tells everyone else.</summary>
    public void SendVote(PlayKey key, ulong voterId, bool allow)
    {
        if (Book.Cast(key, voterId, allow)) VoteReceived?.Invoke(key, voterId, allow);
        _service?.SendMessage(new VoteCastMessage
        {
            PlayerId = key.PlayerId,
            CombatCardIndex = key.CombatCardIndex,
            Attempt = key.Attempt,
            VoterId = voterId,
            Allow = allow,
        });
    }

    private void OnVote(VoteCastMessage message, ulong senderId)
    {
        var key = new PlayKey(message.PlayerId, message.CombatCardIndex, message.Attempt);
        if (Book.Cast(key, message.VoterId, message.Allow)) VoteReceived?.Invoke(key, message.VoterId, message.Allow);
    }

    private void OnHello(ModHelloMessage message, ulong senderId)
    {
        Roster.MarkHasMod(message.PlayerId);
        GD.Print($"[STS2 TestSubjectVote] player {message.PlayerId} has the mod");
    }
}
