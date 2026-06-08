using System.Collections.Generic;
using UnityEngine;

public class RoundPieceProvider : MonoBehaviour, IPieceProvider
{
    // keep the original list for previews while gameplay consumes the queue
    private readonly Queue<TetrominoType> queue = new();
    private readonly List<TetrominoType> originalRoundPieces = new();

    public IReadOnlyList<TetrominoType> OriginalRoundPieces => originalRoundPieces;
    public int RemainingCount => queue.Count;

    public void SetRoundPieces(IEnumerable<TetrominoType> pieces)
    {
        queue.Clear();
        originalRoundPieces.Clear();

        if (pieces == null)
            return;

        foreach (var piece in pieces)
        {
            queue.Enqueue(piece);
            originalRoundPieces.Add(piece);
        }
    }

    public void Clear()
    {
        queue.Clear();
        originalRoundPieces.Clear();
    }

    public bool TryGetNextPieceType(out TetrominoType type)
    {
        if (queue.Count <= 0)
        {
            type = default;
            return false;
        }

        type = queue.Dequeue();
        return true;
    }
}
