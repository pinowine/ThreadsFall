using UnityEngine;

public enum TetrominoType
{
    I,
    O,
    T,
    S,
    Z,
    J,
    L
}

public interface IPieceProvider
{
    bool TryGetNextPieceType(out TetrominoType type);
}

public static class TetrominoShape
{
    // cells are relative to a simple pivot; the board controller owns the world position.
    public static Vector2Int[] GetCells(TetrominoType type)
    {
        return type switch
        {
            TetrominoType.I => new[]
                            {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 0)
                },
            TetrominoType.O => new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1)
                },
            TetrominoType.T => new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1)
                },
            TetrominoType.S => new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 1),
                    new Vector2Int(0, 1)
                },
            TetrominoType.Z => new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1)
                },
            TetrominoType.J => new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 1)
                },
            TetrominoType.L => new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(1, 1)
                },
            _ => new[] { Vector2Int.zero },
        };
    }

    public static Vector2Int RotateCell(Vector2Int cell, bool clockwise)
    {
        // basic rotation for now!!
        if (clockwise)
            return new Vector2Int(cell.y, -cell.x);

        return new Vector2Int(-cell.y, cell.x);
    }

    public static bool CanRotate(TetrominoType type)
    {
        return type != TetrominoType.O;
    }
}
