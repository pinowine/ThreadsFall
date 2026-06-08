using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LocalizationEntry
{
    public string key;
    public string value;
}

[Serializable]
public class LocalizationTable
{
    public LocalizationEntry[] entries;
}

public sealed class JsonLocalizer : ITextLocalizer
{
    private readonly Dictionary<string, string> entries = new Dictionary<string, string>();

    public string Locale { get; private set; }

    public bool Load(string locale)
    {
        entries.Clear();
        Locale = locale;

        // Locale JSON lives in Resources so builds can load it without editor-only lookup.
        TextAsset textAsset = Resources.Load<TextAsset>("Localization/" + locale);
        if (textAsset == null)
        {
            Debug.LogWarning("Localization file not found: " + locale);
            return false;
        }

        LocalizationTable table = JsonUtility.FromJson<LocalizationTable>(textAsset.text);
        if (table == null || table.entries == null)
        {
            Debug.LogWarning("Localization file has no entries: " + locale);
            return false;
        }

        for (int i = 0; i < table.entries.Length; i++)
        {
            LocalizationEntry entry = table.entries[i];
            if (entry == null || string.IsNullOrEmpty(entry.key))
            {
                continue;
            }

            entries[entry.key] = entry.value ?? string.Empty;
        }

        return true;
    }

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        string value;
        return entries.TryGetValue(key, out value) ? value : "#" + key;
    }
}
