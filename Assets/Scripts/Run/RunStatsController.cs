using System;
using UnityEngine;

public class RunStatsController : MonoBehaviour
{
    [Header("Initial Values")]
    [SerializeField] private int initialAttention;
    [SerializeField] private int initialComposure = 5;
    [SerializeField] private int initialNoise;
    [SerializeField] private int initialScore;

    public event Action<RunStatsController> StatsChanged;

    public int Attention { get; private set; }
    public int Composure { get; private set; }
    public int Noise { get; private set; }
    public int Score { get; private set; }
    public int TotalLinesCleared { get; private set; }
    public int CurrentRoundIndex { get; private set; }

    private void Awake()
    {
        ResetRun();
    }

    public void ResetRun()
    {
        Attention = initialAttention;
        Composure = initialComposure;
        Noise = initialNoise;
        Score = initialScore;
        TotalLinesCleared = 0;
        CurrentRoundIndex = 0;

        NotifyChanged();
    }

    public void SetCurrentRoundIndex(int roundIndex)
    {
        CurrentRoundIndex = Mathf.Max(0, roundIndex);
        NotifyChanged();
    }

    public void ApplyLineClearReward(int linesCleared)
    {
        if (linesCleared <= 0)
            return;

        TotalLinesCleared += linesCleared;

        GetLineClearReward(linesCleared, out int attentionReward, out int scoreReward);

        Attention += attentionReward;
        Score += scoreReward;

        GameEvents.CoinsChanged(Attention);
        NotifyChanged();
    }

    public void SetComposure(int composure)
    {
        Composure = Mathf.Max(0, composure);
        NotifyChanged();
    }

    public void SetNoise(int noise)
    {
        Noise = Mathf.Max(0, noise);
        NotifyChanged();
    }

    private void GetLineClearReward(int linesCleared, out int attentionReward, out int scoreReward)
    {
        // attention is acting as the spendable reward for the shop layer
        switch (linesCleared)
        {
            case 1:
                attentionReward = 2;
                scoreReward = 100;
                break;
            case 2:
                attentionReward = 5;
                scoreReward = 300;
                break;
            case 3:
                attentionReward = 8;
                scoreReward = 500;
                break;
            case 4:
                attentionReward = 14;
                scoreReward = 800;
                break;
            default:
                attentionReward = 14;
                scoreReward = 800;
                break;
        }
    }

    private void NotifyChanged()
    {
        StatsChanged?.Invoke(this);
        GameEvents.RunStatsChanged(this);
    }
}
