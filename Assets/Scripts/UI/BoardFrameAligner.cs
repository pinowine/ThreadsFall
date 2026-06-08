using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class BoardFrameAligner : MonoBehaviour
{
    // keeps the UI frame glued to the world-space board
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private TetrisBoardController boardController;
    [SerializeField] private Vector2 paddingPixels = new(0f, 0f);
    [SerializeField] private bool alignInEditMode = true;

    private RectTransform frameRect;

    private void Awake()
    {
        CacheReferences();
        AlignToBoard();
    }

    private void OnEnable()
    {
        CacheReferences();
        AlignToBoard();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying || alignInEditMode)
            AlignToBoard();
    }

    public void AlignToBoard()
    {
        CacheReferences();

        if (frameRect == null || canvas == null || boardController == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;

        if (canvasRect == null)
            return;

        Camera cameraForWorld = worldCamera != null ? worldCamera : Camera.main;

        if (cameraForWorld == null)
            return;

        Vector3 worldMin = boardController.BoardWorldMin;
        Vector3 worldMax = boardController.BoardWorldMax;

        // convert board corners through screen space into the canvas' local plane
        Vector2 screenMin = cameraForWorld.WorldToScreenPoint(worldMin);
        Vector2 screenMax = cameraForWorld.WorldToScreenPoint(worldMax);

        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenMin, canvasCamera, out Vector2 localMin))
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenMax, canvasCamera, out Vector2 localMax))
            return;

        Vector2 min = Vector2.Min(localMin, localMax);
        Vector2 max = Vector2.Max(localMin, localMax);
        Vector2 center = (min + max) * 0.5f;
        Vector2 size = (max - min) + paddingPixels * 2f;

        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = center;
        frameRect.sizeDelta = size;
    }

    private void CacheReferences()
    {
        if (frameRect == null)
            frameRect = GetComponent<RectTransform>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
    }
}
