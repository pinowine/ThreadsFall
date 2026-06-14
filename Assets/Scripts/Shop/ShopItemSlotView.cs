using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopItemSlotView : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image costIconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button lockButton;
    [SerializeField] private TMP_Text lockButtonText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buyButtonText;
    [SerializeField] private TMP_Text cannotBuyPopupText;
    // slot bg removed, rows float on the screen now (raycast still works at alpha 0)
    [SerializeField] private Color emptyColor = new(0f, 0f, 0f, 0f);
    [SerializeField] private Color filledColor = new(0f, 0f, 0f, 0f);
    [SerializeField] private Color lockedColor = new(0f, 0f, 0f, 0f);
    [SerializeField] private Color buyButtonColor = new(0.92f, 0.92f, 0.88f, 1f);
    [SerializeField] private Color buyButtonFailureColor = new(1f, 0.2f, 0.18f, 1f);
    [SerializeField] private RunUiArtCatalog uiArtCatalog;

    private RunShopController shopController;
    private ShopTooltipView tooltipView;
    private ShopItemHoverTarget nameHoverTarget;
    private Image buyButtonImage;
    private Image lockButtonImage;
    private RectTransform lockShackleRect;
    private Image lockBodyImage;
    private Image lockShackleImage;
    private Coroutine cannotBuyRoutine;
    private int slotIndex;
    private ShopItemDefinition currentItem;

    private void Awake()
    {
        EnsureUi();
    }

    public void Bind(RunShopController controller, int index, ShopTooltipView tooltip)
    {
        EnsureUi();
        shopController = controller;
        slotIndex = index;
        tooltipView = tooltip;
        Refresh();
    }

    public void Bind(RunShopController controller, int index, bool isReserveSlot, ShopTooltipView tooltip)
    {
        Bind(controller, index, tooltip);
    }

    public void Refresh()
    {
        EnsureUi();
        currentItem = shopController?.GetShelfItem(slotIndex);
        bool hasItem = currentItem != null;
        bool isLocked = hasItem && shopController != null && shopController.IsShelfSlotLocked(slotIndex);

        backgroundImage.color = isLocked ? lockedColor : hasItem ? filledColor : emptyColor;
        iconImage.enabled = true;
        iconImage.sprite = hasItem ? currentItem.icon : null;
        iconImage.color = hasItem && currentItem.icon != null ? Color.white : new Color(0.45f, 0.48f, 0.52f, 1f);
        nameText.text = hasItem ? Loc.T(currentItem.nameKey) : Loc.T("shop.slot.empty");
        costIconImage.enabled = hasItem;
#pragma warning disable UNT0008 // Null propagation on Unity objects
        costIconImage.sprite = ResolveUiArtCatalog()?.GetStatSprite(RunStatIconKind.Attention);
#pragma warning restore UNT0008 // Null propagation on Unity objects
        costText.text = hasItem ? shopController.GetModifiedCost(currentItem).ToString() : Loc.T("ui.empty");
        buyButton.interactable = hasItem;
        buyButton.gameObject.SetActive(hasItem);
        buyButtonText.text = Loc.T("shop.buy");
        lockButton.gameObject.SetActive(hasItem);
        RefreshLockIcon(isLocked);
        nameHoverTarget.Bind(hasItem ? currentItem : null, tooltipView, false);
    }

    private void Buy()
    {
        if (shopController == null || currentItem == null)
            return;

        bool bought = shopController.TryBuyShelfSlot(slotIndex);

        if (!bought)
            ShowCannotBuyFeedback();
    }

    private void ToggleLock()
    {
        if (shopController == null || currentItem == null)
            return;

        shopController.ToggleShelfLock(slotIndex);
    }

    private void EnsureUi()
    {
#pragma warning disable UNT0026 // GetComponent always allocates
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
#pragma warning restore UNT0026 // GetComponent always allocates

        if (rectTransform == null)
            rectTransform = gameObject.AddComponent<RectTransform>();

        rectTransform.sizeDelta = new Vector2(0f, 20f);

        if (backgroundImage == null)
            backgroundImage = gameObject.GetComponent<Image>();

        if (backgroundImage == null)
            backgroundImage = gameObject.AddComponent<Image>();

        backgroundImage.raycastTarget = true;

        // zoom feedback so the hovered shelf row pops out
        if (gameObject.GetComponent<HoverScaleEffect>() == null)
            gameObject.AddComponent<HoverScaleEffect>().SetHoverScale(1.06f);

        HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
            layout = gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.padding = new RectOffset(3, 3, 2, 2);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (iconImage == null)
            iconImage = CreateImage("Icon", new Vector2(16f, 16f));

        SetFixedLayout(iconImage.gameObject, 16f, 16f);

        RectTransform textColumn = ResolveTextColumn();
        ConfigureTextColumn(textColumn);

        if (nameText == null)
            nameText = CreateText("Name", textColumn, 8, FontStyles.Bold, 9f);

        ConfigureText(nameText, 8f, 9f, FontStyles.Bold);

        nameHoverTarget = nameText.GetComponent<ShopItemHoverTarget>();

        if (nameHoverTarget == null)
            nameHoverTarget = nameText.gameObject.AddComponent<ShopItemHoverTarget>();

        RectTransform costRow = ResolveCostRow(textColumn);
        ConfigureCostRow(costRow);

        if (costIconImage == null)
            costIconImage = CreateInlineIcon("Cost Icon", costRow, new Vector2(7f, 7f));

        SetFixedLayout(costIconImage.gameObject, 7f, 7f);

        if (costText == null)
        {
            costText = CreateText("Cost", costRow, 7, FontStyles.Normal, 8f);
        }
        else if (costText.transform.parent != costRow)
        {
            costText.transform.SetParent(costRow, false);
        }

        ConfigureText(costText, 7f, 8f, FontStyles.Normal);
        costText.raycastTarget = false;

        if (lockButton == null)
            lockButton = CreateSmallButton("Lock Button", ToggleLock, 18f);

        SetFixedLayout(lockButton.gameObject, 18f, 16f);

        if (lockButtonImage == null && lockButton != null)
            lockButtonImage = lockButton.GetComponent<Image>();

        if (lockButtonImage != null)
            lockButtonImage.color = new Color(0.86f, 0.86f, 0.8f, 1f);

        if (lockButtonText == null && lockButton != null)
            lockButtonText = lockButton.GetComponentInChildren<TMP_Text>();

        ConfigureButtonText(lockButtonText);
        EnsureLockIcon();

        if (buyButton == null)
            buyButton = CreateSmallButton("Buy Button", Buy, 30f);

        SetFixedLayout(buyButton.gameObject, 30f, 16f);

        if (buyButtonImage == null && buyButton != null)
            buyButtonImage = buyButton.GetComponent<Image>();

        if (buyButtonImage != null)
            buyButtonImage.color = buyButtonColor;

        if (buyButtonText == null && buyButton != null)
            buyButtonText = buyButton.GetComponentInChildren<TMP_Text>();

        ConfigureButtonText(buyButtonText);

        if (cannotBuyPopupText == null)
            cannotBuyPopupText = CreateCannotBuyPopup();

        cannotBuyPopupText.transform.parent.gameObject.SetActive(false);
    }

    private RectTransform ResolveTextColumn()
    {
        if (nameText != null && nameText.transform.parent is RectTransform nameParent)
            return nameParent;

        if (costText != null && costText.transform.parent is RectTransform costParent)
            return costParent;

        Transform existing = transform.Find("Text Column");

        if (existing is RectTransform existingColumn)
            return existingColumn;

        return CreateTextColumn();
    }

    private Image CreateImage(string objectName, Vector2 size)
    {
        GameObject imageObject = new(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(transform, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = new Color(0.45f, 0.48f, 0.52f, 1f);
        image.raycastTarget = false;

        LayoutElement layoutElement = imageObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = size.x;
        layoutElement.preferredWidth = size.x;
        layoutElement.minHeight = size.y;
        layoutElement.preferredHeight = size.y;
        layoutElement.flexibleWidth = 0f;

        return image;
    }

    private void SetFixedLayout(GameObject target, float width, float height)
    {
#pragma warning disable UNT0026 // GetComponent always allocates
        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
#pragma warning restore UNT0026 // GetComponent always allocates

        if (layoutElement == null)
            layoutElement = target.AddComponent<LayoutElement>();

        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;
    }

    private RectTransform CreateTextColumn()
    {
        GameObject columnObject = new("Text Column", typeof(RectTransform));
        columnObject.transform.SetParent(transform, false);

        VerticalLayoutGroup layout = columnObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 1f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        LayoutElement layoutElement = columnObject.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;
        layoutElement.minHeight = 16f;
        layoutElement.preferredHeight = 16f;

        return (RectTransform)columnObject.transform;
    }

    private void ConfigureTextColumn(RectTransform column)
    {
        if (column == null)
            return;

        VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();

        if (layout == null)
            layout = column.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        LayoutElement layoutElement = column.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = column.gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = 16f;
        layoutElement.preferredHeight = 16f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;
    }

    private RectTransform ResolveCostRow(Transform textColumn)
    {
        Transform existing = textColumn.Find("Cost Row");

        if (existing is RectTransform existingRow)
            return existingRow;

        GameObject rowObject = new("Cost Row", typeof(RectTransform));
        rowObject.transform.SetParent(textColumn, false);

        HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 8f;
        layoutElement.preferredHeight = 8f;
        layoutElement.flexibleWidth = 1f;
        return (RectTransform)rowObject.transform;
    }

    private void ConfigureCostRow(RectTransform row)
    {
        if (row == null)
            return;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
            layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        LayoutElement layoutElement = row.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = row.gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = 8f;
        layoutElement.preferredHeight = 8f;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;
    }

    private TMP_Text CreateText(string objectName, Transform parent, int fontSize, FontStyles style, float preferredHeight)
    {
        GameObject textObject = new(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, fontSize, style, UiTheme.TextInverse, autoSize: true);
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = objectName == "Name";

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = 1f;

        return text;
    }

    private void ConfigureText(TMP_Text text, float fontSize, float preferredHeight, FontStyles style)
    {
        if (text == null)
            return;

        UiTheme.Style(text, fontSize, style, text.color, autoSize: true);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        LayoutElement layoutElement = text.GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = text.gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;
    }

    private void ConfigureButtonText(TMP_Text text)
    {
        if (text == null)
            return;

        UiTheme.Style(text, UiTheme.Small, text.fontStyle, text.color, autoSize: true);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private Button CreateSmallButton(string objectName, UnityEngine.Events.UnityAction onClick, float width)
    {
        GameObject buttonObject = new(objectName, typeof(RectTransform));
        buttonObject.transform.SetParent(transform, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = buyButtonColor;

        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.minHeight = 16f;
        layoutElement.preferredHeight = 16f;
        layoutElement.flexibleWidth = 0f;

        GameObject textObject = new("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, UiTheme.Small, FontStyles.Normal, UiTheme.TextPrimary, autoSize: true);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        if (objectName == "Buy Button")
            buyButtonText = text;
        else
            lockButtonText = text;

        return button;
    }

    private void EnsureLockIcon()
    {
        if (lockButton == null)
            return;

        if (lockButtonText != null)
            lockButtonText.gameObject.SetActive(false);

        if (lockBodyImage == null)
            lockBodyImage = CreateLockPart("Lock Body", new Vector2(0.5f, 0.34f), new Vector2(8f, 5f));

        if (lockShackleImage == null)
            lockShackleImage = CreateLockPart("Lock Shackle", new Vector2(0.5f, 0.68f), new Vector2(9f, 6f));

        if (lockShackleImage != null)
            lockShackleRect = (RectTransform)lockShackleImage.transform;
    }

    private Image CreateLockPart(string objectName, Vector2 anchor, Vector2 size)
    {
        Transform existing = lockButton.transform.Find(objectName);

        if (existing != null && existing.TryGetComponent(out Image existingImage))
            return existingImage;

        GameObject partObject = new(objectName, typeof(RectTransform));
        partObject.transform.SetParent(lockButton.transform, false);

        RectTransform rect = (RectTransform)partObject.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = partObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private Image CreateInlineIcon(string objectName, Transform parent, Vector2 size)
    {
        GameObject imageObject = new(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        LayoutElement layoutElement = imageObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = size.x;
        layoutElement.preferredWidth = size.x;
        layoutElement.minHeight = size.y;
        layoutElement.preferredHeight = size.y;
        layoutElement.flexibleWidth = 0f;

        return image;
    }

    private void RefreshLockIcon(bool isLocked)
    {
        EnsureLockIcon();
        Sprite lockSprite = ResolveUiArtCatalog()?.lockIcon?.FirstFrame;

        if (lockSprite != null && lockButtonImage != null)
        {
            lockButtonImage.sprite = lockSprite;
            lockButtonImage.preserveAspect = true;
            lockButtonImage.color = isLocked ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);

            if (lockBodyImage != null)
                lockBodyImage.gameObject.SetActive(false);

            if (lockShackleImage != null)
                lockShackleImage.gameObject.SetActive(false);

            return;
        }

        Color iconColor = isLocked
            ? new Color(0.05f, 0.05f, 0.05f, 1f)
            : new Color(0.32f, 0.32f, 0.32f, 1f);

        if (lockBodyImage != null)
            lockBodyImage.color = iconColor;

        if (lockShackleImage != null)
            lockShackleImage.color = iconColor;

        if (lockShackleRect != null)
        {
            lockShackleRect.anchoredPosition = isLocked ? Vector2.zero : new Vector2(3f, 1f);
            lockShackleRect.localEulerAngles = isLocked ? Vector3.zero : new Vector3(0f, 0f, -22f);
        }
    }

    private RunUiArtCatalog ResolveUiArtCatalog()
    {
        if (uiArtCatalog == null)
            uiArtCatalog = RunUiArtCatalog.ResolveDefault();

        return uiArtCatalog;
    }

    private TMP_Text CreateCannotBuyPopup()
    {
        GameObject popupObject = new("Cannot Buy Popup", typeof(RectTransform));
        popupObject.transform.SetParent(buyButton.transform, false);

        RectTransform popupRect = (RectTransform)popupObject.transform;
        popupRect.anchorMin = new Vector2(0.5f, 1f);
        popupRect.anchorMax = new Vector2(0.5f, 1f);
        popupRect.pivot = new Vector2(0.5f, 0f);
        popupRect.anchoredPosition = new Vector2(0f, 3f);
        popupRect.sizeDelta = new Vector2(54f, 16f);

        Image popupImage = popupObject.AddComponent<Image>();
        popupImage.color = new Color(0.95f, 0.12f, 0.1f, 0.95f);
        popupImage.raycastTarget = false;

        GameObject textObject = new("Text", typeof(RectTransform));
        textObject.transform.SetParent(popupObject.transform, false);

        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, UiTheme.Small, FontStyles.Normal, UiTheme.TextInverse, autoSize: true);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        return text;
    }

    private void ShowCannotBuyFeedback()
    {
        if (cannotBuyRoutine != null)
            StopCoroutine(cannotBuyRoutine);

        cannotBuyRoutine = StartCoroutine(CannotBuyRoutine());
    }

    private IEnumerator CannotBuyRoutine()
    {
        if (buyButtonImage != null)
            buyButtonImage.color = buyButtonFailureColor;

        if (cannotBuyPopupText != null)
        {
            cannotBuyPopupText.text = Loc.T("shop.purchase.cannot_buy");
            cannotBuyPopupText.transform.parent.gameObject.SetActive(true);
        }

        yield return new WaitForSecondsRealtime(0.18f);

        if (buyButtonImage != null)
            buyButtonImage.color = buyButtonColor;

        yield return new WaitForSecondsRealtime(0.65f);

        if (cannotBuyPopupText != null)
            cannotBuyPopupText.transform.parent.gameObject.SetActive(false);

        cannotBuyRoutine = null;
    }
}

public class ShopItemHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [SerializeField] private TMP_Text labelText;

    private ShopItemDefinition item;
    private ShopTooltipView tooltipView;
    private bool strikeTriggeredEffects;

    private void Awake()
    {
        if (labelText == null)
            labelText = GetComponent<TMP_Text>();
    }

    public void Bind(ShopItemDefinition definition, ShopTooltipView tooltip, bool strikeEffects)
    {
        item = definition;
        tooltipView = tooltip;
        strikeTriggeredEffects = strikeEffects;

        if (labelText != null)
            labelText.raycastTarget = item != null;

        if (item == null)
            tooltipView?.Hide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null)
            tooltipView?.Show(item, eventData.position, strikeTriggeredEffects);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipView?.Hide();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        tooltipView?.MoveTo(eventData.position);
    }

    private void OnDisable()
    {
        // slot can vanish mid hover (buying etc), don't leave the tooltip stranded
        // unity null check on purpose, ?. would still call into a destroyed tooltip
        if (tooltipView != null)
            tooltipView.Hide();
    }
}

public class ShopPurchasedItemRowView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    private ShopItemHoverTarget hoverTarget;
    private ShopItemDefinition currentItem;

    private void Awake()
    {
        EnsureUi();
    }

    public void Bind(ShopItemDefinition item, ShopTooltipView tooltipView)
    {
        EnsureUi();
        currentItem = item;
        bool hasItem = currentItem != null;

        iconImage.enabled = hasItem;
        iconImage.sprite = hasItem ? currentItem.icon : null;
        iconImage.color = hasItem && currentItem.icon != null ? Color.white : new Color(0.45f, 0.48f, 0.52f, 1f);
        nameText.text = hasItem ? Loc.T(currentItem.nameKey) : string.Empty;
        hoverTarget.Bind(currentItem, tooltipView, true);
    }

    private void EnsureUi()
    {
        if (iconImage != null && nameText != null && hoverTarget != null)
            return;

        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();

        if (rectTransform == null)
            rectTransform = gameObject.AddComponent<RectTransform>();

        rectTransform.sizeDelta = new Vector2(0f, 16f);

        HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
            layout = gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.padding = new RectOffset(0, 0, 1, 1);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        LayoutElement rowLayout = gameObject.GetComponent<LayoutElement>();

        if (rowLayout == null)
            rowLayout = gameObject.AddComponent<LayoutElement>();

        rowLayout.minHeight = 16f;
        rowLayout.preferredHeight = 16f;
        rowLayout.flexibleHeight = 0f;

        if (iconImage == null)
            iconImage = CreateIcon();

        if (nameText == null)
            nameText = CreateNameText();

        if (hoverTarget == null)
            hoverTarget = GetOrAddComponent<ShopItemHoverTarget>(nameText.gameObject);
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }

    private Image CreateIcon()
    {
        GameObject iconObject = new("Icon", typeof(RectTransform));
        iconObject.transform.SetParent(transform, false);

        Image image = iconObject.AddComponent<Image>();
        image.raycastTarget = false;

        LayoutElement layoutElement = iconObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = 12f;
        layoutElement.preferredWidth = 12f;
        layoutElement.minHeight = 12f;
        layoutElement.preferredHeight = 12f;
        layoutElement.flexibleWidth = 0f;

        return image;
    }

    private TMP_Text CreateNameText()
    {
        GameObject textObject = new("Name", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, UiTheme.Small, FontStyles.Normal, UiTheme.TextInverse, autoSize: true);
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 14f;
        layoutElement.preferredHeight = 14f;
        layoutElement.flexibleWidth = 1f;

        return text;
    }
}
