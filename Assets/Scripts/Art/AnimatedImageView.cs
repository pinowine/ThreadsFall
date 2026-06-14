using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class AnimatedImageView : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private SpriteSequenceDefinition sequence;
    [SerializeField] private bool playOnEnable;
    // off when the art should stretch to fill its frame
    [SerializeField] private bool preserveAspect = true;

    private float frameTimer;
    private int frameIndex;
    private bool playing;
    private bool playLoop;

    private void Awake()
    {
        EnsureImage();
        ShowFirstFrame();
    }

    private void OnEnable()
    {
        ShowFirstFrame();

        if (playOnEnable)
            Play(loop: sequence == null || sequence.loop);
    }

    private void Update()
    {
        if (!playing || sequence == null || !sequence.HasAnimation)
            return;

        float frameDuration = 1f / Mathf.Max(1f, sequence.framesPerSecond);
        frameTimer += Time.unscaledDeltaTime;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            AdvanceFrame();
        }
    }

    public void SetSequence(SpriteSequenceDefinition nextSequence, bool startPlaying = false)
    {
        EnsureImage();
        sequence = nextSequence;
        frameTimer = 0f;
        frameIndex = 0;
        ShowFrame(frameIndex);

        if (startPlaying && sequence != null && sequence.HasAnimation)
            Play(loop: sequence.loop);
        else
            playing = false;
    }

    public void Play(bool loop = true)
    {
        if (sequence == null || !sequence.HasAnimation)
            return;

        playLoop = loop;
        playing = true;
    }

    public void PlayOnceFromStart()
    {
        if (sequence == null || !sequence.HasAnimation)
            return;

        frameIndex = 0;
        frameTimer = 0f;
        ShowFrame(frameIndex);
        playLoop = false;
        playing = true;
    }

    public void StopAtFirstFrame()
    {
        playing = false;
        frameTimer = 0f;
        frameIndex = 0;
        ShowFrame(frameIndex);
    }

    private void AdvanceFrame()
    {
        frameIndex++;

        if (frameIndex >= sequence.frames.Count)
        {
            if (!playLoop)
            {
                frameIndex = sequence.frames.Count - 1;
                playing = false;
            }
            else
            {
                frameIndex = 0;
            }
        }

        ShowFrame(frameIndex);
    }

    private void ShowFirstFrame()
    {
        frameIndex = 0;
        ShowFrame(frameIndex);
    }

    private void ShowFrame(int index)
    {
        EnsureImage();

        if (sequence == null || sequence.frames == null || sequence.frames.Count <= 0)
        {
            targetImage.sprite = null;
            return;
        }

        int safeIndex = Mathf.Clamp(index, 0, sequence.frames.Count - 1);
        targetImage.sprite = sequence.frames[safeIndex];
        targetImage.color = Color.white;
        targetImage.preserveAspect = preserveAspect;
    }

    public void SetPreserveAspect(bool preserve)
    {
        preserveAspect = preserve;

        if (targetImage != null)
            targetImage.preserveAspect = preserve;
    }

    private void EnsureImage()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
    }
}

public class HoverAnimatedImageTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private AnimatedImageView animatedImage;

    private void Awake()
    {
        if (animatedImage == null)
            animatedImage = GetComponent<AnimatedImageView>();
    }

    // so a bigger hover area (like a whole hud row) can drive someone else's animation
    public void Bind(AnimatedImageView target)
    {
        animatedImage = target;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        animatedImage?.Play(loop: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        animatedImage?.StopAtFirstFrame();
    }
}
