using System;
using System.Collections.Generic;
using UnityEngine;

// central effect runner, boss skills and shop items both land here as EffectSpec
// and get executed when their trigger fires, replaces the old RunEffectController
public class RunEffectSystem : MonoBehaviour
{
    public event Action EffectsChanged;

    public bool CanShowBossIntent => !hideBossIntentForRun && (hideBossIntentUntilRound <= 0 || currentRoundIndex > hideBossIntentUntilRound);

    private sealed class QueuedEffect
    {
        public EffectSpec spec;
        public string source;
        public bool fromBoss;
        // round index after which the effect is dropped, -1 keeps it for the run
        public int expiresAfterRound;
    }

    private readonly List<QueuedEffect> queuedEffects = new();
    private readonly Dictionary<TrollEffectType, int> blockedTrollEffectsUntilRound = new();
    private readonly List<TrollEffectType> activeTrollEffects = new();
    private RunStatsController statsController;
    private TetrisBoardController boardController;
    private RoundPiecePreviewView previewView;
    private bool hideBossIntentForRun;
    private int hideBossIntentUntilRound;
    private int currentRoundIndex;

    private void OnEnable()
    {
        GameEvents.OnLineCleared += HandleLineCleared;
        GameEvents.OnPieceLocked += HandlePieceLocked;
        GameEvents.OnShopOpened += HandleShopOpened;
    }

    private void OnDisable()
    {
        GameEvents.OnLineCleared -= HandleLineCleared;
        GameEvents.OnPieceLocked -= HandlePieceLocked;
        GameEvents.OnShopOpened -= HandleShopOpened;
    }

    public void Initialize(RunStatsController stats, TetrisBoardController board, RoundPiecePreviewView preview)
    {
        statsController = stats;
        boardController = board;
        previewView = preview;
    }

    public void ResetRun()
    {
        hideBossIntentForRun = false;
        hideBossIntentUntilRound = 0;
        currentRoundIndex = 0;
        blockedTrollEffectsUntilRound.Clear();
        activeTrollEffects.Clear();
        queuedEffects.Clear();
        ClearPreviewOverrides();
        NotifyEffectsChanged();
    }

    public void BeginRound(int roundIndex)
    {
        bool couldShowBossIntent = CanShowBossIntent;
        currentRoundIndex = Mathf.Max(0, roundIndex);
        ExpireRoundEffects();
        ExpireQueuedEffects();
        ClearPreviewOverrides();

        if (couldShowBossIntent != CanShowBossIntent)
            NotifyEffectsChanged();
    }

    // boss skills register fresh each node, previous boss effects are dropped
    public void RegisterBossEffects(BossDefinition boss)
    {
        bool changed = ClearBossEffects();

        if (boss != null && boss.effects != null)
        {
            for (int i = 0; i < boss.effects.Count; i++)
            {
                EffectSpec spec = boss.effects[i];

                if (spec == null || spec.kind == EffectKind.None)
                    continue;

                Apply(spec, boss.bossKey, fromBoss: true);

                if (spec.trollEffectType != TrollEffectType.None)
                    activeTrollEffects.Add(spec.trollEffectType);

                changed = true;
            }
        }

        if (changed)
            NotifyEffectsChanged();
    }

    // single entry point for both shop purchases and boss registration
    // OnAcquire executes right away, everything else waits for its trigger
    public bool Apply(EffectSpec spec, string source, bool fromBoss = false)
    {
        if (spec == null || spec.kind == EffectKind.None)
            return true;

        if (spec.trigger == EffectTrigger.OnAcquire)
            return Execute(spec);

        queuedEffects.Add(new QueuedEffect
        {
            spec = spec,
            source = source,
            fromBoss = fromBoss,
            expiresAfterRound = spec.durationRounds < 0 ? -1 : currentRoundIndex + Mathf.Max(0, spec.durationRounds)
        });

        return true;
    }

    public void Fire(EffectTrigger trigger)
    {
        for (int i = 0; i < queuedEffects.Count; i++)
        {
            QueuedEffect queued = queuedEffects[i];

            if (queued.spec.trigger != trigger)
                continue;

            if (queued.spec.chance < 1f && UnityEngine.Random.value > queued.spec.chance)
                continue;

            // shop block items get their moment here, a blocked boss effect is skipped
            if (queued.fromBoss && queued.spec.trollEffectType != TrollEffectType.None && TryBlockTrollEffect(queued.spec.trollEffectType))
                continue;

            Execute(queued.spec);
        }
    }

    public void BlockTrollEffectNextRound(TrollEffectType effectType)
    {
        if (effectType == TrollEffectType.None)
            return;

        int targetRound = currentRoundIndex + 1;
        blockedTrollEffectsUntilRound[effectType] = Mathf.Max(GetBlockedUntilRound(effectType), targetRound);
        NotifyEffectsChanged();
    }

    public bool TryBlockTrollEffect(TrollEffectType effectType)
    {
        if (effectType == TrollEffectType.None)
            return false;

        if (!blockedTrollEffectsUntilRound.TryGetValue(effectType, out int blockedUntilRound))
            return false;

        if (blockedUntilRound < currentRoundIndex)
            return false;

        GameEvents.TrollEffectBlocked(effectType);
        return true;
    }

    public void HideBossIntentForRun()
    {
        hideBossIntentForRun = true;
        NotifyEffectsChanged();
    }

    public void HideBossIntentNextRound()
    {
        hideBossIntentUntilRound = Mathf.Max(hideBossIntentUntilRound, currentRoundIndex + 1);
        NotifyEffectsChanged();
    }

    public void RegisterActiveTrollEffect(TrollEffectType effectType)
    {
        if (effectType == TrollEffectType.None)
            return;

        activeTrollEffects.Add(effectType);
        NotifyEffectsChanged();
    }

    public bool TryRemoveRandomActiveTrollEffect(float chance)
    {
        if (activeTrollEffects.Count <= 0)
            return false;

        if (UnityEngine.Random.value > Mathf.Clamp01(chance))
            return false;

        int index = UnityEngine.Random.Range(0, activeTrollEffects.Count);
        TrollEffectType removedType = activeTrollEffects[index];
        activeTrollEffects.RemoveAt(index);
        RemoveQueuedBossEffect(removedType);
        NotifyEffectsChanged();
        return true;
    }

    private bool Execute(EffectSpec spec)
    {
        switch (spec.kind)
        {
            case EffectKind.AddNoise:
                statsController?.ApplyNoiseDelta(spec.magnitude);
                return true;
            case EffectKind.AddComposure:
                statsController?.ApplyComposureDelta(spec.magnitude);
                return true;
            case EffectKind.AddAttention:
                statsController?.AddAttention(spec.magnitude);
                return true;
            case EffectKind.HideNextPreview:
                previewView?.SetHiddenOverride(true);
                return true;
            case EffectKind.FakeNextPreview:
                previewView?.SetCorruptedOverride(true);
                return true;
            case EffectKind.AddGarbageCell:
                boardController?.AddGarbageCells(Mathf.Max(1, spec.magnitude));
                return true;
            case EffectKind.BlockTrollEffect:
                BlockTrollEffectNextRound(spec.trollEffectType);
                return true;
            case EffectKind.RemoveRandomActiveTrollEffect:
                return TryRemoveRandomActiveTrollEffect(spec.chance);
            case EffectKind.HideBossIntent:
                if (spec.durationRounds < 0)
                    HideBossIntentForRun();
                else
                    HideBossIntentNextRound();
                return true;
            default:
                return true;
        }
    }

    private void HandleLineCleared(int lines)
    {
        Fire(EffectTrigger.LineClear);
    }

    private void HandlePieceLocked()
    {
        Fire(EffectTrigger.PieceLocked);
    }

    private void HandleShopOpened()
    {
        Fire(EffectTrigger.ShopEnter);
    }

    private bool ClearBossEffects()
    {
        bool changed = activeTrollEffects.Count > 0;
        activeTrollEffects.Clear();

        for (int i = queuedEffects.Count - 1; i >= 0; i--)
        {
            if (!queuedEffects[i].fromBoss)
                continue;

            queuedEffects.RemoveAt(i);
            changed = true;
        }

        return changed;
    }

    private void RemoveQueuedBossEffect(TrollEffectType effectType)
    {
        for (int i = 0; i < queuedEffects.Count; i++)
        {
            QueuedEffect queued = queuedEffects[i];

            if (!queued.fromBoss || queued.spec.trollEffectType != effectType)
                continue;

            queuedEffects.RemoveAt(i);
            return;
        }
    }

    private void ClearPreviewOverrides()
    {
        if (previewView == null)
            return;

        previewView.SetHiddenOverride(false);
        previewView.SetCorruptedOverride(false);
    }

    private int GetBlockedUntilRound(TrollEffectType effectType)
    {
        return blockedTrollEffectsUntilRound.TryGetValue(effectType, out int roundIndex) ? roundIndex : 0;
    }

    private void ExpireRoundEffects()
    {
        reusableExpiredEffects.Clear();

        foreach (KeyValuePair<TrollEffectType, int> entry in blockedTrollEffectsUntilRound)
        {
            if (entry.Value < currentRoundIndex)
                reusableExpiredEffects.Add(entry.Key);
        }

        for (int i = 0; i < reusableExpiredEffects.Count; i++)
        {
            blockedTrollEffectsUntilRound.Remove(reusableExpiredEffects[i]);
        }
    }

    private void ExpireQueuedEffects()
    {
        for (int i = queuedEffects.Count - 1; i >= 0; i--)
        {
            QueuedEffect queued = queuedEffects[i];

            if (queued.expiresAfterRound >= 0 && queued.expiresAfterRound < currentRoundIndex)
                queuedEffects.RemoveAt(i);
        }
    }

    private void NotifyEffectsChanged()
    {
        EffectsChanged?.Invoke();
        GameEvents.TrollEffectsChanged();
    }

    private readonly List<TrollEffectType> reusableExpiredEffects = new();
}
