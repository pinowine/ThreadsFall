using System;
using System.Collections;
using UnityEngine;

public class ShutterPanelSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private RectTransform shutterOverlay;
    [SerializeField] private float completionDelay = 0.24f;

    public event Action<GameObject> SwitchCompleted;

    public bool IsSwitching => routine != null;

    private Coroutine routine;
    private int switchVersion;

    private void Awake()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (bossPanel != null)
            bossPanel.SetActive(true);

        HideOverlay();
    }

    public void SetShutterTarget(RectTransform target)
    {
        HideOverlay();
    }

    public void SwitchToShop()
    {
        StartSwitch(shopPanel);
    }

    public void SwitchToBoss()
    {
        StartSwitch(bossPanel);
    }

    private void StartSwitch(GameObject shownContext)
    {
        if (routine != null)
            StopCoroutine(routine);

        HideOverlay();
        switchVersion++;
        routine = StartCoroutine(CompleteAfterAvatarSwap(shownContext, switchVersion));
    }

    private IEnumerator CompleteAfterAvatarSwap(GameObject shownContext, int version)
    {
        float delay = Mathf.Max(0f, completionDelay);

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return null;

        if (version != switchVersion)
            yield break;

        routine = null;
        SwitchCompleted?.Invoke(shownContext);
    }

    private void HideOverlay()
    {
        if (shutterOverlay == null)
            return;

        shutterOverlay.localScale = Vector3.zero;
        shutterOverlay.gameObject.SetActive(false);
    }
}
