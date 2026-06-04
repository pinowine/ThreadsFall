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
    TetrominoType GetNextPieceType();
}

public static class TetrominoShape
{
    public static Vector2Int[] GetCells(TetrominoType type)
    {
        switch (type)
        {
            case TetrominoType.I:
                return new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 0)
                };

            case TetrominoType.O:
                return new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1)
                };

            case TetrominoType.T:
                return new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1)
                };

            case TetrominoType.S:
                return new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 1),
                    new Vector2Int(0, 1)
                };

            case TetrominoType.Z:
                return new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 1)
                };

            case TetrominoType.J:
                return new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 1)
                };

            case TetrominoType.L:
                return new[]
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0),
                    new Vector2Int(1, 1)
                };

            default:
                return new[] { Vector2Int.zero };
        }
    }

    public static Vector2Int RotateCell(Vector2Int cell, bool clockwise)
    {
        if (clockwise)
            return new Vector2Int(cell.y, -cell.x);

        return new Vector2Int(-cell.y, cell.x);
    }

    public static bool CanRotate(TetrominoType type)
    {
        return type != TetrominoType.O;
    }
}