using TMPro;
using UnityEngine;

// single home for fonts, size tiers, and palette so views stop scattering magic values
public static class UiTheme
{
    // size tiers, pixel font renders crispest when everything sticks to these
    public const int Title = 18;
    public const int Heading = 14;
    public const int Body = 11;
    public const int Label = 9;
    public const int Small = 7;

    // palette pulled from the tuples that used to repeat across views
    public static readonly Color PanelBg = new(0.84f, 0.84f, 0.84f, 0.96f);
    public static readonly Color PanelBgDark = new(0.78f, 0.78f, 0.78f, 1f);
    public static readonly Color TooltipBg = new(0.05f, 0.05f, 0.06f, 0.98f);
    public static readonly Color ButtonBg = new(0.92f, 0.92f, 0.88f, 1f);
    public static readonly Color TextPrimary = Color.black;
    public static readonly Color TextInverse = Color.white;
    public static readonly Color TextMuted = new(0.68f, 0.68f, 0.68f, 1f);
    public static readonly Color AccentDanger = new(0.92f, 0.05f, 0.04f, 1f);
    public static readonly Color AccentSafe = new(0.4f, 0.85f, 0.45f, 1f);
    public static readonly Color AccentWarning = new(0.95f, 0.78f, 0.22f, 1f);
    public static readonly Color NodeDone = new(0.42f, 0.42f, 0.42f, 0.86f);
    public static readonly Color NodeCurrent = new(0.96f, 0.96f, 0.9f, 1f);

    private static TMP_FontAsset cachedFont;
    private static bool fontResolved;

    public static TMP_FontAsset Font
    {
        get
        {
            if (!fontResolved)
            {
                // minecraft sdf lives in resources so no scene wiring needed
                cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Minecraft SDF");
                fontResolved = true;
            }

            return cachedFont != null ? cachedFont : TMP_Settings.defaultFontAsset;
        }
    }

    // every text should at least pass through here so the theme font sticks
    public static void ApplyFont(TMP_Text text)
    {
        if (text != null)
            text.font = Font;
    }

    // one stop styling, autosize min is always tier minus two so ranges stay consistent
    public static void Style(TMP_Text text, float size, FontStyles style, Color color, bool autoSize = false)
    {
        if (text == null)
            return;

        text.font = Font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.enableAutoSizing = autoSize;

        if (autoSize)
        {
            text.fontSizeMin = Mathf.Max(4f, size - 2f);
            text.fontSizeMax = size;
        }
    }
}
