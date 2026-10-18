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
    private static bool _prevPinDown;
    private static ulong _lastPolledFrame;
    private static float _uiScale = 1.0f;
    private static float _deckOffsetX;
    private static float _deckOffsetY;
    private static float _attackOffsetX;
    private static float _attackOffsetY;
    private static bool _deckCardHoverEnabled = true;

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

    public static void SyncLayoutFromConfig(OverlayConfig? config)
    {
        if (config == null) return;
        _uiScale = OverlayCalculators.ClampUiScale(config.UiScale);
        _deckOffsetX = config.DeckTrackerOffsetX;
        _deckOffsetY = config.DeckTrackerOffsetY;
        _attackOffsetX = config.AttackSummaryOffsetX;
        _attackOffsetY = config.AttackSummaryOffsetY;
        _deckCardHoverEnabled = config.EnableDeckTrackerCardHover;
    }

    public static void PollHotkeys(
        Action? onToggleAuthor = null,
        Action? onToggleVisibility = null,
        Action? onForceRefresh = null,
        OverlayConfig? config = null,
        Action? onCollapseToggled = null)
    {
        try
        {
            SyncLayoutFromConfig(config);
            ulong frame = Engine.GetProcessFrames();
            if (frame != 0 && frame == _lastPolledFrame) return;
            _lastPolledFrame = frame;

            Key f8Key = ResolveGodotKey(config?.ToggleOverlayKey, Key.F8);
            Key f7Key = ResolveGodotKey(config?.CollapseDeckTrackerKey, Key.F7);
            Key f6Key = ResolveGodotKey(config?.ToggleAuthorKey, Key.F6);
            Key f9Key = ResolveGodotKey(config?.RefreshTiersKey, Key.F9);
            bool chords = config?.EnableControllerChords ?? true;

            bool f8Down = Input.IsPhysicalKeyPressed(f8Key) || (chords && IsControllerChordPressed(JoyButton.DpadUp));
            if (f8Down && !_prevF8Down)
            {
                _overlayHiddenByHotkey = !_overlayHiddenByHotkey;
                onToggleVisibility?.Invoke();
            }
            _prevF8Down = f8Down;

            bool f7Down = Input.IsPhysicalKeyPressed(f7Key) || (chords && IsControllerChordPressed(JoyButton.DpadDown));
            if (f7Down && !_prevF7Down)
            {
                _deckTrackerCollapsed = !_deckTrackerCollapsed;
                onCollapseToggled?.Invoke();
            }
            _prevF7Down = f7Down;

            bool f6Down = Input.IsPhysicalKeyPressed(f6Key) || (chords && IsControllerChordPressed(JoyButton.DpadLeft));
            if (f6Down && !_prevF6Down) onToggleAuthor?.Invoke();
            _prevF6Down = f6Down;

            bool f9Down = Input.IsPhysicalKeyPressed(f9Key) || (chords && IsControllerChordPressed(JoyButton.DpadRight));
            if (f9Down && !_prevF9Down) onForceRefresh?.Invoke();
            _prevF9Down = f9Down;
        }
        catch
        {
            // Ignore outside active input frame
        }
    }

    public static bool PollPinHotkey(OverlayConfig? config)
    {
        try
        {
            Key pinKey = ResolveGodotKey(config?.PinShopItemKey, Key.F5);
            bool chords = config?.EnableControllerChords ?? true;
            bool pinDown = Input.IsPhysicalKeyPressed(pinKey) ||
                           Input.IsMouseButtonPressed(MouseButton.Middle) ||
                           (chords && IsControllerChordPressed(JoyButton.Y));

            bool triggered = pinDown && !_prevPinDown;
            _prevPinDown = pinDown;
            return triggered;
        }
        catch
        {
            return false;
        }
    }

    public static Key ResolveGodotKey(string? configuredKey, Key fallback)
    {
        string fallbackName = fallback.ToString();
        string normalized = OverlayCalculators.NormalizeKeybindName(configuredKey, fallbackName);
        if (normalized.Length == 1 && normalized[0] >= '0' && normalized[0] <= '9')
        {
            return (Key)((int)Key.Key0 + (normalized[0] - '0'));
        }

        if (Enum.TryParse<Key>(normalized, ignoreCase: true, out var parsed) && Enum.IsDefined(typeof(Key), parsed))
        {
            return parsed;
        }
        return fallback;
    }

    private static bool IsControllerChordPressed(JoyButton dpadButton)
    {
        for (int dev = 0; dev < 4; dev++)
        {
            if (Input.IsJoyButtonPressed(dev, JoyButton.Back) && Input.IsJoyButtonPressed(dev, dpadButton))
            {
                return true;
            }
        }
        return false;
    }

    public static void UpdateQuickToggleBar(
        Node? room,
        OverlayConfig? config,
        Action? onToggleAuthor = null,
        Action? onToggleVisibility = null,
        Action? onForceRefresh = null)
    {
        if (room == null || !GodotObject.IsInstanceValid(room)) return;
        SyncLayoutFromConfig(config);

        var panel = room.GetNodeOrNull<PanelContainer>("QuickToggleBarPanel");
        bool enabled = config?.EnableQuickToggleBar ?? true;
        if (!enabled)
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(room);
        string author = string.IsNullOrWhiteSpace(config?.Author) ? "Baalorlord" : config!.Author.Trim();
        string authorKey = OverlayCalculators.NormalizeKeybindName(config?.ToggleAuthorKey, "F6");
        string hideKey = OverlayCalculators.NormalizeKeybindName(config?.ToggleOverlayKey, "F8");
        string refreshKey = OverlayCalculators.NormalizeKeybindName(config?.RefreshTiersKey, "F9");
        bool hidden = _overlayHiddenByHotkey;

        float widthEstimate = hidden ? 145f : 275f;
        var (posX, posY) = OverlayCalculators.ComputeScaledPanelPosition(
            vp.X - widthEstimate - 16f,
            72f,
            0f,
            0f,
            widthEstimate,
            32f,
            _uiScale,
            vp.X,
            vp.Y,
            rightAligned: true);
        Vector2 targetPos = new Vector2(posX, posY);

        HBoxContainer hbox;
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("QuickToggleBarPanel", targetPos, new Color(0.85f, 0.75f, 0.35f, 0.65f));
            panel.MouseFilter = Control.MouseFilterEnum.Pass;
            panel.ZIndex = 90;
            hbox = new HBoxContainer { Name = "QuickToggleHBox", MouseFilter = Control.MouseFilterEnum.Pass };
            hbox.AddThemeConstantOverride("separation", 6);
            panel.AddChild(hbox);
            room.AddChild(panel);
        }
        else
        {
            panel.Position = targetPos;
            hbox = panel.GetNodeOrNull<HBoxContainer>("QuickToggleHBox");
            if (hbox == null) return;
        }

        panel.Scale = new Vector2(_uiScale, _uiScale);
        panel.Visible = true;
        string sig = $"{(hidden ? '1' : '0')}|{author}|{authorKey}|{hideKey}|{refreshKey}|{_uiScale:F2}";
        if (panel.HasMeta("QuickBarSig") && panel.GetMeta("QuickBarSig").AsString() == sig)
        {
            return;
        }
        panel.SetMeta("QuickBarSig", sig);

        foreach (Node child in hbox.GetChildren())
        {
            hbox.RemoveChild(child);
            child.QueueFree();
        }

        if (hidden)
        {
            hbox.AddChild(CreateMiniActionButton($"Show Overlay [{hideKey}]", isGoldAccent: true, () =>
            {
                _overlayHiddenByHotkey = false;
                Callable.From(() =>
                {
                    onToggleVisibility?.Invoke();
                    if (GodotObject.IsInstanceValid(room))
                    {
                        UpdateQuickToggleBar(room, config, onToggleAuthor, onToggleVisibility, onForceRefresh);
                    }
                }).CallDeferred();
            }));
            return;
        }

        hbox.AddChild(CreateMiniActionButton($"⇄ {author} [{authorKey}]", isGoldAccent: true, () =>
        {
            Callable.From(() =>
            {
                onToggleAuthor?.Invoke();
                if (GodotObject.IsInstanceValid(room))
                {
                    UpdateQuickToggleBar(room, config, onToggleAuthor, onToggleVisibility, onForceRefresh);
                }
            }).CallDeferred();
        }));

        hbox.AddChild(CreateMiniActionButton($"↻ [{refreshKey}]", isGoldAccent: false, () =>
        {
            Callable.From(() => onForceRefresh?.Invoke()).CallDeferred();
        }));

        hbox.AddChild(CreateMiniActionButton($"Hide [{hideKey}]", isGoldAccent: false, () =>
        {
            _overlayHiddenByHotkey = true;
            Callable.From(() =>
            {
                onToggleVisibility?.Invoke();
                if (GodotObject.IsInstanceValid(room))
                {
                    UpdateQuickToggleBar(room, config, onToggleAuthor, onToggleVisibility, onForceRefresh);
                }
            }).CallDeferred();
        }));
    }

    public static void UpdateAttackSummary(Node combatRoom, AttackSummary summary, bool enabled, bool showIncoming = true)
    {
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;

        var panel = combatRoom.GetNodeOrNull<PanelContainer>("AttackSummaryPanel");
        bool hasActiveThreat = showIncoming &&
                               (summary.TotalHits > 0 ||
                                summary.SelfDamage > 0 ||
                                summary.DoomedEnemies > 0 ||
                                (summary.Allies != null && summary.Allies.Any(a => a.IncomingDamage > 0)));
        bool hasOutgoing = summary.OutgoingDamage != null && summary.OutgoingDamage.TotalAttacksInHand > 0 &&
                           summary.OutgoingDamage.MaxSingleTargetDamage + summary.OutgoingDamage.TotalAoeDamage > 0;
        if (!enabled || _overlayHiddenByHotkey || (!hasActiveThreat && !hasOutgoing))
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(combatRoom);
        var (posX, posY) = OverlayCalculators.ComputeScaledPanelPosition(
            vp.X - 240f,
            vp.Y * 0.20f,
            _attackOffsetX,
            _attackOffsetY,
            224f,
            64f,
            _uiScale,
            vp.X,
            vp.Y,
            rightAligned: true);
        Vector2 targetPos = new Vector2(posX, posY);

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

        panel.Scale = new Vector2(_uiScale, _uiScale);
        panel.Visible = true;
        string headerLine;
        if (summary.TotalHits > 0)
        {
            string hitWord = summary.TotalHits == 1 ? "Hit" : "Hits";
            string foeSuffix = summary.AttackingEnemies > 1 ? $" · {summary.AttackingEnemies} Foes" : "";
            headerLine = $"Incoming: {summary.TotalDamage} DMG ({summary.TotalHits} {hitWord}{foeSuffix})";
        }
        else if (summary.SelfDamage > 0)
        {
            headerLine = $"End of Turn: {summary.SelfDamage} Self DMG";
        }
        else
        {
            headerLine = $"Incoming: 0 DMG ({summary.DoomedEnemies} Doomed)";
        }

        var eotParts = new List<string>(3);
        if (summary.PassiveBlock > 0) eotParts.Add($"+{summary.PassiveBlock} EOT Block");
        if (summary.SelfDamage > 0 && summary.TotalHits > 0) eotParts.Add($"+{summary.SelfDamage} Self DMG");
        if (summary.DoomedEnemies > 0 && (summary.TotalHits > 0 || summary.SelfDamage > 0)) eotParts.Add($"{summary.DoomedEnemies} Doomed");
        string eotLine = eotParts.Count > 0 ? "\n" + string.Join(" · ", eotParts) : "";

        string netLine = summary.IsLethal
            ? $"⚠ LETHAL (-{summary.UnblockedDamage} HP)"
            : (summary.UnblockedDamage == 0 ? "Blocked (0 DMG)" : $"Unblocked: {summary.UnblockedDamage} DMG");

        var allyLines = new List<string>();
        if (summary.Allies != null && summary.Allies.Count > 0)
        {
            foreach (var ally in summary.Allies)
            {
                if (ally.IsLethal)
                {
                    allyLines.Add($"⚠ {ally.Name}: LETHAL (-{ally.UnblockedDamage} HP)");
                }
                else if (ally.IncomingDamage > 0)
                {
                    string status = ally.UnblockedDamage > 0 ? $"-{ally.UnblockedDamage} HP" : "Blocked";
                    allyLines.Add($"{ally.Name}: {status}");
                }
            }
        }
        string allySection = allyLines.Count > 0 ? "\n" + string.Join("\n", allyLines) : "";

        string outgoingLine = "";
        if (summary.OutgoingDamage != null && summary.OutgoingDamage.TotalAttacksInHand > 0)
        {
            var outDmg = summary.OutgoingDamage;
            int totalMax = outDmg.MaxSingleTargetDamage + outDmg.TotalAoeDamage;
            if (outDmg.HasAllEnemiesLethal)
            {
                outgoingLine = $"\n⚔ LETHAL (All Foes · {totalMax} DMG)";
            }
            else if (outDmg.HasTargetLethal && !string.IsNullOrEmpty(outDmg.LethalTargetName))
            {
                outgoingLine = $"\n⚔ LETHAL ({outDmg.LethalTargetName} · {totalMax} DMG)";
            }
            else if (totalMax > 0)
            {
                outgoingLine = $"\nHand: {totalMax} DMG max ({outDmg.TotalPlayableCards} Playable)";
            }
        }

        label.Text = hasActiveThreat
            ? $"{headerLine}{eotLine}\n{netLine}{allySection}{outgoingLine}"
            : outgoingLine.TrimStart('\n');
    }

    public static void UpdateDeckTracker(
        Node combatRoom,
        DeckTrackerState? state,
        bool enabled,
        string author = "Baalorlord",
        string collapseKeyLabel = "F7")
    {
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;

        var panel = combatRoom.GetNodeOrNull<PanelContainer>("DeckTrackerPanel");
        if (!enabled || _overlayHiddenByHotkey || state == null)
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(combatRoom);
        var (posX, posY) = OverlayCalculators.ComputeScaledPanelPosition(
            16f,
            vp.Y * 0.15f,
            _deckOffsetX,
            _deckOffsetY,
            240f,
            _deckTrackerCollapsed ? 130f : 320f,
            _uiScale,
            vp.X,
            vp.Y,
            rightAligned: false);
        Vector2 targetPos = new Vector2(posX, posY);

        VBoxContainer vbox;
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("DeckTrackerPanel", targetPos, new Color(0.45f, 0.65f, 0.95f, 0.45f));
            panel.MouseFilter = Control.MouseFilterEnum.Pass;
            vbox = new VBoxContainer { Name = "TrackerVBox", MouseFilter = Control.MouseFilterEnum.Pass };
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);
            combatRoom.AddChild(panel);
        }
        else
        {
            panel.Position = targetPos;
            panel.MouseFilter = Control.MouseFilterEnum.Pass;
            vbox = panel.GetNodeOrNull<VBoxContainer>("TrackerVBox");
            if (vbox == null) return;
        }

        panel.Scale = new Vector2(_uiScale, _uiScale);
        panel.Visible = true;
        if (_deckTrackerCollapsed || !_deckCardHoverEnabled) HideDeckCardTip(combatRoom);
        string normalizedKey = OverlayCalculators.NormalizeKeybindName(collapseKeyLabel, "F7");
        string sig = BuildDeckStateSignature(state, _deckTrackerCollapsed, author, normalizedKey) + (_deckCardHoverEnabled ? "|h" : "|n");
        if (panel.HasMeta("StateSig") && panel.GetMeta("StateSig").AsString() == sig)
        {
            return;
        }
        panel.SetMeta("StateSig", sig);
        HideDeckCardTip(combatRoom); // hovered rows are freed below, so MouseExited will never fire

        foreach (Node child in vbox.GetChildren())
        {
            vbox.RemoveChild(child);
            child.QueueFree();
        }

        string toggleHint = _deckTrackerCollapsed ? $"[{normalizedKey} +]" : $"[{normalizedKey} -]";
        string reshuffleHint = state.DrawCount < state.NextHandSampleSize && state.DiscardCount > 0 ? " ↻" : "";
        var headerBtn = CreateTrackerHeaderButton(
            $"DRAW PILE ({state.DrawCount}{reshuffleHint}) [{author}] {toggleHint}",
            () =>
            {
                _deckTrackerCollapsed = !_deckTrackerCollapsed;
                Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(panel))
                    {
                        panel.SetMeta("StateSig", string.Empty);
                    }
                    if (GodotObject.IsInstanceValid(combatRoom))
                    {
                        UpdateDeckTracker(combatRoom, state, enabled, author, normalizedKey);
                    }
                }).CallDeferred();
            });
        vbox.AddChild(headerBtn);

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
                var line = AddTrackerLine(vbox, $" [{cost}] {card.Title} ({card.InDraw}/{card.Total}){oddsSuffix}", cardColor);
                if (_deckCardHoverEnabled)
                {
                    line.MouseFilter = Control.MouseFilterEnum.Pass;
                    var hoverCard = card;
                    var hoverPanel = panel;
                    line.MouseEntered += () => ShowDeckCardTip(combatRoom, hoverPanel, line, hoverCard);
                    line.MouseExited += () => HideDeckCardTip(combatRoom);
                }
            }
        }

        AddTrackerLine(vbox, $"Hand: {state.HandCount} | Discard: {state.DiscardCount} | Exhaust: {state.ExhaustCount}", new Color(0.70f, 0.74f, 0.82f));
    }

    private static string BuildDeckStateSignature(DeckTrackerState state, bool collapsed, string author, string collapseKey = "F7")
    {
        var sb = new System.Text.StringBuilder(128);
        sb.Append(collapsed ? '1' : '0').Append('|')
          .Append(author).Append('|')
          .Append(collapseKey).Append('|')
          .Append(state.DrawCount).Append('|')
          .Append(state.HandCount).Append('|')
          .Append(state.DiscardCount).Append('|')
          .Append(state.ExhaustCount).Append('|')
          .Append(state.NextHandSampleSize);
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

    public static void UpdateShopSlotPinBadge(Node? slot, bool isPinned)
    {
        if (slot == null || !GodotObject.IsInstanceValid(slot)) return;

        var existing = slot.GetNodeOrNull<Control>("ShopPinBadge");
        if (!isPinned)
        {
            if (existing != null && GodotObject.IsInstanceValid(existing))
            {
                existing.Visible = false;
            }
            return;
        }

        if (existing == null || !GodotObject.IsInstanceValid(existing))
        {
            var panel = CreatePanel("ShopPinBadge", new Vector2(4, 4), new Color(0.95f, 0.82f, 0.35f, 0.9f));
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
            panel.ZIndex = 60;
            var lbl = new Label
            {
                Text = "🛒 PINNED",
                SelfModulate = new Color(1.0f, 0.90f, 0.40f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            lbl.AddThemeFontSizeOverride("font_size", 10);
            panel.AddChild(lbl);
            slot.AddChild(panel);
        }
        else
        {
            existing.Visible = true;
        }
    }

    public static void UpdateMerchantGoldLeft(
        Node merchantRoom,
        ShopBudgetSummary? budget,
        bool enabled,
        string pinKeyLabel = "F5")
    {
        if (merchantRoom == null || !GodotObject.IsInstanceValid(merchantRoom)) return;

        var panel = merchantRoom.GetNodeOrNull<PanelContainer>("MerchantGoldLeftPanel");
        if (!enabled || _overlayHiddenByHotkey || budget == null)
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(merchantRoom);
        var (posX, posY) = OverlayCalculators.ComputeScaledPanelPosition(
            40f,
            vp.Y * 0.09f,
            0f,
            0f,
            260f,
            44f,
            _uiScale,
            vp.X,
            vp.Y,
            rightAligned: false);
        Vector2 targetPos = new Vector2(posX, posY);

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

        panel.Scale = new Vector2(_uiScale, _uiScale);
        panel.Visible = true;

        string normalizedKey = OverlayCalculators.NormalizeKeybindName(pinKeyLabel, "F5");

        string line1;
        string line2;
        bool affordable;

        if (budget.HasPinnedItems && budget.HasHoveredItem)
        {
            int cartRem = budget.PinnedRemainingGold;
            int combRem = budget.CombinedRemainingGold ?? cartRem;
            affordable = combRem >= 0;

            line1 = cartRem >= 0
                ? $"🛒 Cart ({budget.PinnedCount}): -{budget.PinnedTotalCost}g ({cartRem}g left)"
                : $"🛒 Cart ({budget.PinnedCount}): Need {-cartRem}g more (Cost: {budget.PinnedTotalCost}g)";

            line2 = combRem >= 0
                ? $"Total: {combRem}g left (+{budget.HoveredCost}g item) [{normalizedKey}]"
                : $"Total: Need {-combRem}g more (+{budget.HoveredCost}g item) [{normalizedKey}]";
        }
        else if (budget.HasPinnedItems)
        {
            int cartRem = budget.PinnedRemainingGold;
            affordable = cartRem >= 0;

            line1 = affordable
                ? $"🛒 Cart ({budget.PinnedCount}): -{budget.PinnedTotalCost}g -> {cartRem}g left"
                : $"🛒 Cart ({budget.PinnedCount}): Need {-cartRem}g more (Cost: {budget.PinnedTotalCost}g)";

            line2 = $"[{normalizedKey}: Hover to Pin/Unpin]";
        }
        else
        {
            int rem = budget.HoveredRemainingGold ?? 0;
            int cost = budget.HoveredCost ?? 0;
            affordable = rem >= 0;

            line1 = affordable
                ? $"Gold Left: {rem}g (-{cost}g)"
                : $"Need {-rem}g more (Cost: {cost}g)";

            line2 = $"[{normalizedKey}: Pin to Cart]";
        }

        Color accentColor = affordable ? new Color(1.0f, 0.85f, 0.3f) : new Color(1.0f, 0.48f, 0.42f);
        label.SelfModulate = accentColor;
        if (panel.GetThemeStylebox("panel") is StyleBoxFlat sb)
        {
            sb.BorderColor = affordable
                ? new Color(0.95f, 0.78f, 0.25f, 0.8f)
                : new Color(0.95f, 0.35f, 0.35f, 0.85f);
        }
        label.Text = $"{line1}\n{line2}";
    }

    public static void UpdateMerchantGoldLeft(Node merchantRoom, (int RemainingGold, int Cost)? goldLeft, bool enabled)
    {
        if (goldLeft == null)
        {
            UpdateMerchantGoldLeft(merchantRoom, (ShopBudgetSummary?)null, enabled);
        }
        else
        {
            var summary = new ShopBudgetSummary(
                HoveredCost: goldLeft.Value.Cost,
                HoveredRemainingGold: goldLeft.Value.RemainingGold,
                PinnedCount: 0,
                PinnedTotalCost: 0,
                PinnedRemainingGold: goldLeft.Value.RemainingGold + goldLeft.Value.Cost,
                CombinedRemainingGold: goldLeft.Value.RemainingGold);
            UpdateMerchantGoldLeft(merchantRoom, summary, enabled);
        }
    }

    public static void UpdateDeckSynergyPanel(Node room, DeckSynergySummary? summary, bool enabled)
    {
        if (room == null || !GodotObject.IsInstanceValid(room)) return;

        var panel = room.GetNodeOrNull<PanelContainer>("DeckSynergyPanel");
        if (!enabled || _overlayHiddenByHotkey || summary == null || summary.TotalCards == 0)
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(room);
        float widthEstimate = 640f;
        float scaledW = widthEstimate * _uiScale;
        float centeredX = Math.Max(16f, (vp.X - scaledW) * 0.5f);
        var (posX, posY) = OverlayCalculators.ComputeScaledPanelPosition(
            centeredX,
            40f,
            0f,
            0f,
            0f,
            34f,
            _uiScale,
            vp.X,
            vp.Y,
            rightAligned: false);
        Vector2 targetPos = new Vector2(posX, posY);

        HBoxContainer hbox;
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("DeckSynergyPanel", targetPos, new Color(0.40f, 0.70f, 0.95f, 0.75f));
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
            panel.ZIndex = 85;
            hbox = new HBoxContainer { Name = "SynergyHBox", MouseFilter = Control.MouseFilterEnum.Ignore };
            hbox.AddThemeConstantOverride("separation", 8);
            panel.AddChild(hbox);
            room.AddChild(panel);
        }
        else
        {
            panel.Position = targetPos;
            hbox = panel.GetNodeOrNull<HBoxContainer>("SynergyHBox");
            if (hbox == null) return;
        }

        panel.Scale = new Vector2(_uiScale, _uiScale);
        panel.Visible = true;

        string sig = $"{summary.FormattedSummary}|{_uiScale:F2}";
        if (panel.HasMeta("SynergySig") && panel.GetMeta("SynergySig").AsString() == sig)
        {
            return;
        }
        panel.SetMeta("SynergySig", sig);

        foreach (Node child in hbox.GetChildren())
        {
            hbox.RemoveChild(child);
            child.QueueFree();
        }

        var tagLbl = new Label
        {
            Text = "DECK SYNERGY",
            SelfModulate = new Color(0.95f, 0.82f, 0.35f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        tagLbl.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(tagLbl);

        var sepLbl = new Label
        {
            Text = "│",
            SelfModulate = new Color(0.50f, 0.55f, 0.65f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        sepLbl.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(sepLbl);

        var textLbl = new Label
        {
            Text = summary.FormattedSummary,
            SelfModulate = new Color(0.90f, 0.94f, 0.98f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        textLbl.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(textLbl);
    }

    public static void UpdateBossPrepBanner(Node room, BossPrepInfo? prep, bool enabled)
    {
        if (room == null || !GodotObject.IsInstanceValid(room)) return;

        var panel = room.GetNodeOrNull<PanelContainer>("BossPrepBannerPanel");
        if (!enabled || _overlayHiddenByHotkey || prep == null)
        {
            if (panel != null && GodotObject.IsInstanceValid(panel)) panel.Visible = false;
            return;
        }

        Vector2 vp = GetViewportSize(room);
        float widthEstimate = 640f;
        float scaledW = widthEstimate * _uiScale;
        float centeredX = Math.Max(16f, (vp.X - scaledW) * 0.5f);

        bool hasSynergyPanel = room.GetNodeOrNull<Control>("DeckSynergyPanel")?.Visible ?? false;
        float baseY = hasSynergyPanel ? 78f : 40f;

        var (posX, posY) = OverlayCalculators.ComputeScaledPanelPosition(
            centeredX,
            baseY,
            0f,
            0f,
            0f,
            34f,
            _uiScale,
            vp.X,
            vp.Y,
            rightAligned: false);
        Vector2 targetPos = new Vector2(posX, posY);

        HBoxContainer hbox;
        Color borderColor = prep.IsFogged
            ? new Color(0.65f, 0.45f, 0.85f, 0.75f)
            : new Color(0.95f, 0.35f, 0.35f, 0.85f);

        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("BossPrepBannerPanel", targetPos, borderColor);
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
            panel.ZIndex = 85;
            hbox = new HBoxContainer { Name = "BossPrepHBox", MouseFilter = Control.MouseFilterEnum.Ignore };
            hbox.AddThemeConstantOverride("separation", 8);
            panel.AddChild(hbox);
            room.AddChild(panel);
        }
        else
        {
            panel.Position = targetPos;
            if (panel.GetThemeStylebox("panel") is StyleBoxFlat sb)
            {
                sb.BorderColor = borderColor;
            }
            hbox = panel.GetNodeOrNull<HBoxContainer>("BossPrepHBox");
            if (hbox == null) return;
        }

        panel.Scale = new Vector2(_uiScale, _uiScale);
        panel.Visible = true;

        string sig = $"{prep.BossId}|{prep.DisplayName}|{prep.IsFogged}|{baseY:F0}|{_uiScale:F2}";
        if (panel.HasMeta("BossPrepSig") && panel.GetMeta("BossPrepSig").AsString() == sig)
        {
            return;
        }
        panel.SetMeta("BossPrepSig", sig);

        foreach (Node child in hbox.GetChildren())
        {
            hbox.RemoveChild(child);
            child.QueueFree();
        }

        var tagLbl = new Label
        {
            Text = prep.IsFogged ? "ACT BOSS (FOGGED)" : "ACT BOSS",
            SelfModulate = prep.IsFogged ? new Color(0.85f, 0.65f, 0.95f) : new Color(1.0f, 0.45f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        tagLbl.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(tagLbl);

        var sepLbl = new Label
        {
            Text = "│",
            SelfModulate = new Color(0.50f, 0.55f, 0.65f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        sepLbl.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(sepLbl);

        var textLbl = new Label
        {
            Text = $"{prep.DisplayName} · {prep.StrategicTip}",
            SelfModulate = new Color(0.92f, 0.94f, 0.98f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        textLbl.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(textLbl);
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
                    var (posX, posY) = OverlayCalculators.ComputeScaledPanelPosition(
                        vp.X - 420f,
                        vp.Y * 0.12f,
                        0f,
                        0f,
                        404f,
                        140f,
                        _uiScale,
                        vp.X,
                        vp.Y,
                        rightAligned: true);
                    var panel = CreatePanel("EventCommentaryPanel", new Vector2(posX, posY), new Color(0.65f, 0.75f, 0.95f, 0.6f));
                    panel.Scale = new Vector2(_uiScale, _uiScale);
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
        var (posX, posY) = OverlayCalculators.ComputeScaledPanelPosition(
            vp.X - 240f,
            48f,
            0f,
            0f,
            224f,
            40f,
            _uiScale,
            vp.X,
            vp.Y,
            rightAligned: true);
        Vector2 targetPos = new Vector2(posX, posY);

        var panel = sceneRoot.GetNodeOrNull<PanelContainer>("OverlayAuthorToast");
        Label? label;
        global::Godot.Timer? timer;

        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("OverlayAuthorToast", targetPos, new Color(0.95f, 0.82f, 0.35f, 0.85f));
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
            panel.Position = targetPos;
            label = panel.GetNodeOrNull<Label>("ToastLabel");
            timer = panel.GetNodeOrNull<global::Godot.Timer>("ToastTimer");
            if (label == null || timer == null) return;
        }

        panel.Scale = new Vector2(_uiScale, _uiScale);
        label.Text = message.Contains('[', StringComparison.Ordinal) ? message : $"Tier List: {message} [F6]";
        panel.Visible = forceVisible || !_overlayHiddenByHotkey;
        timer.Start(2.0);
    }

    public static string FormatHoverCommentary(
        string tier,
        string? commentary,
        string author = "Baalorlord",
        int upgradeLevel = 0,
        string? enchantmentId = null) =>
        OverlayCalculators.FormatCardHoverCommentary(tier, commentary, author, upgradeLevel, enchantmentId);

    public static void RefreshHoverCommentaryIfVisible(PanelContainer? badgePanel)
    {
        if (badgePanel == null || !GodotObject.IsInstanceValid(badgePanel)) return;
        var popup = badgePanel.GetNodeOrNull<PanelContainer>("HoverCommentaryCard");
        if (popup != null && GodotObject.IsInstanceValid(popup) && popup.Visible)
        {
            SetHoverCommentaryVisible(badgePanel, true);
        }
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
        popup.Scale = new Vector2(_uiScale, _uiScale);
        Vector2 vp = GetViewportSize(badgePanel);
        Vector2 badgeGlobal = badgePanel.GlobalPosition;
        float scaledPopupW = 235f * _uiScale;
        float scaledPopupH = 110f * _uiScale;
        float offsetX = -60f;
        float offsetY = 28f;
        if (badgeGlobal.X + offsetX + scaledPopupW > vp.X - 12f)
        {
            offsetX = (vp.X - scaledPopupW - 12f) - badgeGlobal.X;
        }
        if (badgeGlobal.X + offsetX < 12f)
        {
            offsetX = 12f - badgeGlobal.X;
        }
        if (badgeGlobal.Y + offsetY + scaledPopupH > vp.Y - 12f)
        {
            offsetY = -scaledPopupH + 15f;
        }
        if (badgeGlobal.Y + offsetY < 12f)
        {
            offsetY = 12f - badgeGlobal.Y;
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
        SyncLayoutFromConfig(config);

        var descNode = modInfoContainer.GetNodeOrNull<Control>("ModDescription");
        if (descNode != null && GodotObject.IsInstanceValid(descNode))
        {
            descNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        var panel = modInfoContainer.GetNodeOrNull<PanelContainer>("OverlayModSettingsPanel");
        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = CreatePanel("OverlayModSettingsPanel", new Vector2(22, 590), new Color(0.95f, 0.82f, 0.35f, 0.75f));
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
        rootVBox.AddThemeConstantOverride("separation", 6);
        panel.AddChild(rootVBox);

        void Rebuild() => Callable.From(() =>
            AttachOrUpdateModSettingsPanel(modInfoContainer, config, onConfigSaved, onToggleAuthor, onForceRefresh)).CallDeferred();

        var headerRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        headerRow.AddThemeConstantOverride("separation", 8);
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

        var scaleBtn = CreateSettingsButton(
            $"UI Scale: {OverlayCalculators.FormatUiScalePercent(config.UiScale)}",
            isActive: true,
            isGoldAccent: false,
            () =>
            {
                config.UiScale = OverlayCalculators.CycleUiScale(config.UiScale);
                SyncLayoutFromConfig(config);
                onConfigSaved();
                Rebuild();
            });
        scaleBtn.CustomMinimumSize = new Vector2(140, 28);
        headerRow.AddChild(scaleBtn);

        var refreshBtn = CreateSettingsButton("↻ Refresh Tiers", isActive: true, isGoldAccent: true, () =>
        {
            Callable.From(onForceRefresh).CallDeferred();
        });
        refreshBtn.CustomMinimumSize = new Vector2(150, 28);
        headerRow.AddChild(refreshBtn);
        rootVBox.AddChild(headerRow);

        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Pass };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 5);
        rootVBox.AddChild(grid);

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

        grid.AddChild(CreateSettingsButton($"Deck Synergy: {(config.EnableDeckSynergyBar ? "ON" : "OFF")}", config.EnableDeckSynergyBar, false, () =>
        {
            config.EnableDeckSynergyBar = !config.EnableDeckSynergyBar;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Act Boss Reminder: {(config.EnableBossPrepBanner ? "ON" : "OFF")}", config.EnableBossPrepBanner, false, () =>
        {
            config.EnableBossPrepBanner = !config.EnableBossPrepBanner;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Incoming Damage: {(config.EnableAttackSummary ? "ON" : "OFF")}", config.EnableAttackSummary, false, () =>
        {
            config.EnableAttackSummary = !config.EnableAttackSummary;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Deck Card Hover: {(config.EnableDeckTrackerCardHover ? "ON" : "OFF")}", config.EnableDeckTrackerCardHover, false, () =>
        {
            config.EnableDeckTrackerCardHover = !config.EnableDeckTrackerCardHover;
            SyncLayoutFromConfig(config);
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Outgoing Damage: {(config.EnableOutgoingDamageSummary ? "ON" : "OFF")}", config.EnableOutgoingDamageSummary, false, () => { config.EnableOutgoingDamageSummary = !config.EnableOutgoingDamageSummary; onConfigSaved(); Rebuild(); }));
        grid.AddChild(CreateSettingsButton($"Relic Counters: {(config.EnableRelicProcCounters ? "ON" : "OFF")}", config.EnableRelicProcCounters, false, () => { config.EnableRelicProcCounters = !config.EnableRelicProcCounters; RelicCounterBadgeFactory.RefreshForModel(null, config.EnableRelicProcCounters); onConfigSaved(); Rebuild(); }));

        grid.AddChild(CreateSettingsButton($"Shop Gold Left:{(config.EnableMerchantGoldLeft ? "ON" : "OFF")}", config.EnableMerchantGoldLeft, false, () =>
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

        grid.AddChild(CreateSettingsButton($"Quick Toggle Bar: {(config.EnableQuickToggleBar ? "ON" : "OFF")}", config.EnableQuickToggleBar, false, () =>
        {
            config.EnableQuickToggleBar = !config.EnableQuickToggleBar;
            onConfigSaved();
            Rebuild();
        }));

        grid.AddChild(CreateSettingsButton($"Gamepad Chords: {(config.EnableControllerChords ? "ON" : "OFF")}", config.EnableControllerChords, false, () =>
        {
            config.EnableControllerChords = !config.EnableControllerChords;
            onConfigSaved();
            Rebuild();
        }));
    }

    private static Button CreateTrackerHeaderButton(string text, Action onPressed)
    {
        var btn = new Button
        {
            Name = "TrackerHeaderButton",
            Text = text,
            Alignment = HorizontalAlignment.Left,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None
        };
        btn.AddThemeFontSizeOverride("font_size", 12);
        btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.45f));
        btn.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.94f, 0.65f));
        btn.AddThemeColorOverride("font_pressed_color", new Color(1.0f, 0.94f, 0.65f));

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.17f, 0.24f, 0.65f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.95f, 0.85f, 0.45f, 0.45f),
            ContentMarginLeft = 4,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.22f, 0.26f, 0.36f, 0.88f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            BorderWidthBottom = 1,
            BorderColor = new Color(1.0f, 0.92f, 0.55f, 0.85f),
            ContentMarginLeft = 4,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        btn.AddThemeStyleboxOverride("normal", normalStyle);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", hoverStyle);
        btn.Pressed += onPressed;
        return btn;
    }

    private static Button CreateMiniActionButton(string text, bool isGoldAccent, Action onPressed)
    {
        Color accent = isGoldAccent
            ? new Color(0.95f, 0.82f, 0.35f, 0.85f)
            : new Color(0.55f, 0.68f, 0.90f, 0.75f);

        var btn = new Button
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None
        };
        btn.AddThemeFontSizeOverride("font_size", 11);
        btn.AddThemeColorOverride("font_color", isGoldAccent
            ? new Color(0.98f, 0.90f, 0.45f)
            : new Color(0.88f, 0.92f, 0.98f));

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.14f, 0.19f, 0.92f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = accent,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.20f, 0.24f, 0.32f, 0.98f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(Math.Min(1f, accent.R + 0.15f), Math.Min(1f, accent.G + 0.15f), Math.Min(1f, accent.B + 0.15f), 1.0f),
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        btn.AddThemeStyleboxOverride("normal", normalStyle);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", hoverStyle);
        btn.Pressed += onPressed;
        return btn;
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

    private static void HideDeckCardTip(Node combatRoom)
    {
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)) return;
        var tip = combatRoom.GetNodeOrNull<Control>("DeckCardTip");
        if (tip != null && GodotObject.IsInstanceValid(tip)) tip.Visible = false;
    }

    private static void ShowDeckCardTip(Node combatRoom, PanelContainer trackerPanel, Control row, DeckCardGroup card)
    {
        if (combatRoom == null || !GodotObject.IsInstanceValid(combatRoom)
            || trackerPanel == null || !GodotObject.IsInstanceValid(trackerPanel)
            || row == null || !GodotObject.IsInstanceValid(row)) return;

        var old = combatRoom.GetNodeOrNull<Node>("DeckCardTip");
        if (old != null && GodotObject.IsInstanceValid(old))
        {
            combatRoom.RemoveChild(old);
            old.QueueFree();
        }

        string cost = card.CostsX ? "X" : (card.Cost?.ToString() ?? "-");
        string type = string.IsNullOrEmpty(card.Type) ? "" : $" · {card.Type}";
        string desc = string.IsNullOrWhiteSpace(card.Description) ? "(no description available)" : card.Description;

        float x = trackerPanel.Position.X + (trackerPanel.Size.X * _uiScale) + 8f;
        float y = trackerPanel.Position.Y + (row.Position.Y + trackerPanel.GetNode<Control>("TrackerVBox").Position.Y) * _uiScale;
        var tip = CreatePanel("DeckCardTip", new Vector2(x, y), new Color(0.45f, 0.65f, 0.95f, 0.7f));
        tip.MouseFilter = Control.MouseFilterEnum.Ignore;
        tip.ZIndex = 70;
        tip.Scale = new Vector2(_uiScale, _uiScale);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddTrackerLine(box, $"[{cost}] {card.Title}{type}", new Color(1f, 0.92f, 0.6f));
        var body = new Label
        {
            Text = desc,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(220, 0),
            SelfModulate = new Color(0.92f, 0.92f, 0.96f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        body.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(body);
        tip.AddChild(box);
        combatRoom.AddChild(tip);
    }

    private static Label AddTrackerLine(VBoxContainer parent, string text, Color color)
    {
        var lbl = new Label
        {
            Text = text,
            SelfModulate = color,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        parent.AddChild(lbl);
        return lbl;
    }
}
