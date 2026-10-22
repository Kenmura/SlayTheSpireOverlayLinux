using Godot;

namespace SlayTheSpireTestSubjectVote.Godot.UI;

/// <summary>Colours and small style helpers so the pop-up matches the game's gold-on-brown panels (mockup C).</summary>
internal static class PopupStyle
{
    public static readonly Color Gold = new(0.95f, 0.76f, 0.31f);
    public static readonly Color Cream = new(0.91f, 0.87f, 0.78f);
    public static readonly Color Warning = new(1f, 0.56f, 0.42f);
    public static readonly Color AllowGreen = new(0.50f, 0.82f, 0.56f);
    public static readonly Color RejectRed = new(0.88f, 0.54f, 0.54f);
    public static readonly Color Waiting = new(0.80f, 0.73f, 0.54f);

    public static StyleBoxFlat Panel() => Box(new Color(0.20f, 0.16f, 0.11f), new Color(0.54f, 0.45f, 0.28f), 2, 10, 18);

    public static StyleBoxFlat ButtonFill(Color fill, Color border) => Box(fill, border, 2, 6, 10);

    public static Label Text(string text, int size, Color color, bool center = true)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public static Button Action(string text, Color fill, Color border)
    {
        var button = new Button { Text = text, MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(150, 40) };
        button.AddThemeStyleboxOverride("normal", ButtonFill(fill, border));
        button.AddThemeStyleboxOverride("hover", ButtonFill(fill.Lightened(0.15f), border));
        button.AddThemeStyleboxOverride("pressed", ButtonFill(fill.Darkened(0.15f), border));
        button.AddThemeStyleboxOverride("disabled", ButtonFill(fill.Darkened(0.45f), border.Darkened(0.4f)));
        return button;
    }

    public static Control Pip(Color? color)
    {
        var pip = new Panel { CustomMinimumSize = new Vector2(14, 14), MouseFilter = Control.MouseFilterEnum.Ignore };
        pip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = color ?? new Color(0f, 0f, 0f, 0f),
            BorderColor = Waiting,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        });
        return pip;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int borderWidth, int radius, int margin) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthTop = borderWidth,
        BorderWidthBottom = borderWidth,
        BorderWidthLeft = borderWidth,
        BorderWidthRight = borderWidth,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginTop = margin,
        ContentMarginBottom = margin,
        ContentMarginLeft = margin,
        ContentMarginRight = margin,
    };
}
