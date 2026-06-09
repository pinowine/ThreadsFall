using UnityEngine;

public enum RunStatIconKind
{
    Attention,
    Composure,
    Noise
}

[CreateAssetMenu(fileName = "RunUiArtCatalog", menuName = "Threads Fall/Art/Run UI Art Catalog")]
public class RunUiArtCatalog : ScriptableObject
{
    private const string DefaultResourcePath = "Data/RunUiArtCatalog";
    private static RunUiArtCatalog cachedDefault;

    public SpriteSequenceDefinition attentionIcon;
    public SpriteSequenceDefinition composureIcon;
    public SpriteSequenceDefinition noiseIcon;
    public SpriteSequenceDefinition lockIcon;
    public SpriteSequenceDefinition shopkeeperAvatar;
    public SpriteSequenceDefinition searchNodeIcon;
    public SpriteSequenceDefinition miniBossNodeIcon;
    public SpriteSequenceDefinition finalBossNodeIcon;
    public Sprite leftPanelBackground;

    public static RunUiArtCatalog ResolveDefault()
    {
        if (cachedDefault == null)
            cachedDefault = Resources.Load<RunUiArtCatalog>(DefaultResourcePath);

        return cachedDefault;
    }

    public SpriteSequenceDefinition GetStatSequence(RunStatIconKind kind)
    {
        return kind switch
        {
            RunStatIconKind.Composure => composureIcon,
            RunStatIconKind.Noise => noiseIcon,
            _ => attentionIcon
        };
    }

    public Sprite GetStatSprite(RunStatIconKind kind)
    {
        return GetStatSequence(kind)?.FirstFrame;
    }

    public SpriteSequenceDefinition GetProgressNodeSequence(RunNodeType nodeType)
    {
        return nodeType switch
        {
            RunNodeType.FinalBoss => finalBossNodeIcon,
            RunNodeType.MiniBoss => miniBossNodeIcon,
            _ => searchNodeIcon
        };
    }

    public Sprite GetProgressNodeSprite(RunNodeType nodeType)
    {
        return GetProgressNodeSequence(nodeType)?.FirstFrame;
    }
}
