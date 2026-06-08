using System;

[Serializable]
public class RoundTrainingLogEntry
{
    public string roundId;
    public int pieceIndex;
    public float boardHeightRatio;
    public float holesRatio;
    public int bumpiness;
    public int deepestWell;
    public int piecesSinceLastClear;
    public float hardDropRate;
    public float softDropRate;
    public float rotationRate;
    public float averageTimeToPlace;
    public float currentNoise;
    public int currentComposure;
    public float roundProgress;
    public bool playerLostWithinNextNPieces;
    public float manualPressureLabel;
}
