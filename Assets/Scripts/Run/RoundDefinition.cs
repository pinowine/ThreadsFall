using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RoundDefinition
{
    public string roundId;
    public string displayNameKey;
    public BossId bossId;
    // author the exact piece script for this round in order
    public List<TetrominoType> pieces = new();
    public int targetLines;
    public int targetScore;
    public int rewardAttention;
}
