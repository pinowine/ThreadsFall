using System;

public enum BossId
{
    None,
    FalseHelper,
    Spammer,
    Algorithm
}

public enum RunGameState
{
    RunStart,
    RoundPreparation,
    BossPresentation,
    RoundActive,
    RoundResolution,
    IntermissionShop,
    SpecialEvent,
    RunComplete,
    GameOver
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
        // keep localization keys stable
        return bossId switch
        {
            BossId.FalseHelper => "false_helper",
            BossId.Spammer => "spammer",
            BossId.Algorithm => "algorithm",
            _ => "none",
        };
    }
}
