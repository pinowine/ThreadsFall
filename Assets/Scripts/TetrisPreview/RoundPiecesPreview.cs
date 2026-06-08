using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoundPiecePreviewView : MonoBehaviour
{
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private PieceTypeIconView iconPrefab;
    [SerializeField] private MonoBehaviour revealPolicyBehaviour;
    [SerializeField] private Vector2 gridCellSize = new(34f, 42f);
    [SerializeField] private Vector2 gridSpacing = new(3f, 3f);
    [SerializeField] private int gridPadding = 3;
    [SerializeField] private float previewBlockSize = 8f;

    private IPiecePreviewRevealPolicy revealPolicy;
    private readonly List<PieceTypeIconView> spawnedIcons = new();
    private GridLayoutGroup gridLayout;

    private void Awake()
    {
        revealPolicy = revealPolicyBehaviour as IPiecePreviewRevealPolicy;

        if (revealPolicy == null)
            Debug.LogWarning("Reveal policy missing. Add UniqueTypeRevealPolicy.");

        EnsureContainer();
        ConfigureGrid(1);
    }

    private void OnEnable()
    {
        EnsureContainer();
    }

    public void ShowRoundPieces(IReadOnlyList<TetrominoType> roundPieces)
    {
        Clear();

        if (revealPolicy == null)
            return;

        List<TetrominoType> visibleTypes = revealPolicy.GetVisiblePieceTypes(roundPieces);
        ConfigureGrid(visibleTypes.Count);

        foreach (var type in visibleTypes)
        {
            PieceTypeIconView icon = Instantiate(iconPrefab, iconRoot);
            RectTransform iconRect = icon.transform as RectTransform;

            if (iconRect != null)
            {
                iconRect.localScale = Vector3.one;
                iconRect.sizeDelta = gridCellSize;
            }

            icon.SetBlockSize(previewBlockSize);
            icon.Bind(type);
            spawnedIcons.Add(icon);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(iconRoot);
    }

    private void Clear()
    {
        foreach (var icon in spawnedIcons)
        {
            if (icon != null)
                Destroy(icon.gameObject);
        }

        spawnedIcons.Clear();
    }

    private void EnsureContainer()
    {
        if (iconRoot == null)
            return;

        if (!TryGetComponent(out RectMask2D _))
            gameObject.AddComponent<RectMask2D>();

        // the preview owns layout at runtime
        foreach (HorizontalOrVerticalLayoutGroup layout in iconRoot.GetComponents<HorizontalOrVerticalLayoutGroup>())
        {
            layout.enabled = false;
        }

        gridLayout = iconRoot.GetComponent<GridLayoutGroup>();

        if (gridLayout == null)
            gridLayout = iconRoot.gameObject.AddComponent<GridLayoutGroup>();
    }

    private void ConfigureGrid(int itemCount)
    {
        EnsureContainer();

        if (gridLayout == null || iconRoot == null)
            return;

        // force a fresh rect before choosing columns
        Canvas.ForceUpdateCanvases();
        int columns = CalculateColumnCount(itemCount);
        gridLayout.padding = new RectOffset(gridPadding, gridPadding, gridPadding, gridPadding);
        gridLayout.cellSize = gridCellSize;
        gridLayout.spacing = gridSpacing;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = columns;
    }

    private int CalculateColumnCount(int itemCount)
    {
        if (itemCount <= 0)
            return 1;

        // when the root has not been laid out yet, borrow the parent width as a good estimate
        float width = iconRoot.rect.width;

        if (width <= 0f && iconRoot.parent is RectTransform parentRect)
            width = parentRect.rect.width;

        float usableWidth = Mathf.Max(1f, width - gridPadding * 2f);
        int columns = Mathf.FloorToInt((usableWidth + gridSpacing.x) / (gridCellSize.x + gridSpacing.x));
        return Mathf.Clamp(columns, 1, itemCount);
    }
}
