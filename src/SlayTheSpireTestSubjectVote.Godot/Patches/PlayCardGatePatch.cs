using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions;
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

/// <summary>
/// Registers the vote handlers and announces the mod to the other players, BEFORE the run launches.
/// RunManager.Launch stops message buffering and releases everything received while loading, so a handler
/// registered later (for example in NRun._Ready) would miss a slower-loading teammate's hello and the
/// players would disagree about whether to hold a play.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class RunLaunchPatch
{
    internal static void Prefix()
    {
        if (TestSubjectState.IsMultiplayer()) VoteNetwork.Instance.Attach(RunManager.Instance.NetService);
    }
}

/// <summary>Drops the handlers when the run is torn down so nothing from this run leaks into the next.</summary>
[HarmonyPatch(typeof(RunManager), "CleanUp")]
internal static class RunCleanUpPatch
{
    internal static void Prefix() => VoteNetwork.Instance.Detach();
}
