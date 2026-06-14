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
    private const string MinecraftTtfPath = "Assets/TextMesh Pro/Fonts/Minecraft.ttf";
    private const string TargetAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Fusion Pixel 12 SDF.asset";
    private const string MinecraftAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Minecraft SDF.asset";

    // handy for checking the cjk fallback in play mode
    [MenuItem("Tools/ThreadsFall/Toggle Demo Locale")]
    public static void ToggleDemoLocale()
    {
        Loc.ToggleDemoLocale();
        Debug.Log("locale now " + Loc.CurrentLocale);
    }

    // the canvas backdrop has to be the very first child so everything draws over it
    [MenuItem("Tools/ThreadsFall/Send Backdrop To Back")]
    public static void SendBackdropToBack()
    {
        GameObject backdrop = GameObject.Find("Backdrop");

        if (backdrop == null)
        {
            Debug.LogWarning("no backdrop in scene");
            return;
        }

        backdrop.transform.SetAsFirstSibling();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(backdrop.scene);
        Debug.Log("backdrop sent to back");
    }

    // play mode helper to peek at the shop without finishing a round
    [MenuItem("Tools/ThreadsFall/Debug Open Shop")]
    public static void OpenShop()
    {
        RoundFlowController flow = Object.FindFirstObjectByType<RoundFlowController>();

        if (flow == null)
        {
            Debug.LogWarning("no flow controller in scene");
            return;
        }

        System.Reflection.MethodInfo method = typeof(RoundFlowController).GetMethod(
            "OpenShopForDebug",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(flow, null);
        Debug.Log("opened shop");
    }

    // play mode helper to eyeball the game over screen without actually losing
    [MenuItem("Tools/ThreadsFall/Debug Force Game Over")]
    public static void ForceGameOver()
    {
        RoundFlowController flow = Object.FindFirstObjectByType<RoundFlowController>();

        if (flow == null)
        {
            Debug.LogWarning("no flow controller in scene");
            return;
        }

        System.Reflection.MethodInfo method = typeof(RoundFlowController).GetMethod(
            "EnterGameOver",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(flow, null);
        Debug.Log("forced game over");
    }

    // play mode helper to watch a boss skill (and the screen shake) without waiting
    [MenuItem("Tools/ThreadsFall/Debug Force Boss Skill")]
    public static void ForceBossSkill()
    {
        RunEffectSystem effects = Object.FindFirstObjectByType<RunEffectSystem>();

        if (effects == null)
        {
            Debug.LogWarning("no effect system in scene");
            return;
        }

        effects.ForceBossSkillForDebug();
    }

    // play mode helper to watch the high-noise ui corruption without grinding noise
    [MenuItem("Tools/ThreadsFall/Debug Add 40 Noise")]
    public static void AddNoiseForDebug()
    {
        RunStatsController stats = Object.FindFirstObjectByType<RunStatsController>();

        if (stats == null)
        {
            Debug.LogWarning("no stats controller in scene");
            return;
        }

        stats.ApplyNoiseDelta(40);
        Debug.Log($"noise now {stats.Noise}");
    }

    // play mode helper: grant attention and buy shelf slot 0 to exercise the purchase flow
    [MenuItem("Tools/ThreadsFall/Debug Buy First Shelf Item")]
    public static void BuyFirstShelfItem()
    {
        RunStatsController stats = Object.FindFirstObjectByType<RunStatsController>();
        RunShopController shop = Object.FindFirstObjectByType<RunShopController>();

        if (stats == null || shop == null)
        {
            Debug.LogWarning("no stats or shop controller in scene");
            return;
        }

        stats.AddAttention(300);
        bool bought = shop.TryBuyShelfSlot(0);
        Debug.Log($"debug buy slot 0: {bought}");
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

    // the original asset was sampled at 8pt into a 256 atlas which reads mushy,
    // this rebuilds it at high sampling so the pixel edges stay sharp
    [MenuItem("Tools/ThreadsFall/Rebuild Minecraft SDF Crisp")]
    public static void RebuildMinecraftSdf()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(MinecraftTtfPath);

        if (source == null)
        {
            Debug.LogError("minecraft ttf missing at " + MinecraftTtfPath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MinecraftAssetPath) != null)
            AssetDatabase.DeleteAsset(MinecraftAssetPath);

        TMP_FontAsset crisp = TMP_FontAsset.CreateFontAsset(source, 128, 8, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
        crisp.name = "Minecraft SDF";
        AssetDatabase.CreateAsset(crisp, MinecraftAssetPath);
        crisp.atlasTexture.name = crisp.name + " Atlas";
        crisp.material.name = crisp.name + " Material";
        AssetDatabase.AddObjectToAsset(crisp.atlasTexture, crisp);
        AssetDatabase.AddObjectToAsset(crisp.material, crisp);

        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetAssetPath);
        crisp.fallbackFontAssetTable ??= new List<TMP_FontAsset>();

        if (fallback != null && !crisp.fallbackFontAssetTable.Contains(fallback))
            crisp.fallbackFontAssetTable.Add(fallback);

        EditorUtility.SetDirty(crisp);
        AssetDatabase.SaveAssets();
        Debug.Log("minecraft sdf rebuilt crisp at " + MinecraftAssetPath);
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
