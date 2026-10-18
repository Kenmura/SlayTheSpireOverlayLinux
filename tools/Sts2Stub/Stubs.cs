using System;
using System.Collections.Generic;
using Godot;

namespace MegaCrit.Sts2.Core.Modding
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ModInitializerAttribute : Attribute
    {
        public string MethodName { get; }
        public ModInitializerAttribute(string methodName) => MethodName = methodName;
    }
}

namespace MegaCrit.Sts2.Core.Models
{
    public sealed class ModelId
    {
        public string Category { get; set; } = string.Empty;
        public string Entry { get; set; } = string.Empty;
    }

    public abstract class AbstractModel
    {
        public ModelId? Id { get; set; }
    }

    public class CardModel : AbstractModel { }
    public class RelicModel : AbstractModel
    {
        public event System.Action? DisplayAmountChanged;
        protected void InvokeDisplayAmountChanged() => DisplayAmountChanged?.Invoke();
    }
}

namespace MegaCrit.Sts2.Core.HoverTips
{
    public class HoverTip
    {
        public Models.AbstractModel? CanonicalModel { get; set; }
    }

    public class CardHoverTip : HoverTip
    {
        public Models.CardModel? Card { get; set; }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Cards
{
    public class NCard : Control
    {
        public Models.CardModel? Model { get; set; }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Relics
{
    public class NRelic : Control
    {
        public Models.RelicModel? Model { get; set; }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Events
{
    public class EventOption
    {
        public Models.RelicModel? Relic { get; set; }
        public IEnumerable<HoverTips.HoverTip>? HoverTips { get; set; }
        public string? TextKey { get; set; }
    }

    public class NEventOptionButton : Control
    {
        public EventOption? Option { get; set; }
    }
}
