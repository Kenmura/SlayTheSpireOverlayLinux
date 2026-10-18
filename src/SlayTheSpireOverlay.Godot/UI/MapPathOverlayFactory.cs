using Godot;

namespace SlayTheSpireOverlay.Godot.UI;

public static class MapPathOverlayFactory
{
    public const string PillContainerName = "MapPathSummaryPill";
    public const string LabelName = "SummaryLabel";

    public static void ShowSummary(Node mapScreen, string text)
    {
        if (mapScreen == null || !GodotObject.IsInstanceValid(mapScreen)) return;

        var pill = GetOrCreatePill(mapScreen);
        var label = pill.GetNodeOrNull<Label>(LabelName);
        if (label != null && GodotObject.IsInstanceValid(label))
        {
            label.Text = text;
        }
        pill.Visible = true;
    }

    public static void HideSummary(Node mapScreen)
    {
        if (mapScreen == null || !GodotObject.IsInstanceValid(mapScreen)) return;
        var pill = mapScreen.GetNodeOrNull<Control>(PillContainerName);
        if (pill != null && GodotObject.IsInstanceValid(pill))
        {
            pill.Visible = false;
        }
    }

    private static Control GetOrCreatePill(Node mapScreen)
    {
        var existing = mapScreen.GetNodeOrNull<PanelContainer>(PillContainerName);
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            return existing;
        }

        var pill = new PanelContainer
        {
            Name = PillContainerName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.12f, 0.88f),
            BorderColor = new Color(0.35f, 0.5f, 0.7f, 0.65f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        pill.AddThemeStyleboxOverride("panel", style);

        pill.AnchorLeft = 0.5f;
        pill.AnchorRight = 0.5f;
        pill.AnchorTop = 0f;
        pill.AnchorBottom = 0f;
        pill.OffsetLeft = -290;
        pill.OffsetRight = 290;
        pill.OffsetTop = 68;
        pill.OffsetBottom = 104;

        var label = new Label
        {
            Name = LabelName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.SelfModulate = new Color(0.92f, 0.94f, 0.98f, 0.95f);
        pill.AddChild(label);

        mapScreen.AddChild(pill);
        return pill;
    }
}
