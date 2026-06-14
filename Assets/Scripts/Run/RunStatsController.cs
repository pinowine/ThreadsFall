using System;
using UnityEngine;

public class RunStatsController : MonoBehaviour
{
    [Header("Initial Values")]
    [SerializeField] private int initialAttention;
    [SerializeField] private int initialNoise;
    [SerializeField] private int initialScore;

    [Header("Stat Limits")]
    [SerializeField] private int maxComposure = 100;
    [SerializeField] private int maxNoise = 100;

    [Header("Rewards")]
    [SerializeField] private int attentionPerClearedBlock = 4;
    [SerializeField] private int perfectClearComposureGain = 10;
    [SerializeField] private int mistakeComposureLoss = 10;
    [SerializeField] private int noiseGainPerRound = 5;

    public event Action<RunStatsController> StatsChanged;
    public event Action<bool> CollapseChanged;
    public event Action<NoiseTier> NoiseTierChanged;

    public int Attention { get; private set; }
    public int Composure { get; private set; }
    public int Noise { get; private set; }
    public int Score { get; private set; }
    public int TotalLinesCleared { get; private set; }
    public int CurrentRoundIndex { get; private set; }
    public int MaxComposure => Mathf.Max(1, maxComposure);
    public int MaxNoise => Mathf.Max(1, maxNoise);
    public bool IsCollapsed => Composure <= 0;
    public NoiseTier NoiseTier => GetNoiseTier(Noise);
    // noise pays out continuously: +10% income at 50 noise, +20% at 100
    public float AttentionCurrencyModifier => runAttentionPercentBonus + roundAttentionPercentBonus + Noise * NoiseAttentionBonusPerPoint;
    public float AttentionRewardMultiplier => attentionRewardMultiplier;
    // these query points let other systems react without owning stat math
    public bool CanPreviewPieces => !IsCollapsed;
    public float BossActionMultiplier => IsCollapsed ? 2f : NoiseTier switch
    {
        NoiseTier.Critical => 1.5f,
        NoiseTier.High => 1.25f,
        _ => 1f
    };
    public float ShopPriceMultiplier => IsCollapsed ? 1.5f : 1f;
    public bool ShouldDistortUiText => NoiseTier >= NoiseTier.High;
    public bool ShouldBlockUiInteraction => IsCollapsed || NoiseTier == NoiseTier.Critical;

    private const float NoiseAttentionBonusPerPoint = 0.002f;

    // run bonus comes from purchased perks, round bonus from the active boss aura
    private float runAttentionPercentBonus;
    private float roundAttentionPercentBonus;
    private float attentionRewardMultiplier = 1f;
    private bool hasMistakeSinceLastClear;

    private void Awake()
    {
        ResetRun();
    }

    public void ResetRun()
    {
        bool wasCollapsed = IsCollapsed;
        NoiseTier previousNoiseTier = NoiseTier;

        Attention = initialAttention;
        Composure = MaxComposure;
        Noise = Mathf.Clamp(initialNoise, 0, MaxNoise);
        Score = initialScore;
        TotalLinesCleared = 0;
        CurrentRoundIndex = 0;
        runAttentionPercentBonus = 0f;
        roundAttentionPercentBonus = 0f;
        attentionRewardMultiplier = 1f;
        hasMistakeSinceLastClear = false;

        RaiseStateTransitionEvents(wasCollapsed, previousNoiseTier);
        GameEvents.CoinsChanged(Attention);
        NotifyChanged();
    }

    public void SetCurrentRoundIndex(int roundIndex)
    {
        CurrentRoundIndex = Mathf.Max(0, roundIndex);
        NotifyChanged();
    }

    public int ApplyLineClearReward(int linesCleared, int clearedBlocks)
    {
        int safeLinesCleared = Mathf.Max(0, linesCleared);
        int safeClearedBlocks = Mathf.Max(0, clearedBlocks);

        if (safeLinesCleared <= 0 || safeClearedBlocks <= 0)
            return 0;

        bool wasCollapsed = IsCollapsed;
        NoiseTier previousNoiseTier = NoiseTier;

        TotalLinesCleared += safeLinesCleared;

        // currency uses cleared blocks, while score keeps the older line-clear rhythm
        int attentionReward = CalculateAttentionReward(safeClearedBlocks);
        Attention += attentionReward;
        Score += GetLineClearScore(safeLinesCleared);

        if (!hasMistakeSinceLastClear)
            Composure = Mathf.Clamp(Composure + Mathf.Max(0, perfectClearComposureGain), 0, MaxComposure);

        hasMistakeSinceLastClear = false;

        RaiseStateTransitionEvents(wasCollapsed, previousNoiseTier);
        GameEvents.CoinsChanged(Attention);
        NotifyChanged();
        return attentionReward;
    }

    public void ReportMistake(int composureLossOverride = -1)
    {
        // external systems define mistakes so input rules can evolve independently
        hasMistakeSinceLastClear = true;

        int loss = composureLossOverride >= 0 ? composureLossOverride : mistakeComposureLoss;
        ApplyComposureDelta(-Mathf.Max(0, loss));
    }

    public void AddAttention(int amount)
    {
        if (amount == 0)
            return;

        Attention = Mathf.Max(0, Attention + amount);
        GameEvents.CoinsChanged(Attention);
        NotifyChanged();
    }

    public bool TrySpendAttention(int cost)
    {
        int safeCost = Mathf.Max(0, cost);

        if (Attention < safeCost)
            return false;

        Attention -= safeCost;
        GameEvents.CoinsChanged(Attention);
        NotifyChanged();
        return true;
    }

    public void SetComposure(int composure)
    {
        SetComposureInternal(composure, notify: true);
    }

    public void ApplyComposureDelta(int delta)
    {
        SetComposureInternal(Composure + delta, notify: true);
    }

    public void ApplyRoundStartNoise()
    {
        ApplyRoundStartNoise(noiseGainPerRound);
    }

    public void ApplyRoundStartNoise(int noiseAmount)
    {
        ApplyNoiseDelta(noiseAmount);
    }

    public void AddRunAttentionPercent(int percent)
    {
        runAttentionPercentBonus = Mathf.Max(-1f, runAttentionPercentBonus + percent / 100f);
        NotifyChanged();
    }

    public void AddRoundAttentionPercent(int percent)
    {
        roundAttentionPercentBonus = Mathf.Max(-1f, roundAttentionPercentBonus + percent / 100f);
        NotifyChanged();
    }

    public void ResetRoundAttentionModifier()
    {
        if (Mathf.Approximately(roundAttentionPercentBonus, 0f))
            return;

        roundAttentionPercentBonus = 0f;
        NotifyChanged();
    }

    public void SetAttentionRewardMultiplier(float multiplier)
    {
        attentionRewardMultiplier = Mathf.Max(0f, multiplier);
        NotifyChanged();
    }

    public void AdjustComposure(int delta)
    {
        ApplyComposureDelta(delta);
    }

    public bool TrySpendComposure(int cost)
    {
        int safeCost = Mathf.Max(0, cost);

        if (Composure < safeCost)
            return false;

        ApplyComposureDelta(-safeCost);
        return true;
    }

    public void SetNoise(int noise)
    {
        SetNoiseInternal(noise, notify: true);
    }

    public void ApplyNoiseDelta(int delta)
    {
        SetNoiseInternal(Noise + delta, notify: true);
    }

    public void AdjustNoise(int delta)
    {
        ApplyNoiseDelta(delta);
    }

    public int GetModifiedShopCost(int baseCost)
    {
        int safeCost = Mathf.Max(0, baseCost);
        return Mathf.CeilToInt(safeCost * ShopPriceMultiplier);
    }

    private int CalculateAttentionReward(int clearedBlocks)
    {
        float modifier = Mathf.Max(0f, 1f + AttentionCurrencyModifier);
        float rawReward = clearedBlocks * attentionPerClearedBlock * modifier * attentionRewardMultiplier;

        // attention rewards are rounded once so all modifiers share the same final currency pass
        return Mathf.Max(0, Mathf.RoundToInt(rawReward));
    }

    private int GetLineClearScore(int linesCleared)
    {
        switch (linesCleared)
        {
            case 1:
                return 100;
            case 2:
                return 300;
            case 3:
                return 500;
            case 4:
                return 800;
            default:
                return 800;
        }
    }

    private void SetComposureInternal(int value, bool notify)
    {
        bool wasCollapsed = IsCollapsed;
        NoiseTier previousNoiseTier = NoiseTier;

        Composure = Mathf.Clamp(value, 0, MaxComposure);

        RaiseStateTransitionEvents(wasCollapsed, previousNoiseTier);

        if (notify)
            NotifyChanged();
    }

    private void SetNoiseInternal(int value, bool notify)
    {
        bool wasCollapsed = IsCollapsed;
        NoiseTier previousNoiseTier = NoiseTier;

        Noise = Mathf.Clamp(value, 0, MaxNoise);

        RaiseStateTransitionEvents(wasCollapsed, previousNoiseTier);

        if (notify)
            NotifyChanged();
    }

    private void RaiseStateTransitionEvents(bool wasCollapsed, NoiseTier previousNoiseTier)
    {
        if (wasCollapsed != IsCollapsed)
            CollapseChanged?.Invoke(IsCollapsed);

        if (previousNoiseTier != NoiseTier)
            NoiseTierChanged?.Invoke(NoiseTier);
    }

    private static NoiseTier GetNoiseTier(int noise)
    {
        if (noise >= 90)
            return NoiseTier.Critical;

        if (noise >= 70)
            return NoiseTier.High;

        if (noise >= 40)
            return NoiseTier.Elevated;

        return NoiseTier.Low;
    }

    private void NotifyChanged()
    {
        StatsChanged?.Invoke(this);
        GameEvents.RunStatsChanged(this);
    }
}
