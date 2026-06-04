using System;

public enum BossId
{
    None,
    FalseHelper,
    Spammer,
    Algorithm
}

public enum TrollStyle
{
    Sarcasm,
    Bait,
    Spam,
    Manipulation,
    FactCheck,
    Neutral
}

public enum TrollTrigger
{
    RoundStart,
    LineClear,
    HighStack,
    WaitingForIPiece,
    Misdrop,
    FakePreviewSuccess,
    BossPhaseStart,
    ShopEnter
}

public enum TrollEffectType
{
    None,
    FakeNextPreview,
    AddGarbageCell,
    HideNextPreview,
    IncreaseNoise,
    ReduceTrust
}

public enum PlayerState
{
    Stable,
    Panic,
    Greedy,
    Recovering,
    Dominating,
    Misled
}

public enum UpgradeEffectKind
{
    None,
    LineClearCoinBonus,
    MultiLineCoinBonus,
    BlockFakeNextPreview
}

public static class GameEnumParser
{
    public static T Parse<T>(string value, T fallback) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        T parsed;
        return Enum.TryParse(value, true, out parsed) ? parsed : fallback;
    }

    public static string ToLocSegment(this BossId bossId)
    {
        switch (bossId)
        {
            case BossId.FalseHelper:
                return "false_helper";
            case BossId.Spammer:
                return "spammer";
            case BossId.Algorithm:
                return "algorithm";
            default:
                return "none";
        }
    }
}
