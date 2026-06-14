using System;

// every tetromino doubles as a social media trope
public enum PieceProperty
{
    LongTake,
    EchoChamber,
    BaitHook,
    Misdirection,
    Derail
}

[Flags]
public enum PieceTag
{
    None = 0,
    Corrupted = 1,
    Glitched = 2
}

// how much the preview is allowed to tell the player about incoming tags
// UNFINISHED!!! only Summary actually renders, WhichPieces / ExactOrder need
// per piece tag rolls moved into the queue first
public enum TagRevealMode
{
    Summary,
    WhichPieces,
    ExactOrder
}

[Serializable]
public struct PieceTagSummary
{
    public PieceTag tag;
    public float chance;
    public TagRevealMode reveal;
}

public static class PieceLore
{
    public static PieceProperty GetProperty(TetrominoType type)
    {
        return type switch
        {
            TetrominoType.I => PieceProperty.LongTake,
            TetrominoType.O => PieceProperty.EchoChamber,
            TetrominoType.T => PieceProperty.BaitHook,
            TetrominoType.S => PieceProperty.Misdirection,
            TetrominoType.Z => PieceProperty.Misdirection,
            TetrominoType.J => PieceProperty.Derail,
            TetrominoType.L => PieceProperty.Derail,
            _ => PieceProperty.EchoChamber
        };
    }

    public static string NameKey(PieceProperty property)
    {
        return "piece.prop." + ToLocSegment(property) + ".name";
    }

    public static string DescKey(PieceProperty property)
    {
        return "piece.prop." + ToLocSegment(property) + ".desc";
    }

    public static string TagNameKey(PieceTag tag)
    {
        return "piece.tag." + ToLocSegment(tag) + ".name";
    }

    public static string TagDescKey(PieceTag tag)
    {
        return "piece.tag." + ToLocSegment(tag) + ".desc";
    }

    private static string ToLocSegment(PieceProperty property)
    {
        return property switch
        {
            PieceProperty.LongTake => "long_take",
            PieceProperty.EchoChamber => "echo_chamber",
            PieceProperty.BaitHook => "bait_hook",
            PieceProperty.Misdirection => "misdirection",
            PieceProperty.Derail => "derail",
            _ => "unknown"
        };
    }

    private static string ToLocSegment(PieceTag tag)
    {
        return tag switch
        {
            PieceTag.Corrupted => "corrupted",
            PieceTag.Glitched => "glitched",
            _ => "unknown"
        };
    }
}
