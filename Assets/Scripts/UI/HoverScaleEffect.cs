using UnityEngine;
using UnityEngine.EventSystems;

// little zoom on hover so its obvious which element the cursor is on
public class HoverScaleEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.12f;
    [SerializeField] private float lerpSpeed = 14f;

    private Vector3 baseScale = Vector3.one;
    private float targetScale = 1f;
    private bool baseScaleCaptured;

    public void SetHoverScale(float scale)
    {
        hoverScale = Mathf.Max(1f, scale);
    }

    private void OnEnable()
    {
        CaptureBaseScale();
        targetScale = 1f;
        transform.localScale = baseScale;
    }

    private void OnDisable()
    {
        // never leave a zoomed ghost behind when the row gets hidden mid hover
        if (baseScaleCaptured)
            transform.localScale = baseScale;

        targetScale = 1f;
    }

    private void Update()
    {
        if (!baseScaleCaptured)
            return;

        Vector3 desired = baseScale * targetScale;

        if ((transform.localScale - desired).sqrMagnitude < 0.00001f)
        {
            transform.localScale = desired;
            return;
        }

        transform.localScale = Vector3.Lerp(transform.localScale, desired, Time.unscaledDeltaTime * lerpSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CaptureBaseScale();
        targetScale = hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = 1f;
    }

    private void CaptureBaseScale()
    {
        if (baseScaleCaptured)
            return;

        baseScale = transform.localScale;

        // a layout pass can momentarily zero scales, never lock that in as the base
        if (baseScale.sqrMagnitude < 0.0001f)
            baseScale = Vector3.one;

        baseScaleCaptured = true;
    }
}
