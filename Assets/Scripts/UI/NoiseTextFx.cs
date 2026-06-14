using System.Text;
using UnityEngine;

// high noise scrambles ui words into junk glyphs, the feed is drowning the signal
public static class NoiseTextFx
{
    private static readonly char[] Glyphs =
    {
        '█', '▓', '▒', '░', '◆', '◇', '#', '%', '&', '?',
        'Ξ', 'Ψ', 'Ø', 'Д', 'Ж', 'Ю', 'ヰ', 'ヱ', 'ヺ', '¤'
    };

    // intensity 0..1 is the fraction of letters that get replaced
    // seed keeps a given string stable between layout refreshes so it doesn't strobe
    public static string Distort(string text, float intensity, int seed = 0)
    {
        if (string.IsNullOrEmpty(text) || intensity <= 0f)
            return text;

        System.Random random = new(text.GetHashCode() ^ seed);
        StringBuilder builder = new(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];

            if (!char.IsWhiteSpace(current) && random.NextDouble() < intensity)
                builder.Append(Glyphs[random.Next(Glyphs.Length)]);
            else
                builder.Append(current);
        }

        return builder.ToString();
    }
}
