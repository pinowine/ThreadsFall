using System;
using System.Collections.Generic;
using UnityEngine;

// new kinds go at the END, the ints are baked into the item assets
public enum ShopEffectKind
{
    None,
    BlockTrollEffectNextRound,
    RemoveRandomActiveTrollEffectChance,
    HideBossIntentForRun,
    HideBossIntentNextRound,
    // one-shot: eats the next boss skill that would fire
    BlockNextBossSkill,
    // one-shot: the boss cannot use skills at all next round
    DisableBossSkillsNextRound,
    // persistent perks, marked green in the hud
    AttentionGainPercent,
    FallSpeedPercentPersistent,
    ComposurePerRound,
    RevealTruePreview,
    RevealPieceTags,
    BlockUiCorruption
}

[Serializable]
public sealed class ShopEffectSpec
{
    public ShopEffectKind kind;
    public TrollEffectType trollEffectType;
    [Range(0f, 1f)] public float chance = 1f;
    // percent or stat amount for the kinds that need a number
    public int magnitude;

    // bridge into the shared effect pipeline so the legacy assets stay untouched
    public EffectSpec ToEffectSpec()
    {
        return kind switch
        {
            ShopEffectKind.BlockTrollEffectNextRound => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.BlockTrollEffect, trollEffectType = trollEffectType, durationRounds = 1 },
            ShopEffectKind.RemoveRandomActiveTrollEffectChance => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.RemoveRandomActiveTrollEffect, chance = chance },
            ShopEffectKind.HideBossIntentForRun => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.HideBossIntent, durationRounds = -1 },
            ShopEffectKind.HideBossIntentNextRound => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.HideBossIntent, durationRounds = 1 },
            ShopEffectKind.BlockNextBossSkill => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.BlockNextBossSkill },
            ShopEffectKind.DisableBossSkillsNextRound => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.DisableBossSkillsNextRound },
            ShopEffectKind.AttentionGainPercent => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.AttentionGainPercent, magnitude = magnitude },
            // board modifiers reset each round, so persistent perks re-apply on every round start
            ShopEffectKind.FallSpeedPercentPersistent => new EffectSpec { trigger = EffectTrigger.RoundStart, kind = EffectKind.FallSpeedPercent, magnitude = magnitude, durationRounds = -1 },
            ShopEffectKind.ComposurePerRound => new EffectSpec { trigger = EffectTrigger.RoundStart, kind = EffectKind.AddComposure, magnitude = magnitude, durationRounds = -1 },
            ShopEffectKind.RevealTruePreview => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.RevealTruePreview },
            ShopEffectKind.RevealPieceTags => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.RevealPieceTags },
            ShopEffectKind.BlockUiCorruption => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.BlockUiCorruption },
            _ => new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.None }
        };
    }

    // persistent perks render green in tooltips and the purchased list
    public bool IsPersistent => EffectLore.IsPersistentKind(ToEffectSpec().kind) || kind == ShopEffectKind.FallSpeedPercentPersistent || kind == ShopEffectKind.ComposurePerRound;
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

    public bool HasPersistentEffect
    {
        get
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null && effects[i].IsPersistent)
                    return true;
            }

            return false;
        }
    }
}
