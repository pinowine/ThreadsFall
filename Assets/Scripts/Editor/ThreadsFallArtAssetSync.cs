#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ThreadsFallArtAssetSync
{
    private const string UiCatalogPath = "Assets/Resources/Data/RunUiArtCatalog.asset";
    private const string BossCatalogPath = "Assets/Resources/Data/BossCatalog.asset";
    private const string SequenceFolder = "Assets/Resources/Data/SpriteSequences";
    private const string BossFolder = "Assets/Resources/Data/Bosses";
    private const string ShopFolder = "Assets/Resources/Data/Shop";
    private const string ShopItemFolder = "Assets/Resources/Data/Shop/Items";
    private const string ShopCatalogPath = "Assets/Resources/Data/Shop/ShopCatalog.asset";

    [MenuItem("Threads Fall/Sync Aseprite Data")]
    public static void Sync()
    {
        EnsureFolder("Assets/Resources/Data");
        EnsureFolder(SequenceFolder);
        EnsureFolder(BossFolder);
        EnsureFolder(ShopFolder);
        EnsureFolder(ShopItemFolder);

        RunUiArtCatalog uiCatalog = SyncUiCatalog();
        BossCatalog bossCatalog = SyncBossCatalog();
        ShopCatalog shopCatalog = SyncShopCatalog();

        EditorUtility.SetDirty(uiCatalog);
        EditorUtility.SetDirty(bossCatalog);
        EditorUtility.SetDirty(shopCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Threads Fall aseprite data synced.");
    }

    private static RunUiArtCatalog SyncUiCatalog()
    {
        RunUiArtCatalog catalog = LoadOrCreate<RunUiArtCatalog>(UiCatalogPath);
        catalog.attentionIcon = SyncSequence("attention", "Assets/Aseprites/icons/attention.aseprite", 8f, true);
        catalog.composureIcon = SyncSequence("composure", "Assets/Aseprites/icons/composure.aseprite", 8f, true);
        catalog.noiseIcon = SyncSequence("noise", "Assets/Aseprites/icons/noise.aseprite", 8f, true);
        catalog.lockIcon = SyncSequence("lock", "Assets/Aseprites/icons/lock.aseprite", 1f, false);
        catalog.shopkeeperAvatar = SyncSequence("shopkeeper", "Assets/Aseprites/shop/shopkeeper.aseprite", 6f, true);
        catalog.searchNodeIcon = SyncSequence("progress_search", "Assets/Aseprites/icons/search.aseprite", 1f, false);
        catalog.miniBossNodeIcon = SyncSequence("progress_miniboss", "Assets/Aseprites/icons/miniboss.aseprite", 1f, false);
        catalog.finalBossNodeIcon = SyncSequence("progress_final_boss", "Assets/Aseprites/icons/final boss.aseprite", 1f, false);
        catalog.leftPanelBackground = LoadFirstSprite("Assets/Aseprites/ui/leftpanel.aseprite");
        return catalog;
    }

    private static BossCatalog SyncBossCatalog()
    {
        BossCatalog catalog = LoadOrCreate<BossCatalog>(BossCatalogPath);
        catalog.bosses = new List<BossDefinition>
        {
            SyncBoss("algorithm", BossId.Algorithm, RunNodeType.FinalBoss, "Assets/Aseprites/bosses/final/algorithm.aseprite", 7f, 90),
            SyncBoss("anonymous", BossId.Anonymous, RunNodeType.FinalBoss, "Assets/Aseprites/bosses/final/anonymous.aseprite", 7f, 85),
            SyncBoss("fake_scientist", BossId.FakeScientist, RunNodeType.MiniBoss, "Assets/Aseprites/bosses/mini/fakescientist.aseprite", 7f, 55),
            SyncBoss("fanwar_idol", BossId.FanWarIdol, RunNodeType.MiniBoss, "Assets/Aseprites/bosses/mini/fanwaridol.aseprite", 7f, 60),
            SyncBoss("flamebait", BossId.Flamebait, RunNodeType.MiniBoss, "Assets/Aseprites/bosses/mini/flamebait.aseprite", 7f, 50),
            SyncBoss("meme", BossId.Meme, RunNodeType.MiniBoss, "Assets/Aseprites/bosses/mini/meme.aseprite", 7f, 60),
            SyncBoss("review_bomber", BossId.ReviewBomber, RunNodeType.MiniBoss, "Assets/Aseprites/bosses/mini/reviewbomber.aseprite", 7f, 55)
        };
        return catalog;
    }

    private static BossDefinition SyncBoss(string bossKey, BossId bossId, RunNodeType nodeType, string spritePath, float framesPerSecond, int basePressure)
    {
        string assetPath = $"{BossFolder}/{ToPascalCase(bossKey)}.asset";
        BossDefinition boss = LoadOrCreate<BossDefinition>(assetPath);
        boss.bossKey = bossKey;
        boss.bossId = bossId;
        boss.nodeType = nodeType;
        boss.nameKey = $"boss.{bossId.ToLocSegment()}.name";
        boss.commentKey = $"boss.{bossId.ToLocSegment()}.comment.round_start";
        boss.effectKey = "boss.effect.none";
        boss.avatarSequence = SyncSequence("boss_" + bossKey, spritePath, framesPerSecond, true);
        boss.routeWeight = 1f;
        boss.basePressure = basePressure;
        EditorUtility.SetDirty(boss);
        return boss;
    }

    private static ShopCatalog SyncShopCatalog()
    {
        ShopCatalog catalog = LoadOrCreate<ShopCatalog>(ShopCatalogPath);
        catalog.items = BuildShopItems();
        return catalog;
    }

    private static List<ShopItemDefinition> BuildShopItems()
    {
        return new List<ShopItemDefinition>
        {
            SyncShopItem("archived_screenshot", "screenshot", 70, 0, -10, false, false, false, 0, 1f, new[] { Block(TrollEffectType.AddGarbageCell) }),
            SyncShopItem("text_wall", "textwall", 50, -5, 5, false, false, false, 0, 1f, new[] { Remove(0.7f) }),
            SyncShopItem("mute_word_engineering", "forbiddenword", 0, 0, 0, true, true, true, 0, 0.25f, new[] { Effect(ShopEffectKind.HideBossIntentForRun) }),
            SyncShopItem("breathing_draft", "nosend", 45, 15, 3, false, false, false, 0, 1f, new[] { Block(TrollEffectType.ReduceTrust) }),
            SyncShopItem("fifteen_minute_mute", "mute", 80, 0, -25, false, false, false, 0, 1f, new[] { Effect(ShopEffectKind.HideBossIntentNextRound) }),
            SyncShopItem("fact_check_tag", "factcheck", 65, 0, 5, false, false, false, 0, 1f, new[] { Block(TrollEffectType.FakeNextPreview) }),
            SyncShopItem("moderation_queue", "police", 90, -10, 10, false, false, false, 0, 1f, new[] { Remove(1f) }),
            SyncShopItem("slow_reply", "delay", 55, 10, 0, false, false, false, 0, 1f, new[] { Block(TrollEffectType.AccelerateFall) }),
            SyncShopItem("clear_cache", "clean", 75, 0, -15, false, false, false, 0, 1f, new[] { Block(TrollEffectType.HideNextPreview) }),
            SyncShopItem("close_dms", "lockdm", 100, -15, -20, false, false, false, 0, 1f, new[] { Block(TrollEffectType.IncreaseNoise) }),
            SyncShopItem("anti_empathy_filter", "antiempathy", 60, -8, -12, false, false, false, 1, 0.8f, new[] { Effect(ShopEffectKind.HideBossIntentNextRound), Block(TrollEffectType.CorruptPieces) }),
            SyncShopItem("call_for_sources", "call", 60, -5, 5, false, false, false, 1, 0.8f, new[] { Block(TrollEffectType.FakeNextPreview) }),
            SyncShopItem("laugh_reaction", "laugh", 35, 8, 8, false, false, false, 0, 0.8f, new[] { Block(TrollEffectType.GlitchPieces) }),
            SyncShopItem("push_back", "push", 70, -5, 10, false, false, false, 1, 0.8f, new[] { Remove(0.5f) })
        };
    }

    private static ShopItemDefinition SyncShopItem(
        string itemId,
        string spriteName,
        int cost,
        int composureDelta,
        int noiseDelta,
        bool setComposureToMax,
        bool setNoiseToZero,
        bool uniquePerRun,
        int minRound,
        float shelfWeight,
        IReadOnlyList<ShopEffectSpec> effects)
    {
        string assetPath = $"{ShopItemFolder}/{ToPascalCase(itemId)}.asset";
        ShopItemDefinition item = LoadOrCreate<ShopItemDefinition>(assetPath);
        item.itemId = itemId;
        item.icon = LoadFirstSprite($"Assets/Aseprites/shop/items/{spriteName}.aseprite");
        item.nameKey = $"shop.item.{itemId}.name";
        item.descriptionKey = $"shop.item.{itemId}.desc";
        item.effectSummaryKey = $"shop.item.{itemId}.effect";
        item.baseAttentionCost = cost;
        item.composureDelta = composureDelta;
        item.noiseDelta = noiseDelta;
        item.setComposureToMax = setComposureToMax;
        item.setNoiseToZero = setNoiseToZero;
        item.uniquePerRun = uniquePerRun;
        item.minRound = minRound;
        item.shelfWeight = shelfWeight;
        item.effects = effects != null ? effects.ToList() : new List<ShopEffectSpec>();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static ShopEffectSpec Block(TrollEffectType effectType)
    {
        return new ShopEffectSpec
        {
            kind = ShopEffectKind.BlockTrollEffectNextRound,
            trollEffectType = effectType,
            chance = 1f
        };
    }

    private static ShopEffectSpec Remove(float chance)
    {
        return new ShopEffectSpec
        {
            kind = ShopEffectKind.RemoveRandomActiveTrollEffectChance,
            chance = chance
        };
    }

    private static ShopEffectSpec Effect(ShopEffectKind kind)
    {
        return new ShopEffectSpec
        {
            kind = kind,
            chance = 1f
        };
    }

    private static SpriteSequenceDefinition SyncSequence(string sequenceId, string sourcePath, float framesPerSecond, bool loop)
    {
        string assetPath = $"{SequenceFolder}/{ToPascalCase(sequenceId)}.asset";
        SpriteSequenceDefinition sequence = LoadOrCreate<SpriteSequenceDefinition>(assetPath);
        sequence.sequenceId = sequenceId;
        sequence.framesPerSecond = framesPerSecond;
        sequence.loop = loop;
        sequence.frames = LoadSprites(sourcePath);
        EditorUtility.SetDirty(sequence);
        return sequence;
    }

    private static List<Sprite> LoadSprites(string assetPath)
    {
        // keep frame ordering deterministic across aseprite reimports
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
            .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToList();
    }

    private static Sprite LoadFirstSprite(string assetPath)
    {
        return LoadSprites(assetPath).FirstOrDefault();
    }

    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

        if (asset != null)
            return asset;

        EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace("\\", "/"));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string name = Path.GetFileName(folderPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static int ExtractTrailingNumber(string value)
    {
        int number = 0;
        int multiplier = 1;

        for (int i = value.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(value[i]))
                break;

            number += (value[i] - '0') * multiplier;
            multiplier *= 10;
        }

        return number;
    }

    private static string ToPascalCase(string value)
    {
        return string.Concat(value.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
    }
}
#endif
