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
    HideBossIntent
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
}
