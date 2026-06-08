using System.Collections.Generic;

public enum PiecePreviewRevealMode
{
    // extra modes are here so content can grow without changing the policy contract
    UniqueTypes,
    Counts,
    FirstOrderedPieces,
    IPiecePresence,
    BossCorrupted
}

public interface IPiecePreviewRevealPolicy
{
    PiecePreviewRevealMode RevealMode { get; }
    List<TetrominoType> GetVisiblePieceTypes(IReadOnlyList<TetrominoType> roundPieces);
}
