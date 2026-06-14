using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundPiecePreviewView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private PieceTypeIconView iconPrefab;
    [SerializeField] private MonoBehaviour revealPolicyBehaviour;
    [SerializeField] private Vector2 gridCellSize = new(34f, 42f);
    [SerializeField] private Vector2 gridSpacing = new(3f, 3f);
    [SerializeField] private int gridPadding = 3;
    [SerializeField] private float previewBlockSize = 8f;

    private IPiecePreviewRevealPolicy revealPolicy;
    private readonly List<PieceTypeIconView> spawnedIcons = new();
    private readonly List<TetrominoType> lastRoundPieces = new();
    private readonly List<PieceTagSummary> incomingTags = new();
    private readonly List<TMP_Text> tagRows = new();
    private RectTransform tagsRoot;
    private TMP_Text tagsTitleText;
    private RunProgressTooltipView tooltipView;
    private GridLayoutGroup gridLayout;
    // effect system overrides, hidden blanks the panel and corrupted lies about types
    private bool hiddenOverride;
    private bool corruptedOverride;
    // noise smear: 1 hides some icons behind static, 2 blanks the whole shelf
    private int noiseInterference;
    // item perk: show the true full piece order instead of the unique-types digest
    private bool orderRevealOverride;

    private static readonly TetrominoType[] AllTypes =
    {
        TetrominoType.I,
        TetrominoType.O,
        TetrominoType.T,
        TetrominoType.S,
        TetrominoType.Z,
        TetrominoType.J,
        TetrominoType.L
    };

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
        Loc.OnLocaleChanged += HandleLocaleChanged;
        EnsureContainer();
        RefreshTitle();
    }

    private void OnDisable()
    {
        Loc.OnLocaleChanged -= HandleLocaleChanged;
    }

    public void ShowRoundPieces(IReadOnlyList<TetrominoType> roundPieces)
    {
        lastRoundPieces.Clear();

        if (roundPieces != null)
            lastRoundPieces.AddRange(roundPieces);

        Render();
    }

    public void SetHiddenOverride(bool hidden)
    {
        if (hiddenOverride == hidden)
            return;

        hiddenOverride = hidden;

        if (hidden)
            GameEvents.NextPreviewHidden(0f);

        RefreshFromCache();
    }

    public void SetCorruptedOverride(bool corrupted)
    {
        if (corruptedOverride == corrupted)
            return;

        corruptedOverride = corrupted;
        RefreshFromCache();
    }

    public void SetNoiseInterference(int level)
    {
        int clamped = Mathf.Clamp(level, 0, 2);

        if (noiseInterference == clamped)
            return;

        noiseInterference = clamped;
        RefreshFromCache();
    }

    public void SetOrderRevealOverride(bool reveal)
    {
        if (orderRevealOverride == reveal)
            return;

        orderRevealOverride = reveal;
        RefreshFromCache();
    }

    private void RefreshFromCache()
    {
        if (lastRoundPieces.Count > 0)
            Render();
    }

    private void Render()
    {
        Clear();

        if (revealPolicy == null && !orderRevealOverride)
            return;

        // hidden wins, the player just gets an empty shelf this round
        // critical noise drowns the panel the same way
        if (hiddenOverride || noiseInterference >= 2)
            return;

        // the reveal perk shows the honest full order, no digest, no lies
        List<TetrominoType> visibleTypes = orderRevealOverride
            ? new List<TetrominoType>(lastRoundPieces)
            : revealPolicy.GetVisiblePieceTypes(lastRoundPieces);

        if (corruptedOverride && !orderRevealOverride)
            CorruptVisibleTypes(visibleTypes);

        ConfigureGrid(visibleTypes.Count);

        for (int i = 0; i < visibleTypes.Count; i++)
        {
            TetrominoType type = visibleTypes[i];
            PieceTypeIconView icon = Instantiate(iconPrefab, iconRoot);
            RectTransform iconRect = icon.transform as RectTransform;

            if (iconRect != null)
            {
                iconRect.localScale = Vector3.one;
                iconRect.sizeDelta = gridCellSize;
            }

            icon.SetBlockSize(previewBlockSize);

            // high noise eats some entries, only static remains where a piece was
            bool obscured = noiseInterference >= 1 && !orderRevealOverride && (i % 2 == 1 || Random.value < 0.2f);

            if (obscured)
            {
                icon.BindObscured();
            }
            else
            {
                icon.Bind(type);
                AttachHoverHint(icon, type);
            }

            icon.gameObject.AddComponent<HoverScaleEffect>();
            spawnedIcons.Add(icon);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(iconRoot);
    }

    // hovering an icon explains what the lore name actually means
    private void AttachHoverHint(PieceTypeIconView icon, TetrominoType type)
    {
#pragma warning disable UNT0026 // GetComponent always allocates
        Image hoverPlate = icon.GetComponent<Image>();
#pragma warning restore UNT0026 // GetComponent always allocates

        if (hoverPlate == null)
            hoverPlate = icon.gameObject.AddComponent<Image>();

        hoverPlate.color = Color.clear;
        hoverPlate.raycastTarget = true;

#pragma warning disable UNT0026 // GetComponent always allocates
        RunProgressHoverTarget hover = icon.GetComponent<RunProgressHoverTarget>();
#pragma warning restore UNT0026 // GetComponent always allocates

        if (hover == null)
            hover = icon.gameObject.AddComponent<RunProgressHoverTarget>();

        hover.Bind(PieceLore.DescKey(PieceLore.GetProperty(type)), tooltipView);
    }

    public void SetIncomingTags(IReadOnlyList<PieceTagSummary> tags)
    {
        incomingTags.Clear();

        if (tags != null)
            incomingTags.AddRange(tags);

        RenderTags();
    }

    private void RenderTags()
    {
        EnsureTagsUi();

        // header only earns its spot when something is actually incoming
        if (tagsTitleText != null)
        {
            tagsTitleText.text = Loc.T("preview.tags.title");
            tagsTitleText.gameObject.SetActive(incomingTags.Count > 0);
        }

        while (tagRows.Count < incomingTags.Count)
        {
            tagRows.Add(CreateTagRow());
        }

        for (int i = 0; i < tagRows.Count; i++)
        {
            bool active = i < incomingTags.Count;
            tagRows[i].gameObject.SetActive(active);

            if (!active)
                continue;

            PieceTagSummary summary = incomingTags[i];
            // UNFINISHED!!! only the summary line for now, WhichPieces / ExactOrder reveal later
            tagRows[i].text = Loc.T(PieceLore.TagNameKey(summary.tag)) + " " + Mathf.RoundToInt(Mathf.Clamp01(summary.chance) * 100f) + "%";
            RunProgressHoverTarget hover = tagRows[i].GetComponent<RunProgressHoverTarget>();

            if (hover != null)
                hover.Bind(PieceLore.TagDescKey(summary.tag), tooltipView);
        }
    }

    private TMP_Text CreateTagRow()
    {
        GameObject rowObject = new("Tag Row", typeof(RectTransform));
        rowObject.transform.SetParent(tagsRoot, false);

        TextMeshProUGUI text = rowObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, UiTheme.Small, FontStyles.Normal, UiTheme.AccentDanger);
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = true;

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 8f;
        layoutElement.preferredHeight = 8f;

        rowObject.AddComponent<RunProgressHoverTarget>();
        return text;
    }

    private void CorruptVisibleTypes(List<TetrominoType> visibleTypes)
    {
        if (visibleTypes.Count <= 0)
            return;

        // a lie needs types the round does not actually contain
        List<TetrominoType> absentTypes = new();

        foreach (TetrominoType type in AllTypes)
        {
            if (!lastRoundPieces.Contains(type))
                absentTypes.Add(type);
        }

        if (absentTypes.Count <= 0)
            return;

        int swaps = Mathf.Min(2, Mathf.Min(visibleTypes.Count, absentTypes.Count));

        for (int i = 0; i < swaps; i++)
        {
            int targetIndex = Random.Range(0, visibleTypes.Count);
            int absentIndex = Random.Range(0, absentTypes.Count);
            visibleTypes[targetIndex] = absentTypes[absentIndex];
            absentTypes.RemoveAt(absentIndex);
        }

        visibleTypes.Sort();
        GameEvents.FakePreviewTriggered();
    }

    private void Clear()
    {
        foreach (var icon in spawnedIcons)
        {
            if (icon != null)
                Destroy(icon.gameObject);
        }

        spawnedIcons.Clear();

        // icon under the cursor might just have died, don't strand the hint
        if (tooltipView != null)
            tooltipView.Hide();
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

        // icons keep the top, tags strip lives at the bottom
        EnsureTitle();
        iconRoot.anchorMin = new Vector2(0f, 0.26f);
        iconRoot.anchorMax = new Vector2(1f, 0.86f);
        iconRoot.offsetMin = Vector2.zero;
        iconRoot.offsetMax = Vector2.zero;
        EnsureTagsUi();
    }

    private void EnsureTitle()
    {
        if (titleText == null)
        {
            Transform existing = transform.Find("Pieces Title");

            if (existing != null)
                titleText = existing.GetComponent<TMP_Text>();
        }

        if (titleText == null)
        {
            GameObject titleObject = new("Pieces Title", typeof(RectTransform));
            titleObject.transform.SetParent(transform, false);
            titleText = titleObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.05f, 0.86f);
        titleRect.anchorMax = new Vector2(0.95f, 0.98f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        UiTheme.Style(titleText, UiTheme.Label, FontStyles.Bold, UiTheme.TextInverse, autoSize: true);
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.raycastTarget = false;
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        if (titleText != null)
            titleText.text = Loc.T("ui.preview.title");
    }

    private TMP_Text CreateSectionTitle(string objectName, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject titleObject = new(objectName, typeof(RectTransform));
        titleObject.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)titleObject.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = titleObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, UiTheme.Label, FontStyles.Bold, UiTheme.TextInverse, autoSize: true);
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private void EnsureTagsUi()
    {
        if (tooltipView == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform tooltipParent = canvas != null ? canvas.transform : transform;
            GameObject tooltipObject = new("Preview Tooltip", typeof(RectTransform));
            tooltipObject.transform.SetParent(tooltipParent, false);
            tooltipView = tooltipObject.AddComponent<RunProgressTooltipView>();
            tooltipView.Hide();
        }

        if (tagsTitleText == null)
        {
            tagsTitleText = CreateSectionTitle("Tags Title", new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.26f));
            tagsTitleText.gameObject.SetActive(false);
        }

        if (tagsRoot == null)
        {
            GameObject rootObject = new("Tags Root", typeof(RectTransform));
            rootObject.transform.SetParent(transform, false);
            tagsRoot = (RectTransform)rootObject.transform;
            tagsRoot.anchorMin = new Vector2(0.05f, 0.02f);
            tagsRoot.anchorMax = new Vector2(0.95f, 0.15f);
            tagsRoot.offsetMin = Vector2.zero;
            tagsRoot.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 1f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
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

    private void HandleLocaleChanged(string locale)
    {
        RefreshTitle();
    }
}
