using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpireTestSubjectVote.Godot.Game;
using SlayTheSpireTestSubjectVote.Godot.Net;

namespace SlayTheSpireTestSubjectVote.Godot.Patches;

/// <summary>Replaces the task PlayCardAction.ExecuteAction returns with the vote gate when a vote is needed.</summary>
[HarmonyPatch(typeof(PlayCardAction), "ExecuteAction")]
internal static class PlayCardGatePatch
{
    internal static bool Prefix(PlayCardAction __instance, ref Task __result)
    {
        if (VoteGate.ConsumeApproval(__instance)) return true;
        if (!VoteGate.TryBuildHold(__instance, out var held)) return true;
        __result = VoteGate.RunAsync(__instance, held);
        return false;
    }
}

/// <summary>Registers the vote handlers and announces the mod to the other players when a multiplayer run starts.</summary>
[HarmonyPatch(typeof(NRun), "_Ready")]
internal static class RunStartPatch
{
    internal static void Postfix()
    {
        if (TestSubjectState.IsMultiplayer()) VoteNetwork.Instance.Attach(RunManager.Instance.NetService);
    }
}
