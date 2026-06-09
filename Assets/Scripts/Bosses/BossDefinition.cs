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
    // executable skills, registered with the effect system when this boss's node starts
    public List<EffectSpec> effects = new();

    public bool IsValid => !string.IsNullOrWhiteSpace(bossKey) && bossId != BossId.None;
}
