using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpriteSequence", menuName = "Threads Fall/Art/Sprite Sequence")]
public class SpriteSequenceDefinition : ScriptableObject
{
    public string sequenceId;
    public float framesPerSecond = 8f;
    public bool loop = true;
    public List<Sprite> frames = new();

    public Sprite FirstFrame => frames != null && frames.Count > 0 ? frames[0] : null;
    public bool HasAnimation => frames != null && frames.Count > 1;
}
