using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace SlayTheSpireOverlay.Godot;

// Counter badges are separate patches so they cannot disturb the tier-badge patches in ModEntry.
[HarmonyPatch(typeof(NRelic), "_Ready")]
public static class NRelicCounterReadyPatch
{
    public static void Postfix(NRelic __instance) => Apply(__instance);

    internal static void Apply(NRelic relic)
    {
        try
        {
            if (relic == null || !GodotObject.IsInstanceValid(relic)) return;
            UI.RelicCounterBadgeFactory.Refresh(relic, relic.Model, ModEntry.Config.EnableRelicProcCounters, Sts2RuntimeInspector.IsItemInTopBar(relic));
        }
        catch (System.Exception ex)
        {
            if (!ex.Message.Contains("accessed before it was set", System.StringComparison.OrdinalIgnoreCase))
                GD.PrintErr($"[STS2 Overlay] Error in relic counter patch: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NRelic), "set_Model")]
public static class NRelicCounterSetModelPatch
{
    public static void Postfix(NRelic __instance) => NRelicCounterReadyPatch.Apply(__instance);
}

[HarmonyPatch(typeof(NRelic), "Reload")]
public static class NRelicCounterReloadPatch
{
    public static void Postfix(NRelic __instance) => NRelicCounterReadyPatch.Apply(__instance);
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.RelicModel), "InvokeDisplayAmountChanged")]
public static class RelicDisplayAmountChangedPatch
{
    public static void Postfix(object __instance) =>
        UI.RelicCounterBadgeFactory.RefreshForModel(__instance, ModEntry.Config.EnableRelicProcCounters);
}
