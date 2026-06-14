using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BossDefinition", menuName = "Threads Fall/Boss Definition")]
public class BossDefinition : ScriptableObject
{
    public string bossKey;
    public BossId bossId;
    public RunNodeType nodeType = RunNodeType.MiniBoss;
    public string nameKey;
    public string commentKey;
    public string effectKey = "boss.effect.none";
    public SpriteSequenceDefinition avatarSequence;
    public float routeWeight = 1f;
    [Range(0, 100)] public int basePressure = 60;
    // passive aura, registered with the effect system when this boss's node starts
    public List<EffectSpec> effects = new();
    // active skills, fired live during rounds (also normal rounds while this boss is next up)
    public List<EffectSpec> skills = new();
    // how often the boss considers using a skill, and how willing it is when it does
    public float skillIntervalSeconds = 8f;
    [Range(0f, 1f)] public float skillChance = 0.5f;
    // piece properties this boss likes to throw at you, weighted into the round bag
    public List<PieceProperty> preferredPieces = new();
    public float preferredPieceBias = 2.5f;

    public bool IsValid => !string.IsNullOrWhiteSpace(bossKey) && bossId != BossId.None;
}
