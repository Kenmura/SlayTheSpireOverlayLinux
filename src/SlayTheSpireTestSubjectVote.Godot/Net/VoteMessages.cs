using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace SlayTheSpireTestSubjectVote.Godot.Net;

/// <summary>One voter's answer for one held play. Echoed by the host to every other client.</summary>
public class VoteCastMessage : INetMessage
{
    public ulong PlayerId { get; set; }

    /// <summary>The game's synchronized action id of the held PlayCardAction.</summary>
    public uint ActionId { get; set; }

    public ulong VoterId { get; set; }

    public bool Allow { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(PlayerId);
        writer.WriteUInt(ActionId);
        writer.WriteULong(VoterId);
        writer.WriteBool(Allow);
    }

    public void Deserialize(PacketReader reader)
    {
        PlayerId = reader.ReadULong();
        ActionId = reader.ReadUInt();
        VoterId = reader.ReadULong();
        Allow = reader.ReadBool();
    }
}

/// <summary>The host's escape hatch: lets one stuck vote through on every client.</summary>
public class VoteSkipMessage : INetMessage
{
    public ulong PlayerId { get; set; }

    public uint ActionId { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(PlayerId);
        writer.WriteUInt(ActionId);
    }

    public void Deserialize(PacketReader reader)
    {
        PlayerId = reader.ReadULong();
        ActionId = reader.ReadUInt();
    }
}

/// <summary>
/// Announces that this player runs the mod. A hello that is not a reply is answered with a reply, so a player
/// who loads late or rejoins still learns about everyone (a reply is never answered, so there is no ping-pong).
/// </summary>
public class ModHelloMessage : INetMessage
{
    public ulong PlayerId { get; set; }

    public bool IsHost { get; set; }

    public bool IsReply { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(PlayerId);
        writer.WriteBool(IsHost);
        writer.WriteBool(IsReply);
    }

    public void Deserialize(PacketReader reader)
    {
        PlayerId = reader.ReadULong();
        IsHost = reader.ReadBool();
        IsReply = reader.ReadBool();
    }
}
