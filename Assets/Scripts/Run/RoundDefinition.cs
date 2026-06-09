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

public sealed class RuntimeRunNode
{
    public RuntimeRunNode(
        string nodeId,
        int nodeIndex,
        RunNodeType nodeType,
        BossId bossId,
        BossDefinition bossDefinition,
        string displayNameKey,
        int pieceCount,
        List<TetrominoType> pieces)
    {
        NodeId = nodeId;
        NodeIndex = nodeIndex;
        NodeType = nodeType;
        BossId = bossId;
        BossDefinition = bossDefinition;
        DisplayNameKey = displayNameKey;
        PieceCount = pieceCount;
        Pieces = pieces ?? new List<TetrominoType>();
    }

    public string NodeId { get; }
    public int NodeIndex { get; }
    public RunNodeType NodeType { get; }
    public BossId BossId { get; }
    public BossDefinition BossDefinition { get; }
    public string DisplayNameKey { get; }
    public int PieceCount { get; }
    public IReadOnlyList<TetrominoType> Pieces { get; }

    public bool IsBossNode => NodeType != RunNodeType.Normal;
}
