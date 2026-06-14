using System;
using UnityEngine;

// when an effect fires, note that high stack / misdrop style triggers from
// TrollTrigger have no gameplay hooks yet
public enum EffectTrigger
{
    OnAcquire,
    RoundStart,
    RoundEnd,
    LineClear,
    PieceLocked,
    ShopEnter
}

// what an effect does, shared by boss skills and shop items
// new kinds go at the END, the ints are baked into boss assets
public enum EffectKind
{
    None,
    AddNoise,
    AddComposure,
    AddAttention,
    HideNextPreview,
    FakeNextPreview,
    AddGarbageCell,
    BlockTrollEffect,
    RemoveRandomActiveTrollEffect,
    HideBossIntent,
    CorruptPieces,
    GlitchPieces,
    FallSpeedPercent,
    // boss active skills, fired live during a round by the skill driver
    SpawnTopGarbage,
    RotationLockSeconds,
    RemoveBottomLineNoReward,
    // percent modifier on attention income, boss aura (negative) or item perk (positive)
    AttentionGainPercent,
    // item perks
    BlockNextBossSkill,
    DisableBossSkillsNextRound,
    RevealTruePreview,
    RevealPieceTags,
    BlockUiCorruption
}

// boss personality knob, scales the skill chance off game state
public enum SkillMood
{
    None,
    CalmAtHighNoise,
    HypedByLongTakes
}

[Serializable]
public class EffectSpec
{
    public EffectTrigger trigger;
    public EffectKind kind;
    // targeting tag for block and remove plus active effect bookkeeping
    public TrollEffectType trollEffectType;
    // noise delta, garbage count, composure delta and so on
    public int magnitude;
    [Range(0f, 1f)] public float chance = 1f;
    // 0 instant, n lasts n rounds, -1 lasts the rest of the run
    public int durationRounds;
    public SkillMood mood;
}

// short human labels for the effects list and tooltips
public static class EffectLore
{
    public static string KindLabel(EffectSpec spec)
    {
        return spec.kind switch
        {
            EffectKind.AddNoise => Loc.Format("effect.kind.add_noise", FormatSigned(spec.magnitude)),
            EffectKind.AddComposure => Loc.Format("effect.kind.add_composure", FormatSigned(spec.magnitude)),
            EffectKind.AddAttention => Loc.Format("effect.kind.add_attention", FormatSigned(spec.magnitude)),
            EffectKind.HideNextPreview => Loc.T("effect.kind.hide_preview"),
            EffectKind.FakeNextPreview => Loc.T("effect.kind.fake_preview"),
            EffectKind.AddGarbageCell => Loc.Format("effect.kind.garbage", Mathf.Max(1, spec.magnitude)),
            EffectKind.BlockTrollEffect => Loc.Format("effect.kind.block", TrollTypeName(spec.trollEffectType)),
            EffectKind.RemoveRandomActiveTrollEffect => Loc.Format("effect.kind.remove", Mathf.RoundToInt(spec.chance * 100f)),
            EffectKind.HideBossIntent => Loc.T("effect.kind.hide_intent"),
            EffectKind.CorruptPieces => Loc.Format("effect.kind.corrupt", Mathf.RoundToInt(spec.chance * 100f)),
            EffectKind.GlitchPieces => Loc.Format("effect.kind.glitch", Mathf.RoundToInt(spec.chance * 100f)),
            EffectKind.FallSpeedPercent => Loc.Format("effect.kind.fall_speed", FormatSigned(spec.magnitude)),
            EffectKind.SpawnTopGarbage => Loc.Format("effect.kind.top_garbage", Mathf.Max(1, spec.magnitude)),
            EffectKind.RotationLockSeconds => Loc.Format("effect.kind.rotation_lock", Mathf.Max(1, spec.magnitude)),
            EffectKind.RemoveBottomLineNoReward => Loc.T("effect.kind.steal_line"),
            EffectKind.AttentionGainPercent => Loc.Format("effect.kind.attention_percent", FormatSigned(spec.magnitude)),
            EffectKind.BlockNextBossSkill => Loc.T("effect.kind.block_skill"),
            EffectKind.DisableBossSkillsNextRound => Loc.T("effect.kind.disable_skills"),
            EffectKind.RevealTruePreview => Loc.T("effect.kind.true_preview"),
            EffectKind.RevealPieceTags => Loc.T("effect.kind.reveal_tags"),
            EffectKind.BlockUiCorruption => Loc.T("effect.kind.block_corruption"),
            _ => string.Empty
        };
    }

    // run-long perks render green in the purchased list, one-shots gray out when spent
    public static bool IsPersistentKind(EffectKind kind)
    {
        return kind == EffectKind.AttentionGainPercent
            || kind == EffectKind.RevealTruePreview
            || kind == EffectKind.RevealPieceTags
            || kind == EffectKind.BlockUiCorruption;
    }

    private static string FormatSigned(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }

    public static string TrollTypeName(TrollEffectType type)
    {
        string segment = type switch
        {
            TrollEffectType.FakeNextPreview => "fake_preview",
            TrollEffectType.AddGarbageCell => "garbage",
            TrollEffectType.HideNextPreview => "hide_preview",
            TrollEffectType.IncreaseNoise => "noise",
            TrollEffectType.ReduceTrust => "trust",
            TrollEffectType.AccelerateFall => "fall_speed",
            TrollEffectType.CorruptPieces => "corrupt",
            TrollEffectType.GlitchPieces => "glitch",
            _ => "none"
        };

        return Loc.T("troll.type." + segment);
    }
}
