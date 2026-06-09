using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IntermissionPanelView : MonoBehaviour
{
    private enum PanelMode
    {
        Shop,
        RunComplete,
        GameOver
    }

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text nextRoundButtonText;
    [SerializeField] private Button nextRoundButton;

    private readonly List<ShopItemSlotView> shelfSlotViews = new();
    private PanelMode currentMode = PanelMode.Shop;
    private RunStatsController currentStats;
    private RunShopController currentShop;
    private RectTransform shopRoot;
    private ShopTooltipView tooltipView;
    private string shopPrompt;

    private void OnEnable()
    {
        Loc.OnLocaleChanged += HandleLocaleChanged;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.OnLocaleChanged -= HandleLocaleChanged;
        SetStats(null);
        SetShop(null);
    }

    public void ShowShop(RunStatsController stats, bool canContinue)
    {
        ShowShop(stats, (RunShopController)null, canContinue);
    }

    public void ShowShop(RunStatsController stats, RunShopController shop, bool canContinue)
    {
        currentMode = PanelMode.Shop;
        SetStats(stats);
        SetShop(shop);
        shopPrompt = string.Empty;
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(canContinue);
        Refresh();
    }

    public void ShowShop(RunStatsController stats, IReadOnlyList<string> options, bool canContinue)
    {
        currentMode = PanelMode.Shop;
        SetStats(stats);
        SetShop(null);
        shopPrompt = FormatShopOptions(options);
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(canContinue);
        Refresh();
    }

    public void ShowShopResult(RunStatsController stats, string result)
    {
        currentMode = PanelMode.Shop;
        SetStats(stats);
        shopPrompt = result;
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(true);
        Refresh();
    }

    public void ShowRunComplete(RunStatsController stats)
    {
        currentMode = PanelMode.RunComplete;
        SetStats(stats);
        SetShop(null);
        shopPrompt = string.Empty;
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(true);
        Refresh();
    }

    public void ShowGameOver(RunStatsController stats)
    {
        currentMode = PanelMode.GameOver;
        SetStats(stats);
        SetShop(null);
        shopPrompt = string.Empty;
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(true);
        Refresh();
    }

    public void SetNextRoundButtonInteractable(bool interactable)
    {
        if (nextRoundButton != null)
            nextRoundButton.interactable = interactable;
    }

    public void SetNextRoundButtonVisible(bool visible)
    {
        if (nextRoundButton != null)
            nextRoundButton.gameObject.SetActive(visible);
    }

    private void SetStats(RunStatsController stats)
    {
        if (currentStats == stats)
            return;

        if (currentStats != null)
            currentStats.StatsChanged -= HandleStatsChanged;

        currentStats = stats;

        if (currentStats != null)
            currentStats.StatsChanged += HandleStatsChanged;
    }

    private void SetShop(RunShopController shop)
    {
        if (currentShop == shop)
            return;

        if (currentShop != null)
        {
            currentShop.ShopChanged -= HandleShopChanged;
            currentShop.PurchaseFeedback -= HandlePurchaseFeedback;
        }

        currentShop = shop;

        if (currentShop != null)
        {
            currentShop.ShopChanged += HandleShopChanged;
            currentShop.PurchaseFeedback += HandlePurchaseFeedback;
        }
    }

    private void HandleLocaleChanged(string locale)
    {
        Refresh();
    }

    private void HandleStatsChanged(RunStatsController stats)
    {
        Refresh();
    }

    private void HandleShopChanged()
    {
        Refresh();
    }

    private void HandlePurchaseFeedback(string feedback)
    {
        shopPrompt = feedback;
        Refresh();
    }

    private void Refresh()
    {
        if (nextRoundButtonText != null)
            nextRoundButtonText.text = currentMode == PanelMode.GameOver || currentMode == PanelMode.RunComplete
                ? Loc.T("shop.restart")
                : Loc.T("shop.next_round");

        switch (currentMode)
        {
            case PanelMode.Shop:
                ConfigureShopPanelFrame();
                ConfigureShopText();
                SetText(titleText, Loc.T("shop.placeholder.title"));
                SetText(bodyText, FormatShopBody());
                SetShopUiVisible(currentShop != null);
                RefreshShopSlots();
                break;
            case PanelMode.RunComplete:
                SetShopUiVisible(false);
                ConfigureSummaryText();
                SetText(titleText, Loc.T("run.complete"));
                SetText(bodyText, FormatStats("run.complete.body"));
                break;
            case PanelMode.GameOver:
                SetShopUiVisible(false);
                ConfigureSummaryText();
                SetText(titleText, Loc.T("run.game_over"));
                SetText(bodyText, FormatStats("run.game_over.body"));
                break;
        }
    }

    private string FormatShopBody()
    {
        string attention = currentStats != null
            ? Loc.Format("hud.attention", currentStats.Attention)
            : string.Empty;

        if (string.IsNullOrWhiteSpace(shopPrompt))
            return attention;

        return attention + "\n" + shopPrompt;
    }

    private string FormatShopOptions(IReadOnlyList<string> options)
    {
        if (options == null || options.Count <= 0)
            return Loc.T("shop.no_offers");

        string text = Loc.T("shop.legacy.choose") + "\n";

        for (int i = 0; i < options.Count; i++)
        {
            text += $"{i + 1}: {options[i]}\n";
        }

        return text + Loc.T("shop.legacy.skip");
    }

    private string FormatStats(string key)
    {
        if (currentStats == null)
            return Loc.T(key);

        return Loc.Format(
            key,
            currentStats.Score,
            currentStats.Attention,
            currentStats.TotalLinesCleared,
            currentStats.Composure,
            currentStats.Noise
        );
    }

    private void EnsureShopUi()
    {
        if (shopRoot != null)
            return;

        shopRoot = CreateRect("Shop Runtime Root", transform);
        shopRoot.anchorMin = new Vector2(0.05f, 0.13f);
        shopRoot.anchorMax = new Vector2(0.95f, 0.8f);
        shopRoot.offsetMin = Vector2.zero;
        shopRoot.offsetMax = Vector2.zero;

        VerticalLayoutGroup rootLayout = shopRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 3f;
        rootLayout.childAlignment = TextAnchor.UpperCenter;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        RectTransform shelfList = CreateRect("Shelf List", shopRoot);
        VerticalLayoutGroup shelfLayout = shelfList.gameObject.AddComponent<VerticalLayoutGroup>();
        shelfLayout.spacing = 5f;
        shelfLayout.childAlignment = TextAnchor.UpperCenter;
        shelfLayout.childControlWidth = true;
        shelfLayout.childControlHeight = true;
        shelfLayout.childForceExpandWidth = true;
        shelfLayout.childForceExpandHeight = false;
        AddLayoutElement(shelfList.gameObject, 0f, 114f, 1f);

        for (int i = 0; i < 3; i++)
        {
            ShopItemSlotView slotView = CreateSlot("Shelf Slot " + (i + 1), shelfList, 34f);
            shelfSlotViews.Add(slotView);
        }

        RectTransform tooltipRect = CreateRect("Shop Tooltip", ResolveTooltipParent());
        tooltipView = tooltipRect.gameObject.AddComponent<ShopTooltipView>();
        tooltipView.Hide();
    }

    private void ConfigureShopPanelFrame()
    {
        if (transform is not RectTransform rectTransform)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void ConfigureShopText()
    {
        if (titleText != null)
        {
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.05f, 0.88f);
            titleRect.anchorMax = new Vector2(0.95f, 0.97f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            UiTheme.Style(titleText, UiTheme.Title, titleText.fontStyle, titleText.color);
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        if (bodyText == null)
            return;

        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0.05f, 0.81f);
        bodyRect.anchorMax = new Vector2(0.95f, 0.87f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;
        UiTheme.Style(bodyText, UiTheme.Body, bodyText.fontStyle, bodyText.color);
        bodyText.alignment = TextAlignmentOptions.Left;
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        if (nextRoundButton != null)
        {
            RectTransform buttonRect = nextRoundButton.transform as RectTransform;

            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.32f, 0.03f);
                buttonRect.anchorMax = new Vector2(0.68f, 0.12f);
                buttonRect.offsetMin = Vector2.zero;
                buttonRect.offsetMax = Vector2.zero;
            }
        }
    }

    private void ConfigureSummaryText()
    {
        if (titleText != null)
        {
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.08f, 0.72f);
            titleRect.anchorMax = new Vector2(0.92f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            UiTheme.Style(titleText, UiTheme.Title, titleText.fontStyle, titleText.color);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (bodyText != null)
        {
            RectTransform bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0.08f, 0.27f);
            bodyRect.anchorMax = new Vector2(0.92f, 0.68f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
            UiTheme.Style(bodyText, UiTheme.Heading, bodyText.fontStyle, bodyText.color);
            bodyText.alignment = TextAlignmentOptions.Center;
            bodyText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (nextRoundButton != null)
        {
            RectTransform buttonRect = nextRoundButton.transform as RectTransform;

            if (buttonRect == null)
                return;

            buttonRect.anchorMin = new Vector2(0.16f, 0.08f);
            buttonRect.anchorMax = new Vector2(0.84f, 0.21f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
        }
    }

    private ShopItemSlotView CreateSlot(string objectName, Transform parent, float height)
    {
        RectTransform slotRect = CreateRect(objectName, parent);
        AddLayoutElement(slotRect.gameObject, 0f, height, 1f);
        return slotRect.gameObject.AddComponent<ShopItemSlotView>();
    }

    private TMP_Text CreateText(string objectName, Transform parent, int fontSize, FontStyles style)
    {
        RectTransform textRect = CreateRect(objectName, parent);
        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, fontSize, style, UiTheme.TextInverse);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return (RectTransform)rectObject.transform;
    }

    private Transform ResolveTooltipParent()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    private void AddLayoutElement(GameObject target, float preferredWidth, float preferredHeight, float flexibleWidth)
    {
        LayoutElement layoutElement = target.AddComponent<LayoutElement>();
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = flexibleWidth;
        layoutElement.flexibleHeight = 0f;
    }

    private void SetShopUiVisible(bool visible)
    {
        if (visible)
            EnsureShopUi();

        if (shopRoot != null)
            shopRoot.gameObject.SetActive(visible);

        if (!visible && tooltipView != null)
            tooltipView.Hide();
    }

    private void RefreshShopSlots()
    {
        if (currentShop == null)
            return;

        EnsureShopUi();

        for (int i = 0; i < shelfSlotViews.Count; i++)
        {
            shelfSlotViews[i].Bind(currentShop, i, tooltipView);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(shopRoot);
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
