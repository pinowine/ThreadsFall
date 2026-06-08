public readonly struct TetrisBoardMetrics
{
    // compact values that are cheap to serialize into training logs
    public float BoardHeightRatio { get; }
    public float HolesRatio { get; }
    public int Bumpiness { get; }
    public int DeepestWell { get; }

    public TetrisBoardMetrics(float boardHeightRatio, float holesRatio, int bumpiness, int deepestWell)
    {
        BoardHeightRatio = boardHeightRatio;
        HolesRatio = holesRatio;
        Bumpiness = bumpiness;
        DeepestWell = deepestWell;
    }
}
