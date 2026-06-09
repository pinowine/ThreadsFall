using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShopEffectKind
{
    None,
    BlockTrollEffectNextRound,
    RemoveRandomActiveTrollEffectChance,
    HideBossIntentForRun,
    HideBossIntentNextRound
}

[Serializable]
public sealed class ShopEffectSpec
{
    public ShopEffectKind kind;
    public TrollEffectType trollEffectType;
    [Range(0f, 1f)] public float chance = 1f;

    // bridge into the shared effect pipeline so the 14 legacy assets stay untouched
    public EffectSpec ToEffectSpec()
    {
        return kind switch
        {
            ShopEffectKind.BlockTrollEffectNextRound => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.BlockTrollEffect, trollEffectType = trollEffectType, durationRounds = 1 },
            ShopEffectKind.RemoveRandomActiveTrollEffectChance => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.RemoveRandomActiveTrollEffect, chance = chance },
            ShopEffectKind.HideBossIntentForRun => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.HideBossIntent, durationRounds = -1 },
            ShopEffectKind.HideBossIntentNextRound => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.HideBossIntent, durationRounds = 1 },
            _ => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.None }
        };
    }
}

[CreateAssetMenu(fileName = "ShopItemDefinition", menuName = "Threads Fall/Shop Item")]
public class ShopItemDefinition : ScriptableObject
{
    public string itemId;
    public Sprite icon;
    public string nameKey;
    public string descriptionKey;
    public string effectSummaryKey;
    public int baseAttentionCost;
    public int composureDelta;
    public int noiseDelta;
    public bool setComposureToMax;
    public bool setNoiseToZero;
    public bool uniquePerRun;
    public int minRound;
    public float shelfWeight = 1f;
    public List<ShopEffectSpec> effects = new();

    public bool IsValid => !string.IsNullOrWhiteSpace(itemId);
}
