using System;

public static class Loc
{
    public static event Action<string> OnLocaleChanged;

    private static ITextLocalizer localizer;

    public static string CurrentLocale { get; private set; } = "en";

    public static void Initialize(string locale = "en")
    {
        if (localizer != null && CurrentLocale == locale)
        {
            return;
        }

        SetLocale(locale);
    }

    public static void SetLocale(string locale)
    {
        JsonLocalizer jsonLocalizer = new();
        if (!jsonLocalizer.Load(locale))
        {
            // Fall back to English so missing demo translations fail softly.
            if (locale != "en")
            {
                jsonLocalizer.Load("en");
                locale = "en";
            }
        }

        localizer = jsonLocalizer;
        CurrentLocale = locale;
        OnLocaleChanged?.Invoke(CurrentLocale);
    }

    public static void ToggleDemoLocale()
    {
        SetLocale(CurrentLocale == "en" ? "zh-CN" : "en");
    }

    public static string T(string key)
    {
        EnsureInitialized();
        return localizer.Get(key);
    }

    public static string Format(string key, params object[] args)
    {
        string template = T(key);
        return args == null || args.Length == 0 ? template : string.Format(template, args);
    }

    private static void EnsureInitialized()
    {
        if (localizer == null)
        {
            SetLocale(CurrentLocale);
        }
    }
}
