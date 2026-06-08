using System;
using System.Collections;
using UnityEngine;

public class ShutterPanelSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private RectTransform shutterOverlay;
    [SerializeField] private float closeDuration = 0.18f;
    [SerializeField] private float openDuration = 0.22f;

    public event Action<GameObject> SwitchCompleted;

    public bool IsSwitching => routine != null;

    private Coroutine routine;
    // used to ignore completion from a switch interrupted by a newer one
    private int switchVersion;

    private void Awake()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (bossPanel != null)
            bossPanel.SetActive(true);

        SetShutterScale(0f);
    }

    public void SwitchToShop()
    {
        StartSwitch(shopPanel, bossPanel);
    }

    public void SwitchToBoss()
    {
        StartSwitch(bossPanel, shopPanel);
    }

    private void StartSwitch(GameObject show, GameObject hide)
    {
        if (routine != null)
            StopCoroutine(routine);

        switchVersion++;
        routine = StartCoroutine(SwitchRoutine(show, hide, switchVersion));
    }

    private IEnumerator SwitchRoutine(GameObject show, GameObject hide, int version)
    {
        yield return AnimateShutter(0f, 1f, closeDuration);

        if (hide != null)
            hide.SetActive(false);

        if (show != null)
            show.SetActive(true);

        yield return AnimateShutter(1f, 0f, openDuration);

        if (version != switchVersion)
            yield break;

        SetShutterScale(0f);
        routine = null;
        SwitchCompleted?.Invoke(show);
    }

    private IEnumerator AnimateShutter(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetShutterScale(to);
            yield break;
        }

        float t = 0f;

        while (t < duration)
        {
            // UI transitions should keep moving even if gameplay time is paused later.
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);

            float eased = EaseInOut(p);
            SetShutterScale(Mathf.Lerp(from, to, eased));

            yield return null;
        }

        SetShutterScale(to);
    }

    private void SetShutterScale(float y)
    {
        if (shutterOverlay == null)
            return;

        Vector3 scale = shutterOverlay.localScale;
        scale.y = y;
        shutterOverlay.localScale = scale;
    }

    private float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
