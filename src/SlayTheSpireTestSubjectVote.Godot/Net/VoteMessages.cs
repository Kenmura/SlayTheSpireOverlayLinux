using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace SlayTheSpireTestSubjectVote.Godot.Net;

/// <summary>One voter's answer for one held play. Echoed by the host to every other client.</summary>
public class VoteCastMessage : INetMessage
{
    public ulong PlayerId { get; set; }

    public uint CombatCardIndex { get; set; }

    public int Attempt { get; set; }

    public ulong VoterId { get; set; }

    public bool Allow { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(PlayerId);
        writer.WriteUInt(CombatCardIndex);
        writer.WriteInt(Attempt);
        writer.WriteULong(VoterId);
        writer.WriteBool(Allow);
    }

    public void Deserialize(PacketReader reader)
    {
        PlayerId = reader.ReadULong();
        CombatCardIndex = reader.ReadUInt();
        Attempt = reader.ReadInt();
        VoterId = reader.ReadULong();
        Allow = reader.ReadBool();
    }
}

/// <summary>Sent once per run by every client so the others know this player has the mod.</summary>
public class ModHelloMessage : INetMessage
{
    public ulong PlayerId { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public bool ShouldBuffer => true;

    public void Serialize(PacketWriter writer) => writer.WriteULong(PlayerId);

    public void Deserialize(PacketReader reader) => PlayerId = reader.ReadULong();
}
