using UnityEngine;

public class RandomPieceProvider : MonoBehaviour, IPieceProvider
{
    public bool TryGetNextPieceType(out TetrominoType type)
    {
        int count = System.Enum.GetValues(typeof(TetrominoType)).Length;
        type = (TetrominoType)Random.Range(0, count);
        return true;
    }
}