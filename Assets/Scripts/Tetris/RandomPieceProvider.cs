using UnityEngine;

public class RandomPieceProvider : MonoBehaviour, IPieceProvider
{
    public TetrominoType GetNextPieceType()
    {
        int count = System.Enum.GetValues(typeof(TetrominoType)).Length;
        return (TetrominoType)Random.Range(0, count);
    }
}