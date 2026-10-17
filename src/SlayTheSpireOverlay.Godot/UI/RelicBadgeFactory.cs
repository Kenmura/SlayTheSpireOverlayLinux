using Godot;
using System;
using SlayTheSpireOverlay.Core.Interfaces;

namespace SlayTheSpireOverlay.Godot.UI;

public static class RelicBadgeFactory
{
    public static void CreateOrUpdateBadge(
        Node relicNode,
        ITierListProvider tierProvider,
        string relicId,
        string author = "Baalorlord",
        Func<Node, bool>? isSuppressed = null,
        bool showHoverCommentary = true) =>
        TierBadgeFactory.AttachBadge(
            relicNode,
            "RelicBadgePanel",
            new Vector2(22, -8),
            tierProvider,
            relicId,
            author,
            stripOptionSuffix: false,
            alignRightOnControl: false,
            cornerRadius: 4,
            marginH: 5,
            marginV: 2,
            separation: 4,
            isSuppressed: isSuppressed,
            showHoverCommentary: showHoverCommentary);

    public static void CreateOrUpdatePotionBadge(
        Node potionNode,
        ITierListProvider tierProvider,
        string potionId,
        string author = "Baalorlord",
        Func<Node, bool>? isSuppressed = null,
        bool showHoverCommentary = true) =>
        TierBadgeFactory.AttachBadge(
            potionNode,
            "PotionBadgePanel",
            new Vector2(18, -8),
            tierProvider,
            potionId,
            author,
            stripOptionSuffix: false,
            alignRightOnControl: false,
            cornerRadius: 4,
            marginH: 5,
            marginV: 2,
            separation: 4,
            isSuppressed: isSuppressed,
            showHoverCommentary: showHoverCommentary);

    public static void CreateOrUpdateOptionBadge(
        Node buttonNode,
        ITierListProvider tierProvider,
        string optionId,
        string author = "Baalorlord",
        bool showHoverCommentary = true) =>
        TierBadgeFactory.AttachBadge(
            buttonNode,
            "RelicOptionBadgePanel",
            new Vector2(340, 6),
            tierProvider,
            optionId,
            author,
            stripOptionSuffix: true,
            alignRightOnControl: true,
            cornerRadius: 4,
            marginH: 5,
            marginV: 2,
            separation: 4,
            isSuppressed: null,
            showHoverCommentary: showHoverCommentary);
}
