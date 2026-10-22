using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

// Reference-only stand-ins for the game types the Test Subject Vote mod compiles against. Shapes mirror what the
// mod uses; they never run (CI only needs the mod to compile and its architecture to be checked).

namespace MegaCrit.Sts2.Core.Logging
{
    public enum LogLevel { Debug, Info, Warn, Error }
}

namespace MegaCrit.Sts2.Core.Entities.Cards
{
    public enum CardType { Attack, Skill, Power, Status, Curse }

    public enum PileType { None, Draw, Hand, Discard, Exhaust }

    public enum CardPreviewMode { Normal }
}

namespace MegaCrit.Sts2.Core.Entities.Actions
{
    public readonly struct NetCombatCard
    {
        public Models.CardModel? ToCardModelOrNull() => null;
    }
}

namespace MegaCrit.Sts2.Core.Entities.Players
{
    public class Player
    {
        public Creatures.Creature Creature { get; } = null!;
        public ulong NetId { get; }
    }
}

namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    public class Creature
    {
        public Models.MonsterModel? Monster { get; }
        public Combat.ICombatState? CombatState { get; }
        public bool IsAlive { get; }
        public string Name { get; } = string.Empty;
        public bool HasPower<T>() where T : Models.PowerModel => false;
        public T? GetPower<T>() where T : Models.PowerModel => null;
    }
}

namespace MegaCrit.Sts2.Core.Entities.Multiplayer
{
    public class RunLobbyPlayer
    {
        public ulong id;
    }
}

namespace MegaCrit.Sts2.Core.Combat
{
    public interface ICombatState
    {
        IReadOnlyList<Entities.Creatures.Creature> Enemies { get; }
        IReadOnlyList<Entities.Players.Player> Players { get; }
    }
}

namespace MegaCrit.Sts2.Core.Models.Monsters
{
    public sealed class TestSubject : MonsterModel { }
}

namespace MegaCrit.Sts2.Core.Models.Powers
{
    public sealed class AdaptablePower : PowerModel { }
    public sealed class PainfulStabsPower : PowerModel { }
    public sealed class NemesisPower : PowerModel { }
    public sealed class EnragePower : PowerModel { }
}

namespace MegaCrit.Sts2.Core.Multiplayer.Serialization
{
    public class PacketWriter
    {
        public void WriteULong(ulong value) { }
        public void WriteUInt(uint value) { }
        public void WriteInt(int value, int bits) { }
        public void WriteBool(bool value) { }
    }

    public class PacketReader
    {
        public ulong ReadULong() => 0;
        public uint ReadUInt() => 0;
        public int ReadInt(int bits) => 0;
        public bool ReadBool() => false;
    }
}

namespace MegaCrit.Sts2.Core.Multiplayer.Transport
{
    public enum NetTransferMode { Reliable, Unreliable }

    public interface INetMessage
    {
        bool ShouldBroadcast { get; }
        NetTransferMode Mode { get; }
        Logging.LogLevel LogLevel { get; }
        bool ShouldBuffer { get; }
        void Serialize(Serialization.PacketWriter writer);
        void Deserialize(Serialization.PacketReader reader);
    }
}

namespace MegaCrit.Sts2.Core.Multiplayer.Game
{
    public enum NetGameType { None, Singleplayer, Host, Client }

    public static class NetGameTypeExtensions
    {
        public static bool IsMultiplayer(this NetGameType type) => type == NetGameType.Host || type == NetGameType.Client;
    }

    public delegate void MessageHandlerDelegate<T>(T message, ulong senderId) where T : Transport.INetMessage;

    public interface INetGameService
    {
        NetGameType Type { get; }
        ulong NetId { get; }
        void SendMessage<T>(T message) where T : Transport.INetMessage;
        void RegisterMessageHandler<T>(MessageHandlerDelegate<T> handler) where T : Transport.INetMessage;
        void UnregisterMessageHandler<T>(MessageHandlerDelegate<T> handler) where T : Transport.INetMessage;
    }
}

namespace MegaCrit.Sts2.Core.Multiplayer.Game.Lobby
{
    public class RunLobby
    {
        public event Action<ulong>? RemotePlayerDisconnected;
        public event Action<Entities.Multiplayer.RunLobbyPlayer>? PlayerRejoined;
    }
}

namespace MegaCrit.Sts2.Core.Runs
{
    public class RunManager
    {
        public static RunManager Instance { get; } = new();
        public Multiplayer.Game.INetGameService NetService { get; } = null!;
        public Multiplayer.Game.Lobby.RunLobby? RunLobby { get; }
        public void Launch() { }
    }
}

namespace MegaCrit.Sts2.Core.Context
{
    public static class LocalContext
    {
        public static bool IsMe(Entities.Players.Player player) => false;
    }
}

namespace MegaCrit.Sts2.Core.GameActions
{
    public enum GameActionState { None, Executing, Canceled, Finished }

    public class PlayCardAction
    {
        public Entities.Actions.NetCombatCard NetCombatCard { get; }
        public Entities.Players.Player Player { get; } = null!;
        public uint? Id { get; }
        public GameActionState State { get; }
        protected Task ExecuteAction() => Task.CompletedTask;
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Pooling
{
    public static class NodePool
    {
        public static void Free(Cards.NCard card) { }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Combat
{
    public class NCardPlayQueue
    {
        public static NCardPlayQueue? Instance { get; }
        public void RemoveCardFromQueueForCancellation(GameActions.PlayCardAction action) { }
    }

    public class NPlayerHand
    {
        public static NPlayerHand? Instance { get; }
        public void TryCancelCardPlay(Models.CardModel card) { }
    }
}
