using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopTooltipView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text specialEffectText;
    [SerializeField] private RunUiArtCatalog uiArtCatalog;

    private readonly List<ShopEffectIconRowView> effectRows = new();
    private RectTransform rectTransform;
    private RectTransform effectRowsRoot;
    private Canvas canvas;
    private int activeEffectRowCount;

    private void Awake()
    {
        EnsureUi();
        Hide();
    }

    public void Show(ShopItemDefinition item, Vector2 screenPosition, bool strikeTriggeredEffects = false)
    {
        if (item == null)
            return;

        EnsureUi();

        titleText.text = Loc.T(item.nameKey);
        string effectSummary = Loc.T(item.effectSummaryKey);
        bodyText.text = Loc.T(item.descriptionKey);
        RefreshEffectRows(item, strikeTriggeredEffects);
        specialEffectText.text = strikeTriggeredEffects ? "<s>" + effectSummary + "</s>" : effectSummary;
        gameObject.SetActive(true);
        MoveTo(screenPosition);
    }

    public void MoveTo(Vector2 screenPosition)
    {
        if (!gameObject.activeSelf)
            return;

        EnsureUi();
        RectTransform parentRect = rectTransform.parent as RectTransform;

        if (parentRect == null)
            return;

        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, camera, out Vector2 localPoint))
            return;

        Vector2 halfSize = rectTransform.sizeDelta * 0.5f;
        Rect parentBounds = parentRect.rect;
        const float margin = 12f;
        float leftX = localPoint.x - halfSize.x - margin;
        float rightX = localPoint.x + halfSize.x + margin;

        Vector2 anchoredPosition = new(
            leftX >= parentBounds.xMin + margin ? leftX : rightX,
            localPoint.y - 8f
        );

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, parentBounds.xMin + halfSize.x, parentBounds.xMax - halfSize.x);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, parentBounds.yMin + halfSize.y, parentBounds.yMax - halfSize.y);
        rectTransform.anchoredPosition = anchoredPosition;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void EnsureUi()
    {
        if (rectTransform != null)
            return;

        rectTransform = gameObject.GetComponent<RectTransform>();

        if (rectTransform == null)
            rectTransform = gameObject.AddComponent<RectTransform>();

        canvas = GetComponentInParent<Canvas>();
        Image background = gameObject.GetComponent<Image>();

        if (background == null)
            background = gameObject.AddComponent<Image>();

        background.color = UiTheme.TooltipBg;
        background.raycastTarget = false;
        rectTransform.sizeDelta = new Vector2(184f, 186f);
        gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

        VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();

        if (layout == null)
            layout = gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(10, 10, 9, 9);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        titleText = CreateText("Tooltip Title", UiTheme.Heading, FontStyles.Bold);
        bodyText = CreateText("Tooltip Body", UiTheme.Body, FontStyles.Normal);
        effectRowsRoot = CreateEffectRowsRoot();
        specialEffectText = CreateText("Tooltip Special Effect", UiTheme.Body, FontStyles.Normal);
    }

    private void RefreshEffectRows(ShopItemDefinition item, bool strikeTriggeredEffects)
    {
        RunUiArtCatalog artCatalog = ResolveUiArtCatalog();
        activeEffectRowCount = 0;
        SetEffectRowCount(0);

        if (item.baseAttentionCost > 0)
            AddEffectRow(artCatalog != null ? artCatalog.GetStatSprite(RunStatIconKind.Attention) : null, "-" + item.baseAttentionCost, false);

        if (item.setComposureToMax)
            AddEffectRow(artCatalog != null ? artCatalog.GetStatSprite(RunStatIconKind.Composure) : null, Loc.T("shop.effect.value.max"), strikeTriggeredEffects);

        if (item.composureDelta != 0)
            AddEffectRow(artCatalog != null ? artCatalog.GetStatSprite(RunStatIconKind.Composure) : null, FormatSignedValue(item.composureDelta), strikeTriggeredEffects);

        if (item.setNoiseToZero)
            AddEffectRow(artCatalog != null ? artCatalog.GetStatSprite(RunStatIconKind.Noise) : null, "0", strikeTriggeredEffects);

        if (item.noiseDelta != 0)
            AddEffectRow(artCatalog != null ? artCatalog.GetStatSprite(RunStatIconKind.Noise) : null, FormatSignedValue(item.noiseDelta), strikeTriggeredEffects);
    }

    private void AddEffectRow(Sprite icon, string value, bool strike)
    {
        int rowIndex = activeEffectRowCount;
        SetEffectRowCount(rowIndex + 1);
        effectRows[rowIndex].Bind(icon, value, strike);
        activeEffectRowCount++;
    }

    private void SetEffectRowCount(int count)
    {
        while (effectRows.Count < count)
        {
            GameObject rowObject = new("Effect Icon Row", typeof(RectTransform));
            rowObject.transform.SetParent(effectRowsRoot, false);
            effectRows.Add(rowObject.AddComponent<ShopEffectIconRowView>());
        }

        for (int i = 0; i < effectRows.Count; i++)
        {
            effectRows[i].gameObject.SetActive(i < count);
        }
    }

    private RectTransform CreateEffectRowsRoot()
    {
        GameObject rootObject = new("Tooltip Effect Rows", typeof(RectTransform));
        rootObject.transform.SetParent(transform, false);

        VerticalLayoutGroup layout = rootObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        LayoutElement layoutElement = rootObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 18f;
        layoutElement.preferredHeight = 54f;
        layoutElement.flexibleWidth = 1f;

        return (RectTransform)rootObject.transform;
    }

    private string FormatSignedValue(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }

    private RunUiArtCatalog ResolveUiArtCatalog()
    {
        if (uiArtCatalog == null)
            uiArtCatalog = RunUiArtCatalog.ResolveDefault();

        return uiArtCatalog;
    }

    private TMP_Text CreateText(string objectName, int fontSize, FontStyles style)
    {
        GameObject textObject = new(objectName, typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, fontSize, style, UiTheme.TextInverse);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;

        return text;
    }
}

public class ShopEffectIconRowView : MonoBehaviour
{
    private Image iconImage;
    private TMP_Text valueText;

    private void Awake()
    {
        EnsureUi();
    }

    public void Bind(Sprite icon, string value, bool strike)
    {
        EnsureUi();
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        valueText.text = strike ? "<s>" + value + "</s>" : value;
    }

    private void EnsureUi()
    {
        if (iconImage != null && valueText != null)
            return;

        HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
            layout = gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        iconImage = CreateIcon();
        valueText = CreateValueText();
    }

    private Image CreateIcon()
    {
        GameObject iconObject = new("Icon", typeof(RectTransform));
        iconObject.transform.SetParent(transform, false);

        Image image = iconObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        LayoutElement layoutElement = iconObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = 14f;
        layoutElement.preferredWidth = 14f;
        layoutElement.minHeight = 14f;
        layoutElement.preferredHeight = 14f;
        layoutElement.flexibleWidth = 0f;
        return image;
    }

    private TMP_Text CreateValueText()
    {
        GameObject textObject = new("Value", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, UiTheme.Body, FontStyles.Normal, UiTheme.TextInverse);
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;
        return text;
    }
}
