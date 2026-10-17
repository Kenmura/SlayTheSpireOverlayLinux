using Godot;
using System;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Options;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Godot.UI;

/// <summary>
/// Minimal, resolution-aware Godot 4 UI factory for Combat (Incoming Attack Summary + Draw Pile Tracker),
/// Merchant ("Gold Left After Purchase"), Event Commentary, and In-Game Mod Settings overlays.
/// Supports F8 (toggle overlay visibility) and F7 (collapse/expand deck tracker).
/// </summary>
public static class RoomOverlayFactory
{
    private static bool _overlayHiddenByHotkey;
    private static bool _deckTrackerCollapsed;
    private static bool _prevF8Down;
    private static bool _prevF7Down;
    private static bool _prevF6Down;
    private static bool _prevF9Down;
    private static ulong _lastPolledFrame;

    public static bool IsOverlayHidden
    {
        get => _overlayHiddenByHotkey;
        set => _overlayHiddenByHotkey = value;
    }

    public static bool IsDeckTrackerCollapsed
    {
        get => _deckTrackerCollapsed;
        set => _deckTrackerCollapsed = value;
    }

    public static void PollHotkeys(Action? onToggleAuthor = null, Action? onToggleVisibility = null, Action? onForceRefresh = null)
    {
        try
        {
            ulong frame = Engine.GetProcessFrames();
            if (frame != 0 && frame == _lastPolledFrame) return;
            _lastPolledFrame = frame;

            bool f8Down = Input.IsPhysicalKeyPressed(Key.F8);
            if (f8Down && !_prevF8Down)
            {
                _overlayHiddenByHotkey = !_overlayHiddenByHotkey;
                onToggleVisibility?.Invoke();
            }
            _prevF8Down = f8Down;

            bool f7Down = Input.IsPhysicalKeyPressed(Key.F7);
            if (f7Down && !_prevF7Down) _deckTrackerCollapsed = !_deckTrackerCollapsed;
            _prevF7Down = f7Down;

            bool f6Down = Input.IsPhysicalKeyPressed(Key.F6);
            if (f6Down && !_prevF6Down) onToggleAuthor?.Invoke();
            _prevF6Down = f6Down;

            bool f9Down = Input.IsPhysicalKeyPressed(Key.F9);
            if (f9Down && !_prevF9Down) onForceRefresh?.Invoke();
            _prevF9Down = f9Down;
        }
        catch
        {
            // Ignore outside active input frame
        }
    }

    public static void UpdateAttackSummary(Node combatRoom, AttackSummary summary, bool enabled)
    {
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;

        var panel = combatRoom.GetNodeOrNull<PanelContainer>("AttackSummaryPanel");
        if (!enabled || _overlayHiddenByHotkey || summary.TotalHits <= 0)
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(combatRoom);
        Vector2 targetPos = new Vector2(vp.X - 240, vp.Y * 0.20f);

        Label label;
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("AttackSummaryPanel", targetPos, new Color(0.95f, 0.35f, 0.35f, 0.7f));
            label = new Label
            {
                Name = "SummaryText",
                HorizontalAlignment = HorizontalAlignment.Right,
                SelfModulate = new Color(1.0f, 0.45f, 0.45f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            panel.AddChild(label);
            combatRoom.AddChild(panel);
        }
        else
        {
            panel.Position = targetPos;
            label = panel.GetNodeOrNull<Label>("SummaryText");
            if (label == null) return;
        }

        panel.Visible = true;
        string hitWord = summary.TotalHits == 1 ? "Hit" : "Hits";
        string foeSuffix = summary.AttackingEnemies > 1 ? $" · {summary.AttackingEnemies} Foes" : "";
        string netLine = summary.IsLethal
            ? $"⚠ LETHAL (-{summary.UnblockedDamage} HP)"
            : (summary.UnblockedDamage == 0 ? "Blocked (0 DMG)" : $"Unblocked: {summary.UnblockedDamage} DMG");

        Color accentColor = summary.IsLethal
            ? new Color(1.0f, 0.28f, 0.28f)
            : (summary.UnblockedDamage == 0 ? new Color(0.45f, 0.92f, 0.55f) : new Color(1.0f, 0.50f, 0.42f));

        label.SelfModulate = accentColor;
        if (panel.GetThemeStylebox("panel") is StyleBoxFlat sb)
        {
            sb.BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.78f);
        }
        label.Text = $"Incoming: {summary.TotalDamage} DMG ({summary.TotalHits} {hitWord}{foeSuffix})\n{netLine}";
    }

    public static void UpdateDeckTracker(Node combatRoom, DeckTrackerState? state, bool enabled, string author = "Baalorlord")
    {
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;

        var panel = combatRoom.GetNodeOrNull<PanelContainer>("DeckTrackerPanel");
        if (!enabled || _overlayHiddenByHotkey || state == null)
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(combatRoom);
        Vector2 targetPos = new Vector2(16, vp.Y * 0.15f);

        VBoxContainer vbox;
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("DeckTrackerPanel", targetPos, new Color(0.45f, 0.65f, 0.95f, 0.45f));
            vbox = new VBoxContainer { Name = "TrackerVBox", MouseFilter = Control.MouseFilterEnum.Ignore };
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);
            combatRoom.AddChild(panel);
        }
        else
        {
            panel.Position = targetPos;
            vbox = panel.GetNodeOrNull<VBoxContainer>("TrackerVBox");
            if (vbox == null) return;
        }

        panel.Visible = true;
        string sig = BuildDeckStateSignature(state, _deckTrackerCollapsed, author);
        if (panel.HasMeta("StateSig") && panel.GetMeta("StateSig").AsString() == sig)
        {
            return;
        }
        panel.SetMeta("StateSig", sig);

        foreach (Node child in vbox.GetChildren())
        {
            vbox.RemoveChild(child);
            child.QueueFree();
        }

        string toggleHint = _deckTrackerCollapsed ? "[F7 +]" : "[F7 -]";
        string reshuffleHint = state.DrawCount < 5 && state.DiscardCount > 0 ? " ↻" : "";
        AddTrackerLine(vbox, $"DRAW PILE ({state.DrawCount}{reshuffleHint}) [{author}] {toggleHint}", new Color(0.95f, 0.85f, 0.45f));

        foreach (var cat in state.Categories)
        {
            if (cat.Total == 0) continue;
            bool isDeadDrawCat = cat.Category == "Other";
            string catLabel = isDeadDrawCat ? "Status/Curse" : cat.Category;
            Color catColor = isDeadDrawCat && cat.InDraw > 0
                ? new Color(1.0f, 0.65f, 0.28f)
                : new Color(0.65f, 0.82f, 1.0f);

            AddTrackerLine(vbox, $"{catLabel}: {cat.InDraw}/{cat.Total} ({cat.ProbabilityPercent}%)", catColor);

            if (_deckTrackerCollapsed) continue;

            foreach (var card in cat.Cards)
            {
                string cost = card.CostsX ? "X" : (card.Cost?.ToString() ?? "-");
                Color cardColor = card.InDraw > 0
                    ? new Color(0.92f, 0.92f, 0.96f, 1.0f)
                    : new Color(0.92f, 0.92f, 0.96f, 0.42f);
                string oddsSuffix = card.NextHandPercent > 0 ? $" · {card.NextHandPercent}%" : "";
                AddTrackerLine(vbox, $" [{cost}] {card.Title} ({card.InDraw}/{card.Total}){oddsSuffix}", cardColor);
            }
        }

        AddTrackerLine(vbox, $"Hand: {state.HandCount} | Discard: {state.DiscardCount} | Exhaust: {state.ExhaustCount}", new Color(0.70f, 0.74f, 0.82f));
    }

    private static string BuildDeckStateSignature(DeckTrackerState state, bool collapsed, string author)
    {
        var sb = new System.Text.StringBuilder(128);
        sb.Append(collapsed ? '1' : '0').Append('|')
          .Append(author).Append('|')
          .Append(state.DrawCount).Append('|')
          .Append(state.HandCount).Append('|')
          .Append(state.DiscardCount).Append('|')
          .Append(state.ExhaustCount);
        foreach (var cat in state.Categories)
        {
            sb.Append(';').Append(cat.Category).Append(':').Append(cat.InDraw).Append('/').Append(cat.Total);
            if (!collapsed)
            {
                foreach (var c in cat.Cards)
                {
                    sb.Append(',').Append(c.Title).Append(':').Append(c.Cost).Append(':').Append(c.UpgradeLevel).Append(':').Append(c.InDraw).Append('/').Append(c.Total).Append(':').Append(c.NextHandPercent);
                }
            }
        }
        return sb.ToString();
    }

    public static void UpdateMerchantGoldLeft(Node merchantRoom, (int RemainingGold, int Cost)? goldLeft, bool enabled)
    {
        if (merchantRoom == null || !GodotObject.IsInstanceValid(merchantRoom)) return;

        var panel = merchantRoom.GetNodeOrNull<PanelContainer>("MerchantGoldLeftPanel");
        if (!enabled || _overlayHiddenByHotkey || goldLeft == null)
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(merchantRoom);
        Vector2 targetPos = new Vector2(40, vp.Y * 0.09f);

        Label label;
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("MerchantGoldLeftPanel", targetPos, new Color(0.95f, 0.78f, 0.25f, 0.8f));
            label = new Label
            {
                Name = "GoldLeftText",
                SelfModulate = new Color(1.0f, 0.85f, 0.3f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            panel.AddChild(label);
            merchantRoom.AddChild(panel);
        }
        else
        {
            panel.Position = targetPos;
            label = panel.GetNodeOrNull<Label>("GoldLeftText");
            if (label == null) return;
        }

        panel.Visible = true;
        int rem = goldLeft.Value.RemainingGold;
        int cost = goldLeft.Value.Cost;
        if (rem >= 0)
        {
            label.SelfModulate = new Color(1.0f, 0.85f, 0.3f);
            label.Text = $"Gold Left: {rem}g (-{cost}g)";
        }
        else
        {
            label.SelfModulate = new Color(1.0f, 0.48f, 0.42f);
            label.Text = $"Need {-rem}g more (Cost: {cost}g)";
        }
    }

    public static void AttachEventCommentary(Node eventRoom, ITierListProvider tierProvider, string? eventId, bool enabled, string author = "Baalorlord")
    {
        if (eventRoom == null || !GodotObject.IsInstanceValid(eventRoom)) return;
        if (!enabled || string.IsNullOrWhiteSpace(eventId))
        {
            var oldPanel = eventRoom.GetNodeOrNull<Control>("EventCommentaryPanel");
            if (oldPanel != null && GodotObject.IsInstanceValid(oldPanel)) oldPanel.Visible = false;
            return;
        }

        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var tierList = await tierProvider.GetTierListAsync().ConfigureAwait(false);
                string key = $"EVENT_{OverlayCalculators.NormalizeId(eventId)}";
                bool hasTip = tierList.TryGetValue(key, out var data) && !string.IsNullOrWhiteSpace(data?.Commentary);

                Callable.From(() =>
                {
                    if (!GodotObject.IsInstanceValid(eventRoom)) return;
                    var existing = eventRoom.GetNodeOrNull<PanelContainer>("EventCommentaryPanel");
                    if (existing != null && GodotObject.IsInstanceValid(existing))
                    {
                        eventRoom.RemoveChild(existing);
                        existing.QueueFree();
                    }

                    if (!hasTip || data == null) return;

                    string tipAuthor = string.IsNullOrWhiteSpace(data.Tier) || string.Equals(data.Tier, "Guide", StringComparison.OrdinalIgnoreCase)
                        ? author
                        : data.Tier;
                    Vector2 vp = GetViewportSize(eventRoom);
                    var panel = CreatePanel("EventCommentaryPanel", new Vector2(vp.X - 420, vp.Y * 0.12f), new Color(0.65f, 0.75f, 0.95f, 0.6f));
                    panel.Visible = !_overlayHiddenByHotkey;
                    var lbl = new Label
                    {
                        Text = $"Event Tip [{tipAuthor}]:\n{data.Commentary}",
                        AutowrapMode = TextServer.AutowrapMode.WordSmart,
                        CustomMinimumSize = new Vector2(380, 0),
                        SelfModulate = new Color(0.92f, 0.94f, 0.98f),
                        MouseFilter = Control.MouseFilterEnum.Ignore
                    };
                    lbl.AddThemeFontSizeOverride("font_size", 13);
                    panel.AddChild(lbl);
                    eventRoom.AddChild(panel);
                }).CallDeferred();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[STS2 Overlay] Error attaching event commentary: {ex.Message}");
            }
        });
    }

    public static void ShowAuthorToast(Node? sceneRoot, string message, bool forceVisible = false)
    {
        if (sceneRoot == null || !GodotObject.IsInstanceValid(sceneRoot)) return;

        Vector2 vp = GetViewportSize(sceneRoot);
        var panel = sceneRoot.GetNodeOrNull<PanelContainer>("OverlayAuthorToast");
        Label? label;
        global::Godot.Timer? timer;

        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("OverlayAuthorToast", new Vector2(vp.X - 240, 48), new Color(0.95f, 0.82f, 0.35f, 0.85f));
            panel.ZIndex = 120;
            label = new Label
            {
                Name = "ToastLabel",
                SelfModulate = new Color(0.98f, 0.90f, 0.45f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            timer = new global::Godot.Timer { Name = "ToastTimer", WaitTime = 2.0, OneShot = true };
            timer.Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            };
            panel.AddChild(label);
            panel.AddChild(timer);
            sceneRoot.AddChild(panel);
        }
        else
        {
            panel.Position = new Vector2(vp.X - 240, 48);
            label = panel.GetNodeOrNull<Label>("ToastLabel");
            timer = panel.GetNodeOrNull<global::Godot.Timer>("ToastTimer");
            if (label == null || timer == null) return;
        }

        label.Text = message.Contains('[', StringComparison.Ordinal) ? message : $"Tier List: {message} [F6]";
        panel.Visible = forceVisible || !_overlayHiddenByHotkey;
        timer.Start(2.0);
    }

    public static string FormatHoverCommentary(string tier, string? commentary, string author = "Baalorlord")
    {
        string trimmedTier = tier?.Trim() ?? "N/A";
        string tierLabel = trimmedTier.Length == 1 ? $"{trimmedTier} Tier" : trimmedTier;
        string body = string.IsNullOrWhiteSpace(commentary) ? "" : $"\n{commentary.Trim()}";
        return $"[{author} · {tierLabel}]{body}";
    }

    public static void AttachHoverCommentaryPopup(PanelContainer badgePanel)
    {
        if (badgePanel.GetNodeOrNull<PanelContainer>("HoverCommentaryCard") != null) return;

        var popup = CreatePanel("HoverCommentaryCard", new Vector2(-60, 28), new Color(0.75f, 0.80f, 0.95f, 0.6f));
        popup.Visible = false;
        popup.ZIndex = 100;

        var label = new Label
        {
            Name = "PopupText",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(210, 0),
            SelfModulate = new Color(0.95f, 0.95f, 0.98f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        popup.AddChild(label);
        badgePanel.AddChild(popup);

        badgePanel.MouseEntered += () => SetHoverCommentaryVisible(badgePanel, true);
        badgePanel.MouseExited += () => SetHoverCommentaryVisible(badgePanel, false);
    }

    public static void SetHoverCommentaryVisibleRecursive(Node? holderNode, bool visible)
    {
        if (holderNode == null || !GodotObject.IsInstanceValid(holderNode)) return;
        if (holderNode is PanelContainer panel &&
            (panel.Name == "TierBadgePanel" ||
             panel.Name == "RelicBadgePanel" ||
             panel.Name == "PotionBadgePanel" ||
             panel.Name == "RelicOptionBadgePanel"))
        {
            SetHoverCommentaryVisible(panel, visible);
            return;
        }

        foreach (Node child in holderNode.GetChildren())
        {
            SetHoverCommentaryVisibleRecursive(child, visible);
        }
    }

    private static void SetHoverCommentaryVisible(PanelContainer badgePanel, bool visible)
    {
        if (!GodotObject.IsInstanceValid(badgePanel)) return;
        var popup = badgePanel.GetNodeOrNull<PanelContainer>("HoverCommentaryCard");
        var label = popup?.GetNodeOrNull<Label>("PopupText");
        if (popup == null || label == null || !GodotObject.IsInstanceValid(popup) || !GodotObject.IsInstanceValid(label)) return;

        if (!visible || !badgePanel.Visible || _overlayHiddenByHotkey)
        {
            popup.Visible = false;
            return;
        }

        string hoverText = badgePanel.HasMeta("HoverText") ? badgePanel.GetMeta("HoverText").AsString() : badgePanel.TooltipText;
        if (string.IsNullOrWhiteSpace(hoverText))
        {
            popup.Visible = false;
            return;
        }

        label.Text = hoverText;
        Vector2 vp = GetViewportSize(badgePanel);
        Vector2 badgeGlobal = badgePanel.GlobalPosition;
        float offsetX = -60f;
        float offsetY = 28f;
        if (badgeGlobal.X + offsetX + 235f > vp.X - 12f)
        {
            offsetX = (vp.X - 247f) - badgeGlobal.X;
        }
        if (badgeGlobal.X + offsetX < 12f)
        {
            offsetX = 12f - badgeGlobal.X;
        }
        if (badgeGlobal.Y + offsetY + 110f > vp.Y - 12f)
        {
            offsetY = -95f;
        }
        popup.Position = new Vector2(offsetX, offsetY);
        popup.Visible = true;
    }

    private static Vector2 GetViewportSize(Node node)
    {
        var size = node.GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        return size.X > 100 && size.Y > 100 ? size : new Vector2(1920, 1080);
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
            ContentMarginTop = 6,
            ContentMarginRight = 10,
            ContentMarginBottom = 6
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    public static void HideModSettingsPanel(Node? modInfoContainer)
    {
        if (modInfoContainer == null || !GodotObject.IsInstanceValid(modInfoContainer)) return;
        var existing = modInfoContainer.GetNodeOrNull<Control>("OverlayModSettingsPanel");
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            existing.Visible = false;
        }
    }

    public static void AttachOrUpdateModSettingsPanel(
        Node? modInfoContainer,
        OverlayConfig config,
        Action onConfigSaved,
        Action onToggleAuthor,
        Action onForceRefresh)
    {
        if (modInfoContainer == null || !GodotObject.IsInstanceValid(modInfoContainer) || config == null) return;

        var descNode = modInfoContainer.GetNodeOrNull<Control>("ModDescription");
        if (descNode != null && GodotObject.IsInstanceValid(descNode))
        {
            descNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        var panel = modInfoContainer.GetNodeOrNull<PanelContainer>("OverlayModSettingsPanel");
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("OverlayModSettingsPanel", new Vector2(22, 615), new Color(0.95f, 0.82f, 0.35f, 0.75f));
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

        var headerRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        var titleLbl = new Label
        {
            Text = "OVERLAY SETTINGS",
            SelfModulate = new Color(0.98f, 0.90f, 0.45f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        titleLbl.AddThemeFontSizeOverride("font_size", 14);
        headerRow.AddChild(titleLbl);

        var refreshBtn = CreateSettingsButton("↻ Refresh Tiers", isActive: true, isGoldAccent: true, () =>
        {
            onForceRefresh();
        });
        refreshBtn.CustomMinimumSize = new Vector2(150, 28);
        headerRow.AddChild(refreshBtn);
        rootVBox.AddChild(headerRow);

        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Pass };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        rootVBox.AddChild(grid);

        void Rebuild() => AttachOrUpdateModSettingsPanel(modInfoContainer, config, onConfigSaved, onToggleAuthor, onForceRefresh);

        bool overlayActive = config.EnableOverlay && !_overlayHiddenByHotkey;
        grid.AddChild(CreateSettingsButton($"Overlay: {(overlayActive ? "ON" : "OFF")}", overlayActive, false, () =>
        {
            bool next = !overlayActive;
            config.EnableOverlay = next;
            _overlayHiddenByHotkey = !next;
            onConfigSaved();
            Rebuild();
        }));

        string author = string.IsNullOrWhiteSpace(config.Author) ? "Baalorlord" : config.Author;
        grid.AddChild(CreateSettingsButton($"Tier List: {author}", isActive: true, isGoldAccent: true, () =>
        {
            onToggleAuthor();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Card Badges: {(config.EnableCardBadges ? "ON" : "OFF")}", config.EnableCardBadges, false, () =>
        {
            config.EnableCardBadges = !config.EnableCardBadges;
            onConfigSaved();
            Rebuild();
        }));

        bool itemBadges = config.EnableRelicBadges || config.EnablePotionBadges;
        grid.AddChild(CreateSettingsButton($"Relic & Potion Badges: {(itemBadges ? "ON" : "OFF")}", itemBadges, false, () =>
        {
            bool next = !itemBadges;
            config.EnableRelicBadges = next;
            config.EnablePotionBadges = next;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Hover Commentary: {(config.EnableHoverCommentary ? "ON" : "OFF")}", config.EnableHoverCommentary, false, () =>
        {
            config.EnableHoverCommentary = !config.EnableHoverCommentary;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Event Tips: {(config.EnableEventCommentary ? "ON" : "OFF")}", config.EnableEventCommentary, false, () =>
        {
            config.EnableEventCommentary = !config.EnableEventCommentary;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Deck Tracker: {(config.EnableDeckTracker ? "ON" : "OFF")}", config.EnableDeckTracker, false, () =>
        {
            config.EnableDeckTracker = !config.EnableDeckTracker;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Incoming Damage: {(config.EnableAttackSummary ? "ON" : "OFF")}", config.EnableAttackSummary, false, () =>
        {
            config.EnableAttackSummary = !config.EnableAttackSummary;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Shop Gold Left: {(config.EnableMerchantGoldLeft ? "ON" : "OFF")}", config.EnableMerchantGoldLeft, false, () =>
        {
            config.EnableMerchantGoldLeft = !config.EnableMerchantGoldLeft;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Deck View: {(_deckTrackerCollapsed ? "Compact" : "Expanded")}", !_deckTrackerCollapsed, false, () =>
        {
            _deckTrackerCollapsed = !_deckTrackerCollapsed;
            onConfigSaved();
            Rebuild();
        }));
    }

    private static Button CreateSettingsButton(string text, bool isActive, bool isGoldAccent, Action onPressed)
    {
        Color accent = isGoldAccent
            ? new Color(0.95f, 0.82f, 0.35f, 0.85f)
            : (isActive ? new Color(0.42f, 0.88f, 0.55f, 0.85f) : new Color(0.50f, 0.52f, 0.58f, 0.55f));

        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(294, 32),
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

    private static void AddTrackerLine(VBoxContainer parent, string text, Color color)
    {
        var lbl = new Label
        {
            Text = text,
            SelfModulate = color,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        parent.AddChild(lbl);
    }
}
