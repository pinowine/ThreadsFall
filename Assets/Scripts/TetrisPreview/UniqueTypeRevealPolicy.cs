using System.Collections.Generic;
using UnityEngine;

public class UniqueTypeRevealPolicy : MonoBehaviour, IPiecePreviewRevealPolicy
{
    public PiecePreviewRevealMode RevealMode => PiecePreviewRevealMode.UniqueTypes;

    public List<TetrominoType> GetVisiblePieceTypes(IReadOnlyList<TetrominoType> roundPieces)
    {
        HashSet<TetrominoType> set = new();

        foreach (var piece in roundPieces)
            set.Add(piece);

        List<TetrominoType> result = new(set);
        // stable enum order keeps the preview from shuffling between rounds
        result.Sort();

        return result;
    }
}
