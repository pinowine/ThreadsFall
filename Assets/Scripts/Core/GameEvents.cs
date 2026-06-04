using System;

public static class GameEvents
{
    public static Action<int> OnCoinsChanged;
    public static Action<int> OnRoundChanged;
    public static Action<BossId> OnBossChanged;
    public static Action<int> OnLineCleared;
    public static Action<string, TrollStyle, int> OnBossComment;
    public static Action OnFakePreviewTriggered;
    public static Action<float> OnNextPreviewHidden;
    public static Action OnShopOpened;
    public static Action OnPieceLocked;
    public static Action OnBoardChanged;
    public static Action<TrollEffectType> OnTrollEffectBlocked;
    public static Action OnTrollEffectsChanged;

    public static void CoinsChanged(int coins)
    {
        OnCoinsChanged?.Invoke(coins);
    }

    public static void RoundChanged(int roundIndex)
    {
        OnRoundChanged?.Invoke(roundIndex);
    }

    public static void BossChanged(BossId bossId)
    {
        OnBossChanged?.Invoke(bossId);
    }

    public static void LineCleared(int lines)
    {
        OnLineCleared?.Invoke(lines);
    }

    public static void BossComment(string textKey, TrollStyle style, int intensity)
    {
        OnBossComment?.Invoke(textKey, style, intensity);
    }

    public static void FakePreviewTriggered()
    {
        OnFakePreviewTriggered?.Invoke();
    }

    public static void NextPreviewHidden(float seconds)
    {
        OnNextPreviewHidden?.Invoke(seconds);
    }

    public static void ShopOpened()
    {
        OnShopOpened?.Invoke();
    }

    public static void PieceLocked()
    {
        OnPieceLocked?.Invoke();
    }

    public static void BoardChanged()
    {
        OnBoardChanged?.Invoke();
    }

    public static void TrollEffectBlocked(TrollEffectType effectType)
    {
        OnTrollEffectBlocked?.Invoke(effectType);
    }

    public static void TrollEffectsChanged()
    {
        OnTrollEffectsChanged?.Invoke();
    }
}
