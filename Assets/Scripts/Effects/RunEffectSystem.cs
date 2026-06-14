using System;
using System.Collections.Generic;
using UnityEngine;

public enum EffectStatus
{
    Active,
    Pending,
    Blocked,
    Consumed
}

public enum ActiveEffectInfoKind
{
    Effect,
    PendingRemoval
}

// how a purchased item should render in the hud: green run-long perk,
// white still waiting to do its thing, gray already spent
public enum ItemPerkStatus
{
    Consumed,
    Armed,
    Persistent
}

// one row in the effects list, ui reads these to draw and explain whats going on
public sealed class ActiveEffectInfo
{
    public ActiveEffectInfoKind infoKind = ActiveEffectInfoKind.Effect;
    public EffectSpec spec;
    public string sourceNameKey;
    public bool fromBoss;
    public EffectStatus status;
}

// central effect runner, boss skills and shop items both land here as EffectSpec
// and get executed when their trigger fires, replaces the old RunEffectController
public class RunEffectSystem : MonoBehaviour
{
    public event Action EffectsChanged;

    public bool CanShowBossIntent => !hideBossIntentForRun && (hideBossIntentUntilRound <= 0 || currentRoundIndex > hideBossIntentUntilRound);

    private sealed class QueuedEffect
    {
        public EffectSpec spec;
        public string sourceNameKey;
        public bool fromBoss;
        public EffectStatus status;
        // round index after which the effect is dropped, -1 keeps it for the run
        public int expiresAfterRound;
    }

    private sealed class BlockRecord
    {
        public TrollEffectType type;
        public int expiresAfterRound = -1;
        public string sourceNameKey;
        public bool consumed;
    }

    private sealed class PressureReliefRecord
    {
        public int relief;
        public bool consumed;
        public int expiresAfterRound = -1;
    }

    private sealed class PendingRemovalRecord
    {
        public float chance;
        public string sourceNameKey;
        public bool consumed;
        public int expiresAfterRound = -1;
    }

    // one purchased charge that negates the next boss skill that would fire
    private sealed class SkillBlockCharge
    {
        public string sourceNameKey;
        public bool consumed;
    }

    // "no boss skills next round" purchases
    private sealed class SkillMuteRecord
    {
        public string sourceNameKey;
        public int untilRound;
        public bool consumed;
    }

    // run-long item perks (attention %, reveal, ui shield) for status display
    private sealed class RunPerkRecord
    {
        public EffectKind kind;
        public string sourceNameKey;
    }

    private readonly List<QueuedEffect> queuedEffects = new();
    private readonly List<BlockRecord> blockRecords = new();
    private readonly List<PressureReliefRecord> pressureReliefRecords = new();
    private readonly List<PendingRemovalRecord> pendingRemovalRecords = new();
    private readonly List<TrollEffectType> activeTrollEffects = new();
    private readonly List<SkillBlockCharge> skillBlockCharges = new();
    private readonly List<SkillMuteRecord> skillMuteRecords = new();
    private readonly List<RunPerkRecord> runPerkRecords = new();
    private RunStatsController statsController;
    private TetrisBoardController boardController;
    private RoundPiecePreviewView previewView;
    private bool hideBossIntentForRun;
    private int hideBossIntentUntilRound;
    private string hideIntentSourceNameKey;
    private int currentRoundIndex;
    private int currentBossPressure;
    private string currentBossNameKey;
    private bool hasBossPressure;
    // skill driver: which boss can interrupt this round, and the pacing clock
    private BossDefinition skillSourceBoss;
    private bool skillSourceIsBossRound;
    private float skillTimer;
    private bool revealTruePreview;
    private bool revealPieceTags;
    private bool blockUiCorruption;

    // item perk queries for the rest of the ui
    public bool HasTruePreview => revealTruePreview;
    public bool HasTagReveal => revealPieceTags;
    public bool HasUiCorruptionShield => blockUiCorruption;

    private void OnEnable()
    {
        GameEvents.OnLineCleared += HandleLineCleared;
        GameEvents.OnPieceLocked += HandlePieceLocked;
        GameEvents.OnShopOpened += HandleShopOpened;
        GameEvents.OnCorruptedLineCleared += HandleCorruptedLineCleared;
        GameEvents.OnRunStatsChanged += HandleRunStatsChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnLineCleared -= HandleLineCleared;
        GameEvents.OnPieceLocked -= HandlePieceLocked;
        GameEvents.OnShopOpened -= HandleShopOpened;
        GameEvents.OnCorruptedLineCleared -= HandleCorruptedLineCleared;
        GameEvents.OnRunStatsChanged -= HandleRunStatsChanged;
    }

    private void Update()
    {
        TickBossSkillDriver();
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
        hideIntentSourceNameKey = null;
        currentRoundIndex = 0;
        currentBossPressure = 0;
        currentBossNameKey = null;
        hasBossPressure = false;
        skillSourceBoss = null;
        skillSourceIsBossRound = false;
        skillTimer = 0f;
        revealTruePreview = false;
        revealPieceTags = false;
        blockUiCorruption = false;
        blockRecords.Clear();
        pressureReliefRecords.Clear();
        pendingRemovalRecords.Clear();
        activeTrollEffects.Clear();
        queuedEffects.Clear();
        skillBlockCharges.Clear();
        skillMuteRecords.Clear();
        runPerkRecords.Clear();
        ClearPreviewOverrides();
        PushPreviewPerks();
        NotifyEffectsChanged();
    }

    public void BeginRound(int roundIndex)
    {
        bool couldShowBossIntent = CanShowBossIntent;
        currentRoundIndex = Mathf.Max(0, roundIndex);
        bool changed = ExpireBlockRecords();
        changed |= ExpirePressureReliefRecords();
        changed |= ExpirePendingRemovalRecords();
        changed |= ExpireQueuedEffects();
        changed |= ConsumeExpiredSkillMutes();
        skillTimer = 0f;
        ClearPreviewOverrides();

        if (boardController != null)
            boardController.ResetRoundModifiers();

        if (statsController != null)
            statsController.ResetRoundAttentionModifier();

        if (changed || couldShowBossIntent != CanShowBossIntent)
            NotifyEffectsChanged();
    }

    public void EndRound()
    {
        bool changed = ClearBossEffects();
        skillSourceBoss = null;
        skillSourceIsBossRound = false;

        if (hasBossPressure)
        {
            hasBossPressure = false;
            currentBossPressure = 0;
            currentBossNameKey = null;
            changed = true;
        }

        if (changed)
            NotifyEffectsChanged();
    }

    // which boss may interrupt this round: the node boss on boss rounds,
    // the upcoming boss while you are still on search nodes
    public void SetSkillSource(BossDefinition boss, bool isBossRound)
    {
        skillSourceBoss = boss;
        skillSourceIsBossRound = isBossRound;
        skillTimer = 0f;
    }

    public bool AreSkillsMuted()
    {
        for (int i = 0; i < skillMuteRecords.Count; i++)
        {
            if (!skillMuteRecords[i].consumed && skillMuteRecords[i].untilRound >= currentRoundIndex)
                return true;
        }

        return false;
    }

    private void TickBossSkillDriver()
    {
        if (boardController == null || !boardController.IsBoardActive)
            return;

        if (skillSourceBoss == null || skillSourceBoss.skills == null || skillSourceBoss.skills.Count <= 0)
            return;

        if (AreSkillsMuted())
            return;

        skillTimer += Time.deltaTime;
        float interval = Mathf.Max(2f, skillSourceBoss.skillIntervalSeconds);

        if (skillTimer < interval)
            return;

        skillTimer = 0f;
        TryUseBossSkill();
    }

    private void TryUseBossSkill()
    {
        // bosses lurking from a future node interrupt less often than a present one
        float chance = skillSourceBoss.skillChance * (skillSourceIsBossRound ? 1f : 0.55f);

        if (statsController != null)
            chance *= statsController.BossActionMultiplier;

        if (UnityEngine.Random.value > Mathf.Clamp01(chance))
            return;

        FireRandomBossSkill();
    }

    // editor menu hook, fires a skill right now skipping interval and chance
    public void ForceBossSkillForDebug()
    {
        if (skillSourceBoss == null || skillSourceBoss.skills == null || skillSourceBoss.skills.Count <= 0)
        {
            Debug.Log("[BossSkill] no skill source bound, nothing to force");
            return;
        }

        FireRandomBossSkill();
    }

    private void FireRandomBossSkill()
    {
        EffectSpec skill = skillSourceBoss.skills[UnityEngine.Random.Range(0, skillSourceBoss.skills.Count)];

        if (skill == null || skill.kind == EffectKind.None)
            return;

        if (TryConsumeSkillBlockCharge())
        {
            GameEvents.BossSkillBlocked(skillSourceBoss.nameKey);
            NotifyEffectsChanged();
            return;
        }

        Execute(skill, Mathf.Clamp01(skill.chance), skillSourceBoss.nameKey, fromBoss: true);
        Debug.Log($"[BossSkill] {Loc.T(skillSourceBoss.nameKey)} fired {skill.kind} (bossRound={skillSourceIsBossRound})");
        GameEvents.BossSkillUsed(skillSourceBoss.nameKey, skill);
        ScreenShake.Trigger();
        NotifyEffectsChanged();
    }

    private bool TryConsumeSkillBlockCharge()
    {
        for (int i = 0; i < skillBlockCharges.Count; i++)
        {
            if (skillBlockCharges[i].consumed)
                continue;

            skillBlockCharges[i].consumed = true;
            return true;
        }

        return false;
    }

    private bool ConsumeExpiredSkillMutes()
    {
        bool changed = false;

        for (int i = 0; i < skillMuteRecords.Count; i++)
        {
            if (!skillMuteRecords[i].consumed && skillMuteRecords[i].untilRound < currentRoundIndex)
            {
                skillMuteRecords[i].consumed = true;
                changed = true;
            }
        }

        return changed;
    }

    // boss skills register fresh each node, previous boss effects are dropped
    public void RegisterBossEffects(BossDefinition boss)
    {
        bool changed = ClearBossEffects();
        hasBossPressure = false;
        currentBossPressure = 0;
        currentBossNameKey = null;

        if (boss != null)
        {
            currentBossNameKey = boss.nameKey;
            currentBossPressure = CalculateBossPressure(boss);
            hasBossPressure = true;
            MarkItemCountersForBossRound();
            changed = true;
        }

        if (boss != null && boss.effects != null)
        {
            for (int i = 0; i < boss.effects.Count; i++)
            {
                EffectSpec spec = boss.effects[i];

                if (spec == null || spec.kind == EffectKind.None)
                    continue;

                Apply(spec, boss.nameKey, fromBoss: true);

                if (spec.trollEffectType != TrollEffectType.None)
                    activeTrollEffects.Add(spec.trollEffectType);

                changed = true;
            }

            changed |= ConsumePendingRemovals();
        }

        if (changed)
            NotifyEffectsChanged();
    }

    public void QueuePressureReliefFromPurchase(ShopItemDefinition item, int attentionBeforePurchase)
    {
        if (item == null)
            return;

        float attentionScore = Mathf.Clamp01(attentionBeforePurchase / 140f);
        float composureScore = statsController != null ? Mathf.Clamp01((float)statsController.Composure / statsController.MaxComposure) : 0.5f;
        float spaceScore = boardController != null ? boardController.GetBoardSpareScore() : 0.5f;
        float stateScore = 0.4f * attentionScore + 0.3f * composureScore + 0.3f * spaceScore;
        float itemPotency = Mathf.Clamp01((item.baseAttentionCost + 25f) / 125f);
        int relief = Mathf.Clamp(Mathf.RoundToInt((5f + 25f * stateScore) * itemPotency), 3, 28);

        pressureReliefRecords.Add(new PressureReliefRecord
        {
            relief = relief,
            consumed = false,
            expiresAfterRound = -1
        });

        Debug.Log($"[Pressure] Item {Loc.T(item.nameKey)}: attention={attentionScore:0.00} composure={composureScore:0.00} space={spaceScore:0.00} potency={itemPotency:0.00} relief=-{relief}");

        NotifyEffectsChanged();
    }

    // single entry point for both shop purchases and boss registration
    // OnAcquire executes right away, everything else waits for its trigger
    public bool Apply(EffectSpec spec, string sourceNameKey, bool fromBoss = false)
    {
        if (spec == null || spec.kind == EffectKind.None)
            return true;

        if (spec.trigger == EffectTrigger.OnAcquire)
            return Execute(spec, GetEffectiveChance(spec, fromBoss), sourceNameKey, fromBoss);

        queuedEffects.Add(new QueuedEffect
        {
            spec = spec,
            sourceNameKey = sourceNameKey,
            fromBoss = fromBoss,
            status = EffectStatus.Active,
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

            float chance = GetEffectiveChance(queued);

            // for tag and remove kinds the chance IS the payload, don't gate on it
            if (!ChanceIsPayload(queued.spec.kind))
            {
                if (chance <= 0f)
                    continue;

                if (chance < 1f && UnityEngine.Random.value > chance)
                    continue;
            }

            // shop block items get their moment here, a blocked boss effect is skipped
            if (queued.fromBoss && queued.spec.trollEffectType != TrollEffectType.None && TryBlockTrollEffect(queued.spec.trollEffectType))
            {
                queued.status = EffectStatus.Blocked;
                NotifyEffectsChanged();
                continue;
            }

            Execute(queued.spec, chance, queued.sourceNameKey, queued.fromBoss);
        }
    }

    private static bool ChanceIsPayload(EffectKind kind)
    {
        return kind == EffectKind.CorruptPieces
            || kind == EffectKind.GlitchPieces
            || kind == EffectKind.RemoveRandomActiveTrollEffect;
    }

    // boss personality: calm bosses back off, hyped ones push harder, but clamped so its still fair
    private float ApplyMoodChance(EffectSpec spec)
    {
        float chance = spec.chance;

        switch (spec.mood)
        {
            case SkillMood.CalmAtHighNoise:
                if (statsController != null && statsController.NoiseTier >= NoiseTier.High)
                    chance *= 0.5f;
                break;
            case SkillMood.HypedByLongTakes:
                int longTakes = boardController != null ? boardController.GetLockedTypeCount(TetrominoType.I) : 0;
                chance *= Mathf.Min(1.6f, 1f + 0.15f * longTakes);
                break;
        }

        return Mathf.Clamp01(chance);
    }

    private float GetEffectiveChance(QueuedEffect queued)
    {
        return GetEffectiveChance(queued.spec, queued.fromBoss);
    }

    private float GetEffectiveChance(EffectSpec spec, bool fromBoss)
    {
        float chance = ApplyMoodChance(spec);

        if (fromBoss)
            chance *= GetBossPressureStrength();

        return Mathf.Clamp01(chance);
    }

    private float GetBossPressureStrength()
    {
        return hasBossPressure ? Mathf.Clamp01(currentBossPressure / 100f) : 1f;
    }

    private int ScaleMagnitude(EffectSpec spec, bool fromBoss)
    {
        if (!fromBoss || spec.magnitude == 0)
            return spec.magnitude;

        float strength = GetBossPressureStrength();

        if (strength <= 0f)
            return 0;

        int scaled = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(spec.magnitude) * strength));
        return spec.magnitude < 0 ? -scaled : scaled;
    }

    // what the preview panel is allowed to tell the player about incoming tags
    // UNFINISHED!!! WhichPieces and ExactOrder reveal need per piece rolls in the queue
    public void GetActiveTagSummaries(List<PieceTagSummary> results)
    {
        results.Clear();

        for (int i = 0; i < queuedEffects.Count; i++)
        {
            QueuedEffect queued = queuedEffects[i];

            if (queued.status != EffectStatus.Active)
                continue;

            EffectSpec spec = queued.spec;
            float chance = GetEffectiveChance(queued);

            if (chance <= 0f)
                continue;

            if (spec.kind == EffectKind.CorruptPieces)
                results.Add(new PieceTagSummary { tag = PieceTag.Corrupted, chance = chance, reveal = TagRevealMode.Summary });
            else if (spec.kind == EffectKind.GlitchPieces)
                results.Add(new PieceTagSummary { tag = PieceTag.Glitched, chance = chance, reveal = TagRevealMode.Summary });
        }
    }

    // everything the effects panel should show, queued boss skills plus item records
    public void GetActiveEffectInfos(List<ActiveEffectInfo> results)
    {
        results.Clear();

        for (int i = 0; i < queuedEffects.Count; i++)
        {
            QueuedEffect queued = queuedEffects[i];
            results.Add(new ActiveEffectInfo
            {
                infoKind = ActiveEffectInfoKind.Effect,
                spec = CreateDisplaySpec(queued),
                sourceNameKey = queued.sourceNameKey,
                fromBoss = queued.fromBoss,
                status = queued.status
            });
        }

        for (int i = 0; i < blockRecords.Count; i++)
        {
            BlockRecord record = blockRecords[i];
            results.Add(new ActiveEffectInfo
            {
                infoKind = ActiveEffectInfoKind.Effect,
                spec = new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.BlockTrollEffect, trollEffectType = record.type, chance = 1f },
                sourceNameKey = record.sourceNameKey,
                fromBoss = false,
                status = GetItemRecordStatus(record.consumed, record.expiresAfterRound)
            });
        }

        for (int i = 0; i < pendingRemovalRecords.Count; i++)
        {
            PendingRemovalRecord record = pendingRemovalRecords[i];
            results.Add(new ActiveEffectInfo
            {
                infoKind = ActiveEffectInfoKind.PendingRemoval,
                spec = new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.RemoveRandomActiveTrollEffect, chance = record.chance },
                sourceNameKey = record.sourceNameKey,
                fromBoss = false,
                status = record.consumed ? EffectStatus.Consumed : EffectStatus.Pending
            });
        }

        if (!CanShowBossIntent)
        {
            results.Add(new ActiveEffectInfo
            {
                infoKind = ActiveEffectInfoKind.Effect,
                spec = new EffectSpec { trigger = EffectTrigger.OnAcquire, kind = EffectKind.HideBossIntent },
                sourceNameKey = hideIntentSourceNameKey,
                fromBoss = false,
                status = EffectStatus.Active
            });
        }
    }

    private EffectSpec CreateDisplaySpec(QueuedEffect queued)
    {
        return new EffectSpec
        {
            trigger = queued.spec.trigger,
            kind = queued.spec.kind,
            trollEffectType = queued.spec.trollEffectType,
            magnitude = ScaleMagnitude(queued.spec, queued.fromBoss),
            chance = GetEffectiveChance(queued),
            durationRounds = queued.spec.durationRounds,
            mood = queued.spec.mood
        };
    }

    private static EffectStatus GetItemRecordStatus(bool consumed, int expiresAfterRound)
    {
        if (consumed)
            return EffectStatus.Consumed;

        return expiresAfterRound < 0 ? EffectStatus.Pending : EffectStatus.Active;
    }

    public void BlockTrollEffectNextRound(TrollEffectType effectType)
    {
        BlockTrollEffectNextRound(effectType, null);
    }

    public void BlockTrollEffectNextRound(TrollEffectType effectType, string sourceNameKey)
    {
        if (effectType == TrollEffectType.None)
            return;

        BlockRecord existing = FindBlockRecord(effectType);

        if (existing != null)
        {
            existing.expiresAfterRound = -1;
            existing.consumed = false;

            if (!string.IsNullOrEmpty(sourceNameKey))
                existing.sourceNameKey = sourceNameKey;
        }
        else
        {
            blockRecords.Add(new BlockRecord
            {
                type = effectType,
                expiresAfterRound = -1,
                sourceNameKey = sourceNameKey,
                consumed = false
            });
        }

        NotifyEffectsChanged();
    }

    public bool TryBlockTrollEffect(TrollEffectType effectType)
    {
        if (effectType == TrollEffectType.None)
            return false;

        BlockRecord record = FindBlockRecord(effectType);

        if (record == null)
            return false;

        // the block did its job, stays gray in the list but keeps blocking this round
        record.consumed = true;
        record.expiresAfterRound = currentRoundIndex;
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
        return TryRemoveRandomActiveTrollEffect(chance, null);
    }

    public bool TryRemoveRandomActiveTrollEffect(float chance, string sourceNameKey)
    {
        float safeChance = Mathf.Clamp01(chance);

        if (activeTrollEffects.Count <= 0)
        {
            pendingRemovalRecords.Add(new PendingRemovalRecord
            {
                chance = safeChance,
                sourceNameKey = sourceNameKey,
                consumed = false,
                expiresAfterRound = -1
            });
            NotifyEffectsChanged();
            return true;
        }

        if (!TryRemoveRandomActiveTrollEffectNow(safeChance))
            return false;

        pendingRemovalRecords.Add(new PendingRemovalRecord
        {
            chance = safeChance,
            sourceNameKey = sourceNameKey,
            consumed = true,
            expiresAfterRound = currentRoundIndex
        });
        NotifyEffectsChanged();
        return true;
    }

    private bool Execute(EffectSpec spec, float effectiveChance, string sourceNameKey, bool fromBoss)
    {
        int magnitude = ScaleMagnitude(spec, fromBoss);

        switch (spec.kind)
        {
            case EffectKind.CorruptPieces:
                boardController?.AddRoundTagChance(PieceTag.Corrupted, effectiveChance);
                return true;
            case EffectKind.GlitchPieces:
                boardController?.AddRoundTagChance(PieceTag.Glitched, effectiveChance);
                return true;
            case EffectKind.FallSpeedPercent:
                if (magnitude != 0)
                    boardController?.AddFallSpeedPercent(magnitude);
                return true;
            case EffectKind.AddNoise:
                if (magnitude != 0)
                    statsController?.ApplyNoiseDelta(magnitude);
                return true;
            case EffectKind.AddComposure:
                if (magnitude != 0)
                    statsController?.ApplyComposureDelta(magnitude);
                return true;
            case EffectKind.AddAttention:
                if (magnitude != 0)
                    statsController?.AddAttention(magnitude);
                return true;
            case EffectKind.HideNextPreview:
                // a fact-checked feed cannot be blanked out
                if (!revealTruePreview)
                    previewView?.SetHiddenOverride(true);
                return true;
            case EffectKind.FakeNextPreview:
                if (!revealTruePreview)
                    previewView?.SetCorruptedOverride(true);
                return true;
            case EffectKind.AddGarbageCell:
                if (magnitude > 0)
                    boardController?.AddGarbageCells(magnitude);
                return true;
            case EffectKind.BlockTrollEffect:
                BlockTrollEffectNextRound(spec.trollEffectType, sourceNameKey);
                return true;
            case EffectKind.RemoveRandomActiveTrollEffect:
                return TryRemoveRandomActiveTrollEffect(effectiveChance, sourceNameKey);
            case EffectKind.HideBossIntent:
                hideIntentSourceNameKey = sourceNameKey;

                if (spec.durationRounds < 0)
                    HideBossIntentForRun();
                else
                    HideBossIntentNextRound();

                return true;
            case EffectKind.SpawnTopGarbage:
                boardController?.AddGarbageCellsNearTop(Mathf.Max(1, magnitude));
                return true;
            case EffectKind.RotationLockSeconds:
                boardController?.LockRotation(Mathf.Max(1, magnitude));
                return true;
            case EffectKind.RemoveBottomLineNoReward:
                boardController?.RemoveBottomLineNoReward();
                return true;
            case EffectKind.AttentionGainPercent:
                if (statsController != null && magnitude != 0)
                {
                    // boss auras only last their round, item perks stick for the run
                    if (fromBoss)
                    {
                        statsController.AddRoundAttentionPercent(magnitude);
                    }
                    else
                    {
                        statsController.AddRunAttentionPercent(magnitude);
                        runPerkRecords.Add(new RunPerkRecord { kind = spec.kind, sourceNameKey = sourceNameKey });
                    }
                }

                return true;
            case EffectKind.BlockNextBossSkill:
                skillBlockCharges.Add(new SkillBlockCharge { sourceNameKey = sourceNameKey, consumed = false });
                NotifyEffectsChanged();
                return true;
            case EffectKind.DisableBossSkillsNextRound:
                skillMuteRecords.Add(new SkillMuteRecord { sourceNameKey = sourceNameKey, untilRound = currentRoundIndex + 1, consumed = false });
                NotifyEffectsChanged();
                return true;
            case EffectKind.RevealTruePreview:
                revealTruePreview = true;
                runPerkRecords.Add(new RunPerkRecord { kind = spec.kind, sourceNameKey = sourceNameKey });
                ClearPreviewOverrides();
                PushPreviewPerks();
                NotifyEffectsChanged();
                return true;
            case EffectKind.RevealPieceTags:
                revealPieceTags = true;
                runPerkRecords.Add(new RunPerkRecord { kind = spec.kind, sourceNameKey = sourceNameKey });
                PushPreviewPerks();
                NotifyEffectsChanged();
                return true;
            case EffectKind.BlockUiCorruption:
                blockUiCorruption = true;
                runPerkRecords.Add(new RunPerkRecord { kind = spec.kind, sourceNameKey = sourceNameKey });
                PushPreviewPerks();
                NotifyEffectsChanged();
                return true;
            default:
                return true;
        }
    }

    private void HandleRunStatsChanged(RunStatsController stats)
    {
        PushPreviewInterference(stats);
    }

    // high noise smears the preview unless a perk keeps the feed honest
    private void PushPreviewInterference(RunStatsController stats)
    {
        if (previewView == null || stats == null)
            return;

        int level = 0;

        if (!revealTruePreview)
        {
            if (stats.NoiseTier == NoiseTier.Critical)
                level = 2;
            else if (stats.NoiseTier == NoiseTier.High)
                level = 1;
        }

        previewView.SetNoiseInterference(level);
    }

    private void PushPreviewPerks()
    {
        if (previewView == null)
            return;

        previewView.SetOrderRevealOverride(revealPieceTags);

        if (statsController != null)
            PushPreviewInterference(statsController);
    }

    // hud paints each purchased item by what its effect is still worth
    public ItemPerkStatus GetItemStatus(string sourceNameKey)
    {
        if (string.IsNullOrEmpty(sourceNameKey))
            return ItemPerkStatus.Consumed;

        for (int i = 0; i < runPerkRecords.Count; i++)
        {
            if (runPerkRecords[i].sourceNameKey == sourceNameKey)
                return ItemPerkStatus.Persistent;
        }

        // run-long queued effects (round start composure, fall speed perks...) stay green
        for (int i = 0; i < queuedEffects.Count; i++)
        {
            QueuedEffect queued = queuedEffects[i];

            if (!queued.fromBoss && queued.sourceNameKey == sourceNameKey && queued.expiresAfterRound < 0)
                return ItemPerkStatus.Persistent;
        }

        for (int i = 0; i < skillBlockCharges.Count; i++)
        {
            if (skillBlockCharges[i].sourceNameKey == sourceNameKey && !skillBlockCharges[i].consumed)
                return ItemPerkStatus.Armed;
        }

        for (int i = 0; i < skillMuteRecords.Count; i++)
        {
            if (skillMuteRecords[i].sourceNameKey == sourceNameKey && !skillMuteRecords[i].consumed && skillMuteRecords[i].untilRound >= currentRoundIndex)
                return ItemPerkStatus.Armed;
        }

        for (int i = 0; i < blockRecords.Count; i++)
        {
            if (blockRecords[i].sourceNameKey == sourceNameKey && !blockRecords[i].consumed)
                return ItemPerkStatus.Armed;
        }

        for (int i = 0; i < pendingRemovalRecords.Count; i++)
        {
            if (pendingRemovalRecords[i].sourceNameKey == sourceNameKey && !pendingRemovalRecords[i].consumed)
                return ItemPerkStatus.Armed;
        }

        // nothing tracked anymore: instant stat items count as spent
        return ItemPerkStatus.Consumed;
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

    private void HandleCorruptedLineCleared(int lines)
    {
        // corrupted junk in the feed, clearing it still stirs things up
        statsController?.ApplyNoiseDelta(10 * lines);
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

    private int CalculateBossPressure(BossDefinition boss)
    {
        float noiseScore = statsController != null ? Mathf.Clamp01((float)statsController.Noise / statsController.MaxNoise) : 0f;
        float noiseMultiplier = 1f + 0.45f * noiseScore * noiseScore;
        int rawPressure = Mathf.RoundToInt(boss.basePressure * noiseMultiplier);
        int totalRelief = Mathf.Clamp(ConsumePendingPressureRelief(), 0, 55);
        int finalPressure = Mathf.Clamp(rawPressure - totalRelief, 15, 100);

        Debug.Log($"[Pressure] Boss {Loc.T(boss.nameKey)}: base={boss.basePressure} noise=x{noiseMultiplier:0.00} relief=-{totalRelief} final={finalPressure}");
        return finalPressure;
    }

    private int ConsumePendingPressureRelief()
    {
        int total = 0;

        for (int i = 0; i < pressureReliefRecords.Count; i++)
        {
            PressureReliefRecord record = pressureReliefRecords[i];

            if (record.consumed)
                continue;

            total += record.relief;
            record.consumed = true;
            record.expiresAfterRound = currentRoundIndex;
        }

        return total;
    }

    private void MarkItemCountersForBossRound()
    {
        for (int i = 0; i < blockRecords.Count; i++)
        {
            if (!blockRecords[i].consumed && blockRecords[i].expiresAfterRound < 0)
                blockRecords[i].expiresAfterRound = currentRoundIndex;
        }
    }

    private bool ConsumePendingRemovals()
    {
        bool changed = false;

        for (int i = 0; i < pendingRemovalRecords.Count; i++)
        {
            PendingRemovalRecord record = pendingRemovalRecords[i];

            if (record.consumed)
                continue;

            TryRemoveRandomActiveTrollEffectNow(record.chance);
            record.consumed = true;
            record.expiresAfterRound = currentRoundIndex;
            changed = true;
        }

        return changed;
    }

    private bool TryRemoveRandomActiveTrollEffectNow(float chance)
    {
        if (activeTrollEffects.Count <= 0)
            return false;

        if (UnityEngine.Random.value > Mathf.Clamp01(chance))
            return false;

        int index = UnityEngine.Random.Range(0, activeTrollEffects.Count);
        TrollEffectType removedType = activeTrollEffects[index];
        activeTrollEffects.RemoveAt(index);
        RemoveQueuedBossEffect(removedType);
        return true;
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

    private BlockRecord FindBlockRecord(TrollEffectType effectType)
    {
        for (int i = 0; i < blockRecords.Count; i++)
        {
            if (blockRecords[i].type == effectType)
                return blockRecords[i];
        }

        return null;
    }

    private bool ExpireBlockRecords()
    {
        bool changed = false;

        for (int i = blockRecords.Count - 1; i >= 0; i--)
        {
            if (blockRecords[i].expiresAfterRound >= 0 && blockRecords[i].expiresAfterRound < currentRoundIndex)
            {
                blockRecords.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    private bool ExpirePressureReliefRecords()
    {
        bool changed = false;

        for (int i = pressureReliefRecords.Count - 1; i >= 0; i--)
        {
            if (pressureReliefRecords[i].expiresAfterRound >= 0 && pressureReliefRecords[i].expiresAfterRound < currentRoundIndex)
            {
                pressureReliefRecords.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    private bool ExpirePendingRemovalRecords()
    {
        bool changed = false;

        for (int i = pendingRemovalRecords.Count - 1; i >= 0; i--)
        {
            if (pendingRemovalRecords[i].expiresAfterRound >= 0 && pendingRemovalRecords[i].expiresAfterRound < currentRoundIndex)
            {
                pendingRemovalRecords.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    private bool ExpireQueuedEffects()
    {
        bool changed = false;

        for (int i = queuedEffects.Count - 1; i >= 0; i--)
        {
            QueuedEffect queued = queuedEffects[i];

            if (queued.expiresAfterRound >= 0 && queued.expiresAfterRound < currentRoundIndex)
            {
                queuedEffects.RemoveAt(i);
                changed = true;
            }
        }

        return changed;
    }

    private void NotifyEffectsChanged()
    {
        EffectsChanged?.Invoke();
        GameEvents.TrollEffectsChanged();
    }
}
