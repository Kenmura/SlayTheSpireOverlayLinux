using Godot;
using HarmonyLib;
using SlayTheSpireMod.Common;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Godot;

/// <summary>Reads the local player's potion reward odds. Returns null when anything is missing (never throws).</summary>
public static class PotionOddsReader
{
    public static float? ReadCurrentOdds()
    {
        try
        {
            object? player = Sts2RuntimeInspector.GetLocalPlayer();
            object? odds = ReflectionAccess.GetMember(ReflectionAccess.GetMember(player, "PlayerOdds"), "PotionReward");
            return ReflectionAccess.GetMember(odds, "CurrentValue") as float?;
        }
        catch
        {
            return null;
        }
    }
}

// String-based targets keep the patch resilient to game versions; each is also listed in tools/Sts2CompatCheck/patch-targets.txt.
[HarmonyPatch("MegaCrit.Sts2.Core.Nodes.Screens.NRewardsScreen", "_Ready")]
public static class NRewardsScreenPotionChanceReadyPatch
{
    public const string LabelName = "PotionDropChanceLabel";

    public static void Postfix(Node __instance) => Apply(__instance);

    internal static void Apply(Node? screen)
    {
        try
        {
            if (screen == null || !GodotObject.IsInstanceValid(screen)) return;
            string? text = ModEntry.Config.EnablePotionDropChance ? PotionDropRules.FormatLabel(PotionOddsReader.ReadCurrentOdds()) : null;
            var label = screen.GetNodeOrNull<Label>(LabelName);
            if (text == null)
            {
                if (label != null && GodotObject.IsInstanceValid(label)) label.Visible = false;
                return;
            }
            if (label == null || !GodotObject.IsInstanceValid(label))
            {
                label = new Label { Name = LabelName, MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeFontSizeOverride("font_size", 18);
                label.SelfModulate = new Color(0.75f, 0.9f, 1.0f, 0.9f);
                screen.AddChild(label);
                // Full-width strip with centred text, so placement does not depend on the text width at layout time.
                label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
                label.OffsetLeft = 0;
                label.OffsetRight = 0;
                label.OffsetTop = 64;
                label.OffsetBottom = 96;
            }
            label.Text = text;
            label.Visible = true;
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error in potion drop chance patch: {ex.Message}");
        }
    }
}
