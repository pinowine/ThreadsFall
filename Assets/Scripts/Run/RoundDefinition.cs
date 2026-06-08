using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RoundDefinition
{
    public string roundId;
    public string displayNameKey;
    public BossId bossId;
    // Author the exact piece script for this round in order.
    public List<TetrominoType> pieces = new();
    public int targetLines;
    public int targetScore;
    public int rewardAttention;

    // Optional supervised target for training exports; leave at 0 when the round is not annotated.
    [Range(0f, 1f)]
    public float manualPressureLabel;
}
