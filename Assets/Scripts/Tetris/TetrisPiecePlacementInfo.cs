public readonly struct TetrisPiecePlacementInfo
{
    // captures player input around one lock before the board starts the next piece
    public TetrominoType PieceType { get; }
    public float TimeToPlace { get; }
    public bool UsedHardDrop { get; }
    public int MoveInputCount { get; }
    public int SoftDropInputCount { get; }
    public int RotationInputCount { get; }
    public int LinesCleared { get; }

    public TetrisPiecePlacementInfo(
        TetrominoType pieceType,
        float timeToPlace,
        bool usedHardDrop,
        int moveInputCount,
        int softDropInputCount,
        int rotationInputCount,
        int linesCleared)
    {
        PieceType = pieceType;
        TimeToPlace = timeToPlace;
        UsedHardDrop = usedHardDrop;
        MoveInputCount = moveInputCount;
        SoftDropInputCount = softDropInputCount;
        RotationInputCount = rotationInputCount;
        LinesCleared = linesCleared;
    }
}
