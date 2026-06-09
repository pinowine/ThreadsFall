using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// one shot setup that turns the fusion pixel ttf into a dynamic tmp asset
// and chains it behind minecraft sdf so chinese text renders in pixel style
public static class ThreadsFallFontSetup
{
    private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/FusionPixel12.ttf";
    private const string TargetAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Fusion Pixel 12 SDF.asset";
    private const string MinecraftAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Minecraft SDF.asset";

    // handy for checking the cjk fallback in play mode
    [MenuItem("Tools/ThreadsFall/Toggle Demo Locale")]
    public static void ToggleDemoLocale()
    {
        Loc.ToggleDemoLocale();
        Debug.Log("locale now " + Loc.CurrentLocale);
    }

    // play mode helper so automated checks can reach a boss node without input
    [MenuItem("Tools/ThreadsFall/Debug Fast Forward To Boss")]
    public static void FastForwardToBoss()
    {
        RoundFlowController flow = Object.FindFirstObjectByType<RoundFlowController>();

        if (flow == null)
        {
            Debug.LogWarning("no flow controller in scene");
            return;
        }

        System.Reflection.MethodInfo method = typeof(RoundFlowController).GetMethod(
            "FastForwardToNextBossForDebug",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(flow, null);
        Debug.Log("fast forwarded to next boss node");
    }

    [MenuItem("Tools/ThreadsFall/Create CJK Fallback Font")]
    public static void CreateCjkFallback()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);

        if (sourceFont == null)
        {
            Debug.LogError("fusion pixel ttf missing at " + SourceFontPath);
            return;
        }

        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetAssetPath);

        if (fallback == null)
        {
            // dynamic mode pulls glyphs on demand so the huge cjk set stays cheap
            fallback = TMP_FontAsset.CreateFontAsset(sourceFont, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fallback.name = "Fusion Pixel 12 SDF";
            AssetDatabase.CreateAsset(fallback, TargetAssetPath);

            // atlas and material ride along as sub assets so the asset survives reloads
            fallback.atlasTexture.name = fallback.name + " Atlas";
            fallback.material.name = fallback.name + " Material";
            AssetDatabase.AddObjectToAsset(fallback.atlasTexture, fallback);
            AssetDatabase.AddObjectToAsset(fallback.material, fallback);
        }

        TMP_FontAsset minecraft = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MinecraftAssetPath);

        if (minecraft == null)
        {
            Debug.LogError("minecraft sdf missing at " + MinecraftAssetPath);
            return;
        }

        minecraft.fallbackFontAssetTable ??= new List<TMP_FontAsset>();

        if (!minecraft.fallbackFontAssetTable.Contains(fallback))
        {
            minecraft.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(minecraft);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("cjk fallback ready at " + TargetAssetPath);
    }
}
