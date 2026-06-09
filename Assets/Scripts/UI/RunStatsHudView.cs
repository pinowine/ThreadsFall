using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunStatsHudView : MonoBehaviour
{
    [SerializeField] private RunStatsController statsController;
    [SerializeField] private TMP_Text attentionText;
    [SerializeField] private TMP_Text composureText;
    [SerializeField] private TMP_Text noiseText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text linesText;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text combinedStatsText;
    [SerializeField] private TMP_Text purchasedItemsText;
    [SerializeField] private RunUiArtCatalog uiArtCatalog;
    [SerializeField] private RectTransform hudPanel;
    [SerializeField] private RectTransform itemsPanel;

    private readonly List<ShopPurchasedItemRowView> purchasedItemRows = new();
    private bool statsSubscribed;
    private bool shopSubscribed;
    private RunShopController shopController;
    private RectTransform statusRowsRoot;
    private HudStatRowView attentionRow;
    private HudStatRowView composureRow;
    private HudStatRowView noiseRow;
    private RectTransform purchasedItemsRoot;
    private ShopTooltipView tooltipView;

    private void Awake()
    {
        EnsureSplitLayout();
    }

    private void OnEnable()
    {
        EnsureSplitLayout();
        Loc.OnLocaleChanged += HandleLocaleChanged;
        SubscribeStats();
        SubscribeShop();
        Refresh();
    }

    private void OnDisable()
    {
        Loc.OnLocaleChanged -= HandleLocaleChanged;
        UnsubscribeStats();
        UnsubscribeShop();
    }

    public void SetStatsController(RunStatsController controller)
    {
        if (statsController == controller)
            return;

        UnsubscribeStats();
        statsController = controller;
        SubscribeStats();
        Refresh();
    }

    public void SetShopController(RunShopController controller)
    {
        if (shopController == controller)
            return;

        UnsubscribeShop();
        shopController = controller;
        SubscribeShop();
        Refresh();
    }

    private void SubscribeStats()
    {
        if (statsController == null || statsSubscribed)
            return;

        statsController.StatsChanged += HandleStatsChanged;
        statsSubscribed = true;
    }

    private void UnsubscribeStats()
    {
        if (statsController == null || !statsSubscribed)
            return;

        statsController.StatsChanged -= HandleStatsChanged;
        statsSubscribed = false;
    }

    private void SubscribeShop()
    {
        if (shopController == null || shopSubscribed)
            return;

        shopController.PurchaseHistoryChanged += HandlePurchaseHistoryChanged;
        shopSubscribed = true;
    }

    private void UnsubscribeShop()
    {
        if (shopController == null || !shopSubscribed)
            return;

        shopController.PurchaseHistoryChanged -= HandlePurchaseHistoryChanged;
        shopSubscribed = false;
    }

    private void HandleStatsChanged(RunStatsController stats)
    {
        Refresh();
    }

    private void HandleLocaleChanged(string locale)
    {
        Refresh();
    }

    private void HandlePurchaseHistoryChanged()
    {
        Refresh();
    }

    private void Refresh()
    {
        EnsureSplitLayout();

        if (statsController == null)
            return;

        SetText(attentionText, Loc.Format("hud.attention", statsController.Attention));
        SetText(composureText, Loc.Format("hud.composure", statsController.Composure));
        SetText(noiseText, Loc.Format("hud.noise", statsController.Noise));
        SetText(scoreText, Loc.Format("hud.score", statsController.Score));
        SetText(linesText, Loc.Format("hud.lines", statsController.TotalLinesCleared));
        SetText(roundText, Loc.Format("hud.round", statsController.CurrentRoundIndex));

        if (combinedStatsText != null)
        {
            combinedStatsText.text = Loc.T("hud.status_title");
        }

        RefreshStatusRows();
        RefreshPurchasedItems();
    }

    private void RefreshStatusRows()
    {
        EnsureStatusRows();

        if (statsController == null)
            return;

        RunUiArtCatalog artCatalog = ResolveUiArtCatalog();
        attentionRow.Bind(artCatalog != null ? artCatalog.attentionIcon : null, Loc.T("hud.attention.label"), statsController.Attention.ToString());
        composureRow.Bind(artCatalog != null ? artCatalog.composureIcon : null, Loc.T("hud.composure.label"), statsController.Composure.ToString());
        noiseRow.Bind(artCatalog != null ? artCatalog.noiseIcon : null, Loc.T("hud.noise.label"), statsController.Noise.ToString());
    }

    private void RefreshPurchasedItems()
    {
        if (purchasedItemsText == null)
            return;

        if (shopController == null || shopController.PurchasedItems.Count <= 0)
        {
            SetText(purchasedItemsText, Loc.T("hud.purchased_items"));
            SetPurchasedRowCount(0);
            return;
        }

        SetText(purchasedItemsText, Loc.T("hud.purchased_items"));
        EnsurePurchasedItemsRoot();
        EnsureTooltip();
        SetPurchasedRowCount(shopController.PurchasedItems.Count);

        for (int i = 0; i < shopController.PurchasedItems.Count; i++)
        {
            purchasedItemRows[i].Bind(shopController.PurchasedItems[i], tooltipView);
        }
    }

    private void EnsureSplitLayout()
    {
        ResolvePanelRoots();

        if (combinedStatsText == null)
            combinedStatsText = FindOrCreateCombinedStatsText();

        if (combinedStatsText == null)
            return;

        ApplyPanelArt();
        ConfigurePanelClip(hudPanel);
        ConfigurePanelClip(itemsPanel);
        MoveToPanel(combinedStatsText.rectTransform, ResolveHudPanel());
        ConfigureText(combinedStatsText, new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.96f), 9);
        EnsureStatusRows();

        if (purchasedItemsText == null)
            purchasedItemsText = FindOrCreatePurchasedItemsText();

        if (purchasedItemsText != null)
        {
            MoveToPanel(purchasedItemsText.rectTransform, ResolveItemsPanel());
            ConfigureText(purchasedItemsText, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f), 9);
        }

        EnsurePurchasedItemsRoot();
        EnsureTooltip();
    }

    private void EnsureStatusRows()
    {
        if (combinedStatsText == null)
            return;

        if (statusRowsRoot != null)
        {
            EnsureStatusRowReferences();
            return;
        }

        RectTransform parent = ResolveHudPanel();

        if (parent == null)
            return;

        Transform existing = parent.Find("StatusRowsRoot") ?? FindDescendant(transform, "StatusRowsRoot");

        if (existing is RectTransform existingRoot)
        {
            statusRowsRoot = existingRoot;
            MoveToPanel(statusRowsRoot, parent);
        }
        else
        {
            GameObject rootObject = new("StatusRowsRoot", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            statusRowsRoot = (RectTransform)rootObject.transform;
        }

        statusRowsRoot.anchorMin = new Vector2(0.05f, 0.08f);
        statusRowsRoot.anchorMax = new Vector2(0.95f, 0.68f);
        statusRowsRoot.offsetMin = Vector2.zero;
        statusRowsRoot.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = statusRowsRoot.GetComponent<VerticalLayoutGroup>();

        if (layout == null)
            layout = statusRowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 1f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        EnsureStatusRowReferences();
    }

    private void EnsureStatusRowReferences()
    {
        attentionRow ??= FindOrCreateStatusRow("Attention Row");
        composureRow ??= FindOrCreateStatusRow("Composure Row");
        noiseRow ??= FindOrCreateStatusRow("Noise Row");
    }

    private HudStatRowView FindOrCreateStatusRow(string objectName)
    {
        Transform existing = statusRowsRoot.Find(objectName);

        if (existing != null && existing.TryGetComponent(out HudStatRowView existingRow))
            return existingRow;

        GameObject rowObject = new(objectName, typeof(RectTransform));
        rowObject.transform.SetParent(statusRowsRoot, false);
        return rowObject.AddComponent<HudStatRowView>();
    }

    private TMP_Text FindOrCreatePurchasedItemsText()
    {
        RectTransform parent = ResolveItemsPanel();

        if (parent == null)
            return null;

        Transform existing = parent.Find("PurchasedItemsText") ?? FindDescendant(transform, "PurchasedItemsText");

        if (existing != null && existing.TryGetComponent(out TMP_Text existingText))
        {
            MoveToPanel(existingText.rectTransform, parent);
            return existingText;
        }

        GameObject textObject = new("PurchasedItemsText", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        return textObject.AddComponent<TextMeshProUGUI>();
    }

    private TMP_Text FindOrCreateCombinedStatsText()
    {
        RectTransform parent = ResolveHudPanel();

        if (parent == null)
            return null;

        Transform existing = parent.Find("CombinedStatsText") ?? parent.Find("StatusText") ?? FindDescendant(transform, "CombinedStatsText");

        if (existing != null && existing.TryGetComponent(out TMP_Text existingText))
        {
            MoveToPanel(existingText.rectTransform, parent);
            return existingText;
        }

        GameObject textObject = new("CombinedStatsText", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        return textObject.AddComponent<TextMeshProUGUI>();
    }

    private void EnsurePurchasedItemsRoot()
    {
        if (purchasedItemsText == null || purchasedItemsRoot != null)
            return;

        RectTransform parent = ResolveItemsPanel();

        if (parent == null)
            return;

        Transform existing = parent.Find("PurchasedItemsRoot") ?? FindDescendant(transform, "PurchasedItemsRoot");

        if (existing is RectTransform existingRoot)
        {
            purchasedItemsRoot = existingRoot;
            MoveToPanel(purchasedItemsRoot, parent);
        }
        else
        {
            GameObject rootObject = new("PurchasedItemsRoot", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);
            purchasedItemsRoot = (RectTransform)rootObject.transform;
        }

        purchasedItemsRoot.anchorMin = new Vector2(0.05f, 0.08f);
        purchasedItemsRoot.anchorMax = new Vector2(0.95f, 0.78f);
        purchasedItemsRoot.offsetMin = Vector2.zero;
        purchasedItemsRoot.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = purchasedItemsRoot.GetComponent<VerticalLayoutGroup>();

        if (layout == null)
            layout = purchasedItemsRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private void EnsureTooltip()
    {
        if (tooltipView != null)
            return;

        Transform parent = ResolveTooltipParent();
        GameObject tooltipObject = new("Purchased Item Tooltip", typeof(RectTransform));
        tooltipObject.transform.SetParent(parent, false);
        tooltipView = tooltipObject.AddComponent<ShopTooltipView>();
        tooltipView.Hide();
    }

    private Transform ResolveTooltipParent()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    private RunUiArtCatalog ResolveUiArtCatalog()
    {
        if (uiArtCatalog == null)
            uiArtCatalog = RunUiArtCatalog.ResolveDefault();

        return uiArtCatalog;
    }

    private void SetPurchasedRowCount(int count)
    {
        EnsurePurchasedItemsRoot();

        if (purchasedItemsRoot == null)
            return;

        while (purchasedItemRows.Count < count)
        {
            GameObject rowObject = new("Purchased Item Row", typeof(RectTransform));
            rowObject.transform.SetParent(purchasedItemsRoot, false);
            purchasedItemRows.Add(rowObject.AddComponent<ShopPurchasedItemRowView>());
        }

        for (int i = 0; i < purchasedItemRows.Count; i++)
        {
            purchasedItemRows[i].gameObject.SetActive(i < count);
        }
    }

    private void ConfigureText(TMP_Text text, Vector2 anchorMin, Vector2 anchorMax, int fontSize)
    {
        RectTransform rectTransform = text.rectTransform;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        UiTheme.Style(text, fontSize, text.fontStyle, UiTheme.TextInverse);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Masking;
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private void ApplyPanelArt()
    {
        Image panelImage = GetComponent<Image>();
        Sprite panelSprite = ResolveUiArtCatalog()?.leftPanelBackground;

        if (panelImage == null || panelSprite == null)
            return;

        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Simple;
        panelImage.color = Color.white;
    }

    private void ResolvePanelRoots()
    {
        hudPanel ??= FindPanel("HUDPanel");
        itemsPanel ??= FindPanel("ItemsPanel");
    }

    private RectTransform FindPanel(string objectName)
    {
        Transform direct = transform.Find(objectName);

        if (direct is RectTransform directRect)
            return directRect;

        return FindDescendant(transform, objectName) as RectTransform;
    }

    private RectTransform ResolveHudPanel()
    {
        ResolvePanelRoots();
        return hudPanel != null ? hudPanel : transform as RectTransform;
    }

    private RectTransform ResolveItemsPanel()
    {
        ResolvePanelRoots();
        return itemsPanel != null ? itemsPanel : transform as RectTransform;
    }

    private void ConfigurePanelClip(RectTransform panel)
    {
        if (panel == null || panel.GetComponent<RectMask2D>() != null)
            return;

        // panel masks keep dynamic text and item rows inside their authored blocks
        panel.gameObject.AddComponent<RectMask2D>();
    }

    private void MoveToPanel(RectTransform child, RectTransform panel)
    {
        if (child == null || panel == null || child.parent == panel)
            return;

        child.SetParent(panel, false);
    }

    private Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == objectName)
                return child;

            Transform nested = FindDescendant(child, objectName);

            if (nested != null)
                return nested;
        }

        return null;
    }
}
