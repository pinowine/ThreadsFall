using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// full screen jolt used when a boss fires a skill, shakes the camera and every ui canvas
public class ScreenShake : MonoBehaviour
{
    private static ScreenShake instance;

    private readonly List<RectTransform> shakenRects = new();
    private readonly List<Vector2> rectOriginalPositions = new();
    private Coroutine shakeRoutine;
    private Camera shakenCamera;
    private Vector3 cameraOriginalPosition;

    public static void Trigger(float duration = 0.35f, float uiMagnitude = 8f, float worldMagnitude = 0.12f)
    {
        if (instance == null)
        {
            GameObject host = new("Screen Shake");
            instance = host.AddComponent<ScreenShake>();
        }

        instance.StartShake(duration, uiMagnitude, worldMagnitude);
    }

    private void StartShake(float duration, float uiMagnitude, float worldMagnitude)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            RestoreTargets();
        }

        CaptureTargets();
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, uiMagnitude, worldMagnitude));
    }

    private void CaptureTargets()
    {
        shakenRects.Clear();
        rectOriginalPositions.Clear();

        shakenCamera = Camera.main;

        if (shakenCamera != null)
            cameraOriginalPosition = shakenCamera.transform.localPosition;

        // overlay canvases ignore the camera, so their top level panels get nudged directly
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas)
                continue;

            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                if (canvas.transform.GetChild(i) is RectTransform childRect && childRect.gameObject.activeSelf)
                {
                    shakenRects.Add(childRect);
                    rectOriginalPositions.Add(childRect.anchoredPosition);
                }
            }
        }
    }

    private IEnumerator ShakeRoutine(float duration, float uiMagnitude, float worldMagnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float falloff = 1f - Mathf.Clamp01(elapsed / duration);
            Vector2 unit = Random.insideUnitCircle;

            if (shakenCamera != null)
                shakenCamera.transform.localPosition = cameraOriginalPosition + (Vector3)(unit * (worldMagnitude * falloff));

            for (int i = 0; i < shakenRects.Count; i++)
            {
                if (shakenRects[i] != null)
                    shakenRects[i].anchoredPosition = rectOriginalPositions[i] + unit * (uiMagnitude * falloff);
            }

            yield return null;
        }

        RestoreTargets();
        shakeRoutine = null;
    }

    private void RestoreTargets()
    {
        if (shakenCamera != null)
            shakenCamera.transform.localPosition = cameraOriginalPosition;

        for (int i = 0; i < shakenRects.Count; i++)
        {
            if (shakenRects[i] != null)
                shakenRects[i].anchoredPosition = rectOriginalPositions[i];
        }

        shakenRects.Clear();
        rectOriginalPositions.Clear();
    }
}
