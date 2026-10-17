using Godot;
using System;
using SlayTheSpireMapFog.Core.Options;

namespace SlayTheSpireMapFog.Godot.UI;

public static class MapFogSettingsFactory
{
    public static void HideModSettingsPanel(Node? modInfoContainer)
    {
        if (modInfoContainer == null || !GodotObject.IsInstanceValid(modInfoContainer)) return;
        var existing = modInfoContainer.GetNodeOrNull<Control>("MapFogModSettingsPanel");
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            existing.Visible = false;
        }
    }

    public static void AttachOrUpdateModSettingsPanel(
        Node? modInfoContainer,
        MapFogConfig config,
        Action onConfigSaved)
    {
        if (modInfoContainer == null || !GodotObject.IsInstanceValid(modInfoContainer) || config == null) return;

        var descNode = modInfoContainer.GetNodeOrNull<Control>("ModDescription");
        if (descNode != null && GodotObject.IsInstanceValid(descNode))
        {
            descNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        var panel = modInfoContainer.GetNodeOrNull<PanelContainer>("MapFogModSettingsPanel");
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("MapFogModSettingsPanel", new Vector2(22, 615), new Color(0.55f, 0.78f, 0.95f, 0.75f));
            panel.CustomMinimumSize = new Vector2(618, 0);
            panel.MouseFilter = Control.MouseFilterEnum.Pass;
            panel.ZIndex = 20;
            modInfoContainer.AddChild(panel);
        }

        panel.Visible = true;
        foreach (Node child in panel.GetChildren())
        {
            panel.RemoveChild(child);
            child.QueueFree();
        }

        var rootVBox = new VBoxContainer { Name = "SettingsVBox", MouseFilter = Control.MouseFilterEnum.Pass };
        rootVBox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(rootVBox);

        var titleLbl = new Label
        {
            Text = "FOG OF WAR SETTINGS",
            SelfModulate = new Color(0.75f, 0.88f, 0.98f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titleLbl.AddThemeFontSizeOverride("font_size", 14);
        rootVBox.AddChild(titleLbl);

        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Pass };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        rootVBox.AddChild(grid);

        void Rebuild() => AttachOrUpdateModSettingsPanel(modInfoContainer, config, onConfigSaved);

        grid.AddChild(CreateSettingsButton($"Fog of War: {(config.Enabled ? "ON" : "OFF")}", config.Enabled, false, () =>
        {
            config.Enabled = !config.Enabled;
            onConfigSaved();
            Rebuild();
        }));

        string modeLabel = config.Mode == FogMode.FullFog ? "Full Fog" : "Hidden Icons (?)";
        grid.AddChild(CreateSettingsButton($"Fog Mode: {modeLabel}", isActive: true, isGoldAccent: true, () =>
        {
            config.Mode = config.Mode == FogMode.FullFog ? FogMode.HiddenIcons : FogMode.FullFog;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Act Boss: {(config.ShowBoss ? "Visible" : "Hidden")}", config.ShowBoss, false, () =>
        {
            config.ShowBoss = !config.ShowBoss;
            onConfigSaved();
            Rebuild();
        }));

        int depth = Math.Clamp(config.VisionDepth, 1, 3);
        string stepWord = depth == 1 ? "1 Step Ahead" : $"{depth} Steps Ahead";
        grid.AddChild(CreateSettingsButton($"Vision: {stepWord}", isActive: true, isGoldAccent: true, () =>
        {
            config.VisionDepth = depth >= 3 ? 1 : depth + 1;
            onConfigSaved();
            Rebuild();
        }));
    }

    private static PanelContainer CreatePanel(string name, Vector2 pos, Color borderColor)
    {
        var panel = new PanelContainer
        {
            Name = name,
            Position = pos,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 50
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.12f, 0.92f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            BorderWidthLeft = 2,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = borderColor,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Button CreateSettingsButton(string text, bool isActive, bool isGoldAccent, Action onPressed)
    {
        Color accent = isGoldAccent
            ? new Color(0.95f, 0.82f, 0.35f, 0.85f)
            : (isActive ? new Color(0.42f, 0.88f, 0.55f, 0.85f) : new Color(0.50f, 0.52f, 0.58f, 0.55f));

        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(294, 34),
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None
        };
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.AddThemeColorOverride("font_color", isGoldAccent
            ? new Color(0.98f, 0.90f, 0.45f)
            : (isActive ? new Color(0.88f, 0.98f, 0.90f) : new Color(0.68f, 0.70f, 0.75f)));

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.15f, 0.20f, 0.95f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderWidthLeft = 2,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = accent,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.20f, 0.23f, 0.30f, 0.98f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderWidthLeft = 2,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(Math.Min(1f, accent.R + 0.15f), Math.Min(1f, accent.G + 0.15f), Math.Min(1f, accent.B + 0.15f), 1.0f),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        btn.AddThemeStyleboxOverride("normal", normalStyle);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", hoverStyle);
        btn.Pressed += onPressed;
        return btn;
    }
}
