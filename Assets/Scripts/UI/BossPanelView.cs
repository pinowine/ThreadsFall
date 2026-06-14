using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BossPanelView : MonoBehaviour
{
    private enum EncounterMode
    {
        Node,
        Shop,
        RunComplete,
        GameOver
    }

    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text bossCommentText;
    [SerializeField] private TMP_Text bossEffectText;
    [SerializeField] private RunEffectSystem effectController;

    private RectTransform effectBoxRect;
    [SerializeField] private RunUiArtCatalog uiArtCatalog;
    [SerializeField] private float avatarSwapDuration = 0.24f;

    public RectTransform AvatarRect
    {
        get
        {
            EnsureLayout();
            return avatarRect;
        }
    }

    private readonly List<RuntimeRunNode> routeNodes = new();
    private readonly List<ShopItemSlotView> shelfSlotViews = new();
    private EncounterMode currentMode = EncounterMode.Node;
    private RuntimeRunNode currentNode;
    private BossId currentBossId = BossId.None;
    private RunStatsController currentStats;
    private RunShopController currentShop;
    private UnityAction nextRoundAction;
    private RectTransform avatarRect;
    private RectTransform avatarCurrentRect;
    private RectTransform avatarPreviousRect;
    private Image avatarImage;
    private Image avatarPreviousImage;
    private AnimatedImageView avatarAnimator;
    private TMP_Text avatarText;
    private TMP_Text avatarPreviousText;
    private Image dialogBackgroundImage;
    private RectTransform shopRoot;
    private TMP_Text shopFeedbackText;
    private Button nextRoundButton;
    private TMP_Text nextRoundButtonText;
    private RunProgressView progressView;
    private ShopTooltipView tooltipView;
    private RunProgressTooltipView bossInfoTooltip;
    private BossInfoHoverTarget avatarHoverTarget;
    private BossInfoHoverTarget nameHoverTarget;
    private bool layoutReady;
    private int currentProgressIndex;
    private int completedNodeCount;
    private string shopPrompt;
    private Coroutine avatarSwapRoutine;
    private string currentAvatarKey;
    private bool avatarReady;

    private void OnEnable()
    {
        EnsureLayout();
        Loc.OnLocaleChanged += HandleLocaleChanged;
        SubscribeEffects();
        SubscribeStats();
        SubscribeShop();
        Refresh();
    }

    private void OnDisable()
    {
        Loc.OnLocaleChanged -= HandleLocaleChanged;
        UnsubscribeEffects();
        UnsubscribeStats();
        UnsubscribeShop();
    }

    public void BindRoute(IReadOnlyList<RuntimeRunNode> nodes)
    {
        EnsureLayout();
        routeNodes.Clear();

        if (nodes != null)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                routeNodes.Add(nodes[i]);
            }
        }

        progressView.BindRoute(routeNodes);
        RefreshProgress();
    }

    public void ShowNode(RuntimeRunNode node, int progressIndex, int completedCount)
    {
        currentMode = EncounterMode.Node;
        currentNode = node;
        currentBossId = node != null ? node.BossId : BossId.None;
        currentProgressIndex = progressIndex;
        completedNodeCount = completedCount;
        SetStats(null);
        SetShop(null);
        shopPrompt = string.Empty;
        SetNextRoundButtonVisible(false);
        SetShopRootVisible(false);
        Refresh();
    }

    public void ShowShop(RunStatsController stats, RunShopController shop, bool canContinue, UnityAction continueAction, int progressIndex, int completedCount)
    {
        currentMode = EncounterMode.Shop;
        currentNode = null;
        currentBossId = BossId.None;
        currentProgressIndex = progressIndex;
        completedNodeCount = completedCount;
        nextRoundAction = continueAction;
        shopPrompt = string.Empty;
        SetStats(stats);
        SetShop(shop);
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(canContinue);
        SetShopRootVisible(true);
        Refresh();
    }

    public void ShowRunComplete(RunStatsController stats, UnityAction restartAction)
    {
        currentMode = EncounterMode.RunComplete;
        nextRoundAction = restartAction;
        currentProgressIndex = Mathf.Max(0, routeNodes.Count - 1);
        completedNodeCount = routeNodes.Count;
        SetStats(stats);
        SetShop(null);
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(true);
        SetShopRootVisible(false);
        Refresh();
    }

    public void ShowGameOver(RunStatsController stats, UnityAction restartAction)
    {
        currentMode = EncounterMode.GameOver;
        nextRoundAction = restartAction;
        SetStats(stats);
        SetShop(null);
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(true);
        SetShopRootVisible(false);
        Refresh();
    }

    public void SetNextRoundButtonVisible(bool visible)
    {
        EnsureLayout();

        if (nextRoundButton != null)
            nextRoundButton.gameObject.SetActive(visible);
    }

    public void SetNextRoundButtonInteractable(bool interactable)
    {
        EnsureLayout();

        if (nextRoundButton != null)
            nextRoundButton.interactable = interactable;
    }

    public void SetEffectController(RunEffectSystem controller)
    {
        if (effectController == controller)
            return;

        UnsubscribeEffects();
        effectController = controller;
        SubscribeEffects();
        Refresh();
    }

    public void ShowBoss(BossId bossId)
    {
        currentMode = EncounterMode.Node;
        currentBossId = bossId;
        Refresh();
    }

    private void HandleLocaleChanged(string locale)
    {
        Refresh();
    }

    private void HandleEffectsChanged()
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
        EnsureLayout();
        RefreshProgress();

        if (nextRoundButtonText != null)
            nextRoundButtonText.text = currentMode == EncounterMode.GameOver || currentMode == EncounterMode.RunComplete
                ? Loc.T("shop.restart")
                : Loc.T("shop.next_round");

        switch (currentMode)
        {
            case EncounterMode.Shop:
                RefreshShopMode();
                break;
            case EncounterMode.RunComplete:
                RefreshSummaryMode("run.complete", "run.complete.body", "END");
                break;
            case EncounterMode.GameOver:
                RefreshSummaryMode("run.game_over", "run.game_over.body", "END");
                break;
            default:
                RefreshNodeMode();
                break;
        }
    }

    private void RefreshNodeMode()
    {
        SetShopRootVisible(false);
        SetDialogTextVisible(true);

        RunNodeType nodeType = currentNode != null ? currentNode.NodeType : RunNodeType.Normal;
        RuntimeRunNode displayNode = nodeType == RunNodeType.Normal ? FindNextBossNode(currentProgressIndex) : currentNode;
        RunNodeType displayNodeType = displayNode != null ? displayNode.NodeType : nodeType;
        BossDefinition bossDefinition = displayNode != null ? displayNode.BossDefinition : currentNode != null ? currentNode.BossDefinition : null;
        BossId displayBossId = displayNode != null ? displayNode.BossId : currentBossId;
        string displayNameKey = displayNode != null ? displayNode.DisplayNameKey : currentNode != null ? currentNode.DisplayNameKey : "run.node.search.name";
        SetText(bossNameText, Loc.T(displayNameKey));
        ConfigureAvatar(displayNodeType, GetAvatarLabel(displayNodeType), bossDefinition != null ? bossDefinition.avatarSequence : null, displayNodeType != RunNodeType.Normal);

        // aura and skill details live on the portrait/name hover now
        BindBossInfoHover(displayNode != null ? displayNode.BossDefinition : null, displayNodeType);

        if (displayNodeType == RunNodeType.Normal)
        {
            SetText(bossCommentText, Loc.T("run.node.search.comment"));
            SetEffectBoxText(string.Empty);
            return;
        }

        if (effectController != null && !effectController.CanShowBossIntent)
        {
            SetHiddenBossText();
            return;
        }

        string bossSegment = displayBossId.ToLocSegment();
        string commentKey = bossDefinition != null && !string.IsNullOrWhiteSpace(bossDefinition.commentKey)
            ? bossDefinition.commentKey
            : "boss." + bossSegment + ".comment.round_start";
        string effectKey = bossDefinition != null && !string.IsNullOrWhiteSpace(bossDefinition.effectKey)
            ? bossDefinition.effectKey
            : "boss.effect.none";
        string comment = Loc.T(commentKey);
        string effect = Loc.T(effectKey);

        if (bossCommentText != null)
            bossCommentText.text = bossEffectText == null ? comment + "\n" + effect : comment;

        SetEffectBoxText(effect);
    }

    private void RefreshShopMode()
    {
        BindBossInfoHover(null, RunNodeType.Normal);
        SetText(bossNameText, Loc.T("shop.encounter.name"));
        ConfigureAvatar(RunNodeType.Normal, "SHOP", ResolveUiArtCatalog()?.shopkeeperAvatar, false);
        SetEffectBoxText(string.Empty);
        SetDialogTextVisible(false);
        SetShopRootVisible(true);

        if (shopFeedbackText != null)
        {
            shopFeedbackText.gameObject.SetActive(true);
            shopFeedbackText.text = FormatShopBody();
        }

        for (int i = 0; i < shelfSlotViews.Count; i++)
        {
            shelfSlotViews[i].gameObject.SetActive(currentShop != null);
        }

        RefreshShopSlots();
    }

    private void RefreshSummaryMode(string titleKey, string bodyKey, string avatarLabel)
    {
        BindBossInfoHover(null, RunNodeType.Normal);
        // shop root stays on so the restart button inside it can actually show
        SetShopRootVisible(true);

        if (shopFeedbackText != null)
            shopFeedbackText.gameObject.SetActive(false);

        for (int i = 0; i < shelfSlotViews.Count; i++)
        {
            shelfSlotViews[i].gameObject.SetActive(false);
        }

        SetDialogTextVisible(true);
        SetText(bossNameText, Loc.T(titleKey));
        ConfigureAvatar(RunNodeType.FinalBoss, avatarLabel);
        SetText(bossCommentText, FormatStats(bodyKey));
        SetEffectBoxText(string.Empty);
    }

    // hovering the portrait or the name explains what this boss passively does
    // and which skills it can throw mid round
    private void BindBossInfoHover(BossDefinition boss, RunNodeType nodeType)
    {
        EnsureLayout();

        string info = BuildBossInfoText(boss, nodeType);
        avatarHoverTarget?.Bind(info, bossInfoTooltip);
        nameHoverTarget?.Bind(info, bossInfoTooltip);
    }

    private string BuildBossInfoText(BossDefinition boss, RunNodeType nodeType)
    {
        if (boss == null)
            return string.Empty;

        if (effectController != null && !effectController.CanShowBossIntent)
            return Loc.T("boss.hidden.comment") + "\n" + Loc.T("boss.hidden.effect");

        var builder = new System.Text.StringBuilder();

        if (nodeType == RunNodeType.Normal)
            builder.AppendLine(Loc.Format("boss.tooltip.upcoming", Loc.T(boss.nameKey)));

        string effectKey = string.IsNullOrWhiteSpace(boss.effectKey) ? "boss.effect.none" : boss.effectKey;
        builder.AppendLine(Loc.T(effectKey));

        if (boss.skills != null && boss.skills.Count > 0)
        {
            builder.AppendLine(Loc.T("boss.tooltip.skills"));

            for (int i = 0; i < boss.skills.Count; i++)
            {
                if (boss.skills[i] != null && boss.skills[i].kind != EffectKind.None)
                    builder.AppendLine("- " + EffectLore.KindLabel(boss.skills[i]));
            }
        }

        return builder.ToString().TrimEnd();
    }

    private void SetHiddenBossText()
    {
        string comment = Loc.T("boss.hidden.comment");
        string effect = Loc.T("boss.hidden.effect");

        if (bossNameText != null)
            bossNameText.text = Loc.T("boss.hidden.name");

        if (bossCommentText != null)
            bossCommentText.text = bossEffectText == null ? comment + "\n" + effect : comment;

        SetEffectBoxText(effect);
    }

    private void ConfigureAvatar(RunNodeType nodeType, string label, SpriteSequenceDefinition avatarSequence = null, bool playSpeaking = false)
    {
        if (avatarImage == null || avatarText == null)
            return;

        if (avatarAnimator == null)
            avatarAnimator = GetOrAddComponent<AnimatedImageView>(avatarCurrentRect.gameObject);

        string nextAvatarKey = BuildAvatarKey(nodeType, label, avatarSequence);

        if (!avatarReady)
        {
            ApplyAvatarState(nodeType, label, avatarSequence, playSpeaking);
            currentAvatarKey = nextAvatarKey;
            avatarReady = true;
            return;
        }

        if (currentAvatarKey != nextAvatarKey)
        {
            StartAvatarSwap(nodeType, label, avatarSequence, playSpeaking, nextAvatarKey);
            return;
        }

        if (playSpeaking)
            avatarAnimator.PlayOnceFromStart();
    }

    private string BuildAvatarKey(RunNodeType nodeType, string label, SpriteSequenceDefinition avatarSequence)
    {
        string sequenceId = avatarSequence != null ? avatarSequence.sequenceId : string.Empty;
        return nodeType + "|" + label + "|" + sequenceId + "|" + currentMode;
    }

    private void StartAvatarSwap(RunNodeType nodeType, string label, SpriteSequenceDefinition avatarSequence, bool playSpeaking, string nextAvatarKey)
    {
        if (avatarSwapRoutine != null)
            StopCoroutine(avatarSwapRoutine);

        CapturePreviousAvatar();
        ApplyAvatarState(nodeType, label, avatarSequence, playSpeaking);
        currentAvatarKey = nextAvatarKey;
        avatarSwapRoutine = StartCoroutine(AvatarSwapRoutine());
    }

    private void CapturePreviousAvatar()
    {
        if (avatarPreviousRect == null || avatarPreviousImage == null || avatarPreviousText == null)
            return;

        avatarPreviousRect.gameObject.SetActive(true);
        avatarPreviousImage.sprite = avatarImage.sprite;
        avatarPreviousImage.color = avatarImage.color;
        avatarPreviousImage.preserveAspect = false;
        bool showingText = avatarText != null && avatarText.gameObject.activeSelf;
        avatarPreviousText.gameObject.SetActive(showingText);
        avatarPreviousText.text = showingText ? avatarText.text : string.Empty;
    }

    private void ApplyAvatarState(RunNodeType nodeType, string label, SpriteSequenceDefinition avatarSequence, bool playSpeaking)
    {
        bool hasSequence = avatarSequence != null && avatarSequence.FirstFrame != null;
        avatarText.gameObject.SetActive(!hasSequence);
        avatarAnimator.SetSequence(hasSequence ? avatarSequence : null);

        if (hasSequence)
        {
            if (playSpeaking)
                avatarAnimator.PlayOnceFromStart();

            return;
        }

        avatarImage.sprite = null;
        avatarImage.color = GetFallbackAvatarColor(nodeType);
        avatarText.text = label;
    }

    private Color GetFallbackAvatarColor(RunNodeType nodeType)
    {
        return nodeType switch
        {
            RunNodeType.FinalBoss => new Color(0.02f, 0.02f, 0.02f, 1f),
            RunNodeType.MiniBoss => new Color(0.04f, 0.04f, 0.04f, 1f),
            _ => currentMode == EncounterMode.Shop ? new Color(0.08f, 0.08f, 0.05f, 1f) : new Color(0.02f, 0.02f, 0.02f, 1f)
        };
    }

    private IEnumerator AvatarSwapRoutine()
    {
        float height = avatarRect != null ? avatarRect.rect.height : 0f;

        if (height <= 1f)
            height = 120f;

        avatarPreviousRect.SetAsFirstSibling();
        avatarCurrentRect.SetAsLastSibling();
        SetAvatarLayerOffset(avatarPreviousRect, 0f);
        SetAvatarLayerOffset(avatarCurrentRect, -height);

        float duration = Mathf.Max(0.01f, avatarSwapDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            SetAvatarLayerOffset(avatarPreviousRect, Mathf.Lerp(0f, height, eased));
            SetAvatarLayerOffset(avatarCurrentRect, Mathf.Lerp(-height, 0f, eased));
            yield return null;
        }

        SetAvatarLayerOffset(avatarPreviousRect, 0f);
        SetAvatarLayerOffset(avatarCurrentRect, 0f);
        avatarPreviousRect.gameObject.SetActive(false);
        avatarSwapRoutine = null;
    }

    private void SetAvatarLayerOffset(RectTransform rectTransform, float y)
    {
        if (rectTransform == null)
            return;

        rectTransform.offsetMin = new Vector2(0f, y);
        rectTransform.offsetMax = new Vector2(0f, y);
    }

    private string GetAvatarLabel(RunNodeType nodeType)
    {
        return nodeType switch
        {
            RunNodeType.FinalBoss => "FINAL",
            RunNodeType.MiniBoss => "BOSS",
            _ => "NODE"
        };
    }

    private RuntimeRunNode FindNextBossNode(int fromIndex)
    {
        int startIndex = Mathf.Clamp(fromIndex + 1, 0, Mathf.Max(0, routeNodes.Count - 1));

        for (int i = startIndex; i < routeNodes.Count; i++)
        {
            if (routeNodes[i] != null && routeNodes[i].IsBossNode)
                return routeNodes[i];
        }

        return null;
    }

    private string FormatShopBody()
    {
        // attention already lives in the hud, shop only shows purchase feedback
        return string.IsNullOrWhiteSpace(shopPrompt) ? string.Empty : shopPrompt;
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

    private void SetStats(RunStatsController stats)
    {
        if (currentStats == stats)
            return;

        UnsubscribeStats();
        currentStats = stats;
        SubscribeStats();
    }

    private void SetShop(RunShopController shop)
    {
        if (currentShop == shop)
            return;

        UnsubscribeShop();
        currentShop = shop;
        SubscribeShop();
    }

    private void SubscribeEffects()
    {
        if (effectController != null)
            effectController.EffectsChanged += HandleEffectsChanged;
    }

    private void UnsubscribeEffects()
    {
        if (effectController != null)
            effectController.EffectsChanged -= HandleEffectsChanged;
    }

    private void SubscribeStats()
    {
        if (currentStats != null)
            currentStats.StatsChanged += HandleStatsChanged;
    }

    private void UnsubscribeStats()
    {
        if (currentStats != null)
            currentStats.StatsChanged -= HandleStatsChanged;
    }

    private void SubscribeShop()
    {
        if (currentShop == null)
            return;

        currentShop.ShopChanged += HandleShopChanged;
        currentShop.PurchaseFeedback += HandlePurchaseFeedback;
    }

    private void UnsubscribeShop()
    {
        if (currentShop == null)
            return;

        currentShop.ShopChanged -= HandleShopChanged;
        currentShop.PurchaseFeedback -= HandlePurchaseFeedback;
    }

    private void RefreshProgress()
    {
        if (progressView != null)
            progressView.SetProgress(currentProgressIndex, completedNodeCount);
    }

    private void RefreshShopSlots()
    {
        if (currentShop == null)
            return;

        EnsureLayout();

        for (int i = 0; i < shelfSlotViews.Count; i++)
        {
            shelfSlotViews[i].gameObject.SetActive(true);
            shelfSlotViews[i].Bind(currentShop, i, tooltipView);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(shopRoot);
    }

    private void EnsureLayout()
    {
        if (layoutReady)
            return;

        layoutReady = true;

        if (transform is RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        Image panelImage = GetComponent<Image>();

        if (panelImage != null)
        {
            // no big gray backdrop anymore, content floats on the game bg
            panelImage.enabled = false;
        }

        progressView = GetOrCreateChildComponent<RunProgressView>("Run Progress", transform);
        progressView.SetArtCatalog(ResolveUiArtCatalog());
        ConfigureRect(progressView.transform as RectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.96f));

        if (bossNameText == null)
            bossNameText = CreateText("Encounter Name", transform, UiTheme.Title, FontStyles.Bold);

        ConfigureText(bossNameText, new Vector2(0.07f, 0.62f), new Vector2(0.93f, 0.74f), UiTheme.Title, TextAlignmentOptions.Left);
        // no gray backdrop anymore so the name has to be white to read on black
        bossNameText.color = UiTheme.TextInverse;
        // pixel font runs wide, let long names shrink instead of spilling over the dialog
        bossNameText.enableAutoSizing = true;
        bossNameText.fontSizeMin = 10f;
        bossNameText.fontSizeMax = UiTheme.Title;
        bossNameText.textWrappingMode = TextWrappingModes.NoWrap;
        bossNameText.overflowMode = TextOverflowModes.Ellipsis;
        // the name is a hover target for the aura/skills breakdown
        bossNameText.raycastTarget = true;
        nameHoverTarget = GetOrAddComponent<BossInfoHoverTarget>(bossNameText.gameObject);

        avatarRect = FindOrCreateRect("Avatar Placeholder", transform);
        ConfigureRect(avatarRect, new Vector2(0.07f, 0.18f), new Vector2(0.32f, 0.58f));
        Image avatarBackground = GetOrAddComponent<Image>(avatarRect.gameObject);
        avatarBackground.color = Color.black;
        // hover area for the portrait animation
        avatarBackground.raycastTarget = true;

        if (avatarRect.GetComponent<RectMask2D>() == null)
            avatarRect.gameObject.AddComponent<RectMask2D>();

        avatarCurrentRect = FindOrCreateRect("Avatar Current", avatarRect);
        ConfigureRect(avatarCurrentRect, Vector2.zero, Vector2.one);
        avatarImage = GetOrAddComponent<Image>(avatarCurrentRect.gameObject);
        avatarImage.raycastTarget = false;
        avatarAnimator = GetOrAddComponent<AnimatedImageView>(avatarCurrentRect.gameObject);
        // fill the whole frame, no side bars
        avatarAnimator.SetPreserveAspect(false);
        GetOrAddComponent<HoverAnimatedImageTrigger>(avatarRect.gameObject).Bind(avatarAnimator);
        avatarHoverTarget = GetOrAddComponent<BossInfoHoverTarget>(avatarRect.gameObject);
        Transform legacyAvatarLabel = avatarRect.Find("Avatar Label");

        if (legacyAvatarLabel != null && legacyAvatarLabel.parent != avatarCurrentRect)
            legacyAvatarLabel.SetParent(avatarCurrentRect, false);

        avatarText = avatarCurrentRect.GetComponentInChildren<TMP_Text>();

        if (avatarText == null)
            avatarText = CreateText("Avatar Label", avatarCurrentRect, UiTheme.Heading, FontStyles.Bold);

        ConfigureRect(avatarText.rectTransform, Vector2.zero, Vector2.one);
        avatarText.color = UiTheme.TextInverse;
        avatarText.alignment = TextAlignmentOptions.Center;
        avatarText.textWrappingMode = TextWrappingModes.NoWrap;
        avatarText.enableAutoSizing = true;
        avatarText.fontSizeMin = 6f;
        avatarText.fontSizeMax = UiTheme.Heading;

        avatarPreviousRect = FindOrCreateRect("Avatar Previous", avatarRect);
        ConfigureRect(avatarPreviousRect, Vector2.zero, Vector2.one);
        avatarPreviousImage = GetOrAddComponent<Image>(avatarPreviousRect.gameObject);
        avatarPreviousImage.raycastTarget = false;
        avatarPreviousText = avatarPreviousRect.GetComponentInChildren<TMP_Text>();

        if (avatarPreviousText == null)
            avatarPreviousText = CreateText("Avatar Previous Label", avatarPreviousRect, UiTheme.Heading, FontStyles.Bold);

        ConfigureRect(avatarPreviousText.rectTransform, Vector2.zero, Vector2.one);
        avatarPreviousText.color = UiTheme.TextInverse;
        avatarPreviousText.alignment = TextAlignmentOptions.Center;
        avatarPreviousText.textWrappingMode = TextWrappingModes.NoWrap;
        avatarPreviousText.enableAutoSizing = true;
        avatarPreviousText.fontSizeMin = 6f;
        avatarPreviousText.fontSizeMax = UiTheme.Heading;
        avatarPreviousRect.gameObject.SetActive(false);

        RectTransform dialogRect = FindOrCreateRect("Dialog Background", transform);
        ConfigureRect(dialogRect, new Vector2(0.38f, 0.18f), new Vector2(0.93f, 0.58f));
        dialogRect.SetAsFirstSibling();
        dialogBackgroundImage = GetOrAddComponent<Image>(dialogRect.gameObject);
        // dialogue floats on the screen now, no box behind it
        dialogBackgroundImage.enabled = false;

        if (bossCommentText == null)
            bossCommentText = CreateText("Encounter Comment", transform, UiTheme.Heading, FontStyles.Normal);

        ConfigureText(bossCommentText, new Vector2(0.42f, 0.22f), new Vector2(0.9f, 0.54f), UiTheme.Body, TextAlignmentOptions.MidlineLeft);
        // dialog bg is gone so the comment reads white on black
        bossCommentText.color = UiTheme.TextInverse;
        // comment plus effect line can get long, shrink to fit the dialog box
        bossCommentText.enableAutoSizing = true;
        bossCommentText.fontSizeMin = 6f;
        bossCommentText.fontSizeMax = UiTheme.Body;

        // boss skill gets its own little box under the dialogue now
        effectBoxRect = FindOrCreateRect("Effect Box", transform);
        ConfigureRect(effectBoxRect, new Vector2(0.38f, 0.04f), new Vector2(0.93f, 0.15f));
        Image effectBoxImage = GetOrAddComponent<Image>(effectBoxRect.gameObject);
        effectBoxImage.color = UiTheme.PanelBgDark;
        effectBoxImage.raycastTarget = false;

        if (bossEffectText == null)
            bossEffectText = CreateText("Encounter Effect", effectBoxRect, UiTheme.Body, FontStyles.Normal);
        else if (bossEffectText.transform.parent != effectBoxRect)
            bossEffectText.transform.SetParent(effectBoxRect, false);

        ConfigureRect(bossEffectText.rectTransform, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));
        UiTheme.Style(bossEffectText, UiTheme.Body, FontStyles.Normal, UiTheme.TextPrimary, autoSize: true);
        bossEffectText.fontSizeMin = 6f;
        bossEffectText.alignment = TextAlignmentOptions.MidlineLeft;
        bossEffectText.gameObject.SetActive(true);

        EnsureShopUi();
        EnsureTooltip();
        EnsureBossInfoTooltip();
    }

    private void EnsureBossInfoTooltip()
    {
        if (bossInfoTooltip != null)
            return;

        GameObject tooltipObject = new("Boss Info Tooltip", typeof(RectTransform));
        tooltipObject.transform.SetParent(ResolveTooltipParent(), false);
        bossInfoTooltip = tooltipObject.AddComponent<RunProgressTooltipView>();
        bossInfoTooltip.Hide();

        // needs space for an aura line plus a few skill rows
        if (tooltipObject.transform is RectTransform rectTransform)
            rectTransform.sizeDelta = new Vector2(230f, 96f);
    }

    private void SetEffectBoxText(string value)
    {
        if (bossEffectText != null)
            bossEffectText.text = value;

        if (effectBoxRect != null)
            effectBoxRect.gameObject.SetActive(false);
    }

    private void EnsureShopUi()
    {
        if (shopRoot != null)
            return;

        shopRoot = FindOrCreateRect("Encounter Shop Root", transform);
        ConfigureRect(shopRoot, new Vector2(0.36f, 0.13f), new Vector2(0.94f, 0.6f));

        VerticalLayoutGroup rootLayout = GetOrAddComponent<VerticalLayoutGroup>(shopRoot.gameObject);
        rootLayout.spacing = 2f;
        rootLayout.childAlignment = TextAnchor.UpperCenter;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        // shop feedback line removed, purchases just speak for themselfs

        RectTransform shelfList = FindOrCreateRect("Shelf List", shopRoot);
        VerticalLayoutGroup shelfLayout = GetOrAddComponent<VerticalLayoutGroup>(shelfList.gameObject);
        shelfLayout.spacing = 2f;
        shelfLayout.childAlignment = TextAnchor.UpperCenter;
        shelfLayout.childControlWidth = true;
        shelfLayout.childControlHeight = true;
        shelfLayout.childForceExpandWidth = true;
        shelfLayout.childForceExpandHeight = false;
        AddLayoutElement(shelfList.gameObject, 0f, 68f, 1f);

        for (int i = 0; i < 3; i++)
        {
            RectTransform slotRect = FindOrCreateRect("Shelf Slot " + (i + 1), shelfList);
            AddLayoutElement(slotRect.gameObject, 0f, 20f, 1f);
            shelfSlotViews.Add(GetOrAddComponent<ShopItemSlotView>(slotRect.gameObject));
        }

        nextRoundButton = CreateButton("Next Round Button", shopRoot, HandleNextRoundClicked);
        AddLayoutElement(nextRoundButton.gameObject, 92f, 18f, 0f);
        nextRoundButtonText = nextRoundButton.GetComponentInChildren<TMP_Text>();
        shopRoot.gameObject.SetActive(false);
    }

    private void EnsureTooltip()
    {
        if (tooltipView != null)
            return;

        RectTransform tooltipRect = FindOrCreateRect("Encounter Shop Tooltip", ResolveTooltipParent());
        tooltipView = tooltipRect.gameObject.AddComponent<ShopTooltipView>();
        tooltipView.Hide();
    }

    private void HandleNextRoundClicked()
    {
        nextRoundAction?.Invoke();
    }

    private void SetShopRootVisible(bool visible)
    {
        EnsureLayout();

        if (shopRoot != null)
            shopRoot.gameObject.SetActive(visible);

        if (!visible && tooltipView != null)
            tooltipView.Hide();
    }

    private void SetDialogTextVisible(bool visible)
    {
        if (bossCommentText != null)
            bossCommentText.gameObject.SetActive(visible);
    }

    private TMP_Text CreateText(string objectName, Transform parent, int fontSize, FontStyles style)
    {
        RectTransform textRect = FindOrCreateRect(objectName, parent);
        TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(textRect.gameObject);
        UiTheme.Style(text, fontSize, style, UiTheme.TextPrimary);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, UnityAction onClick)
    {
        RectTransform buttonRect = FindOrCreateRect(objectName, parent);
        Image image = GetOrAddComponent<Image>(buttonRect.gameObject);
        image.color = UiTheme.ButtonBg;

        Button button = GetOrAddComponent<Button>(buttonRect.gameObject);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);

        TMP_Text text = buttonRect.GetComponentInChildren<TMP_Text>();

        if (text == null)
            text = CreateText("Text", buttonRect, UiTheme.Label, FontStyles.Normal);

        ConfigureRect(text.rectTransform, Vector2.zero, Vector2.one);
        UiTheme.Style(text, UiTheme.Label, FontStyles.Normal, UiTheme.TextPrimary, autoSize: true);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return button;
    }

    private T GetOrCreateChildComponent<T>(string objectName, Transform parent) where T : Component
    {
        RectTransform rect = FindOrCreateRect(objectName, parent);
        return GetOrAddComponent<T>(rect.gameObject);
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }

    private RectTransform FindOrCreateRect(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);

        if (existing is RectTransform existingRect)
            return existingRect;

        GameObject rectObject = new(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return (RectTransform)rectObject.transform;
    }

    private void ConfigureText(TMP_Text text, Vector2 anchorMin, Vector2 anchorMax, int fontSize, TextAlignmentOptions alignment)
    {
        ConfigureRect(text.rectTransform, anchorMin, anchorMax);
        UiTheme.Style(text, fontSize, text.fontStyle, UiTheme.TextPrimary);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private void ConfigureRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private void AddLayoutElement(GameObject target, float preferredWidth, float preferredHeight, float flexibleWidth)
    {
        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(target);
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleWidth = flexibleWidth;
        layoutElement.flexibleHeight = 0f;
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

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}

// hover target carrying prebuilt multiline text (aura plus skills) for the boss tooltip
public class BossInfoHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private string infoText;
    private RunProgressTooltipView tooltipView;

    public void Bind(string text, RunProgressTooltipView tooltip)
    {
        infoText = text;
        tooltipView = tooltip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(infoText))
            tooltipView?.Show(infoText, eventData.position);
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
        if (tooltipView != null)
            tooltipView.Hide();
    }
}

public class RunProgressView : MonoBehaviour
{
    private readonly List<RuntimeRunNode> nodes = new();
    private readonly List<Image> nodeImages = new();
    private readonly List<TMP_Text> nodeLabels = new();
    private readonly List<TMP_Text> arrowLabels = new();
    private RunProgressTooltipView tooltipView;
    private RunUiArtCatalog uiArtCatalog;
    private int currentIndex;
    private int completedCount;

    private void Awake()
    {
        EnsureLayout();
    }

    public void BindRoute(IReadOnlyList<RuntimeRunNode> routeNodes)
    {
        EnsureLayout();
        nodes.Clear();

        if (routeNodes != null)
        {
            for (int i = 0; i < routeNodes.Count; i++)
            {
                nodes.Add(routeNodes[i]);
            }
        }

        RebuildNodes();
        Refresh();
    }

    public void SetArtCatalog(RunUiArtCatalog catalog)
    {
        uiArtCatalog = catalog;
        RefreshNodeArt();
        Refresh();
    }

    public void SetProgress(int progressIndex, int completedNodeCount)
    {
        currentIndex = Mathf.Clamp(progressIndex, 0, Mathf.Max(0, nodes.Count - 1));
        completedCount = Mathf.Clamp(completedNodeCount, 0, nodes.Count);
        Refresh();
    }

    private void EnsureLayout()
    {
        HorizontalLayoutGroup layout = GetOrAddComponent<HorizontalLayoutGroup>(gameObject);
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (tooltipView == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform tooltipParent = canvas != null ? canvas.transform : transform;
            GameObject tooltipObject = new("Run Progress Tooltip", typeof(RectTransform));
            tooltipObject.transform.SetParent(tooltipParent, false);
            tooltipView = tooltipObject.AddComponent<RunProgressTooltipView>();
            tooltipView.Hide();
        }
    }

    private void RebuildNodes()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        nodeImages.Clear();
        nodeLabels.Clear();
        arrowLabels.Clear();

        for (int i = 0; i < nodes.Count; i++)
        {
            CreateNode(nodes[i]);

            if (i < nodes.Count - 1)
                CreateArrow();
        }
    }

    private void CreateNode(RuntimeRunNode node)
    {
        GameObject nodeObject = new("Progress Node", typeof(RectTransform));
        nodeObject.transform.SetParent(transform, false);

        RectTransform nodeRect = (RectTransform)nodeObject.transform;
        nodeRect.sizeDelta = new Vector2(18f, 18f);

        Image image = nodeObject.AddComponent<Image>();
        image.raycastTarget = true;
        image.preserveAspect = true;

        LayoutElement layoutElement = nodeObject.AddComponent<LayoutElement>();
        // long routes squeeze nodes down toward minWidth so the strip never overflows
        layoutElement.minWidth = 11f;
        layoutElement.preferredWidth = 18f;
        layoutElement.minHeight = 18f;
        layoutElement.preferredHeight = 18f;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        TextMeshProUGUI label = CreateCenteredText("Label", nodeObject.transform, 10);
        SetNodeArt(image, label, node.NodeType);

        RunProgressHoverTarget hoverTarget = nodeObject.AddComponent<RunProgressHoverTarget>();
        hoverTarget.Bind(node.DisplayNameKey, tooltipView);

        nodeImages.Add(image);
        nodeLabels.Add(label);
    }

    private void CreateArrow()
    {
        GameObject arrowObject = new("Progress Arrow", typeof(RectTransform));
        arrowObject.transform.SetParent(transform, false);

        RectTransform arrowRect = (RectTransform)arrowObject.transform;
        arrowRect.sizeDelta = new Vector2(8f, 18f);

        LayoutElement layoutElement = arrowObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = 4f;
        layoutElement.preferredWidth = 8f;
        layoutElement.minHeight = 18f;
        layoutElement.preferredHeight = 18f;
        layoutElement.flexibleWidth = 0f;
        layoutElement.flexibleHeight = 0f;

        TextMeshProUGUI arrow = CreateCenteredText("Arrow", arrowObject.transform, 10);
        arrow.text = ">";
        arrowLabels.Add(arrow);
    }

    private void Refresh()
    {
        RefreshNodeArt();

        for (int i = 0; i < nodeImages.Count; i++)
        {
            bool completed = i < completedCount;
            bool current = i == currentIndex && completedCount <= i;
            nodeImages[i].color = completed
                ? UiTheme.NodeDone
                : current
                    ? UiTheme.NodeCurrent
                    : Color.white;
            nodeImages[i].rectTransform.localScale = current ? Vector3.one * 1.14f : Vector3.one;
            nodeLabels[i].color = completed ? UiTheme.TextMuted : current ? UiTheme.TextPrimary : UiTheme.TextInverse;
        }

        for (int i = 0; i < arrowLabels.Count; i++)
        {
            bool pointsToFinalBoss = i + 1 < nodes.Count && nodes[i + 1].NodeType == RunNodeType.FinalBoss;
            bool reachedLeadIn = currentIndex >= i || completedCount >= i + 1;
            arrowLabels[i].color = pointsToFinalBoss && reachedLeadIn
                ? UiTheme.AccentDanger
                : UiTheme.PanelBgDark;
        }
    }

    private void RefreshNodeArt()
    {
        for (int i = 0; i < nodeImages.Count && i < nodes.Count; i++)
        {
            SetNodeArt(nodeImages[i], nodeLabels[i], nodes[i].NodeType);
        }
    }

    private void SetNodeArt(Image image, TMP_Text label, RunNodeType nodeType)
    {
        Sprite sprite = ResolveUiArtCatalog()?.GetProgressNodeSprite(nodeType);

        if (sprite != null)
        {
            image.sprite = sprite;
            label.gameObject.SetActive(false);
            return;
        }

        image.sprite = null;
        label.gameObject.SetActive(true);
        label.text = GetIcon(nodeType);
    }

    private TextMeshProUGUI CreateCenteredText(string objectName, Transform parent, int fontSize)
    {
        GameObject textObject = new(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = (RectTransform)textObject.transform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.ApplyFont(text);
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 7f;
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private string GetIcon(RunNodeType nodeType)
    {
        return nodeType switch
        {
            RunNodeType.FinalBoss => "X",
            RunNodeType.MiniBoss => "!",
            _ => "?"
        };
    }

    private RunUiArtCatalog ResolveUiArtCatalog()
    {
        if (uiArtCatalog == null)
            uiArtCatalog = RunUiArtCatalog.ResolveDefault();

        return uiArtCatalog;
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }
}

public class RunProgressHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private string labelKey;
    private RunProgressTooltipView tooltipView;

    public void Bind(string displayNameKey, RunProgressTooltipView tooltip)
    {
        labelKey = displayNameKey;
        tooltipView = tooltip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        tooltipView?.Show(Loc.T(labelKey), eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltipView?.Hide();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        tooltipView?.MoveTo(eventData.position);
    }
}

public class RunProgressTooltipView : MonoBehaviour
{
    private RectTransform rectTransform;
    private TMP_Text labelText;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        EnsureUi();
        Hide();
    }

    public void Show(string text, Vector2 screenPosition)
    {
        EnsureUi();
        labelText.text = text;
        canvasGroup.alpha = 1f;
        MoveTo(screenPosition);
    }

    public void MoveTo(Vector2 screenPosition)
    {
        if (canvasGroup == null || canvasGroup.alpha <= 0f)
            return;

        EnsureUi();

        if (rectTransform.parent is not RectTransform parentRect)
            return;

        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, camera, out Vector2 localPoint))
            return;

        Vector2 halfSize = rectTransform.sizeDelta * 0.5f;
        Rect parentBounds = parentRect.rect;
        Vector2 anchoredPosition = new(localPoint.x, localPoint.y - halfSize.y - 14f);
        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, parentBounds.xMin + halfSize.x, parentBounds.xMax - halfSize.x);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, parentBounds.yMin + halfSize.y, parentBounds.yMax - halfSize.y);
        rectTransform.anchoredPosition = anchoredPosition;
    }

    public void Hide()
    {
        // same alpha trick as the shop tooltip, SetActive ghosting
        EnsureUi();
        canvasGroup.alpha = 0f;
    }

    private void EnsureUi()
    {
        if (rectTransform != null)
            return;

        rectTransform = GetOrAddComponent<RectTransform>(gameObject);
        // roomy enough for piece descs, short labels just center in it
        rectTransform.sizeDelta = new Vector2(170f, 44f);
        canvas = GetComponentInParent<Canvas>();

        Image image = GetOrAddComponent<Image>(gameObject);
        image.color = UiTheme.TooltipBg;
        image.raycastTarget = false;

        canvasGroup = GetOrAddComponent<CanvasGroup>(gameObject);
        canvasGroup.blocksRaycasts = false;

        GameObject textObject = new("Text", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 3f);
        textRect.offsetMax = new Vector2(-8f, -3f);

        labelText = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(labelText, UiTheme.Body, FontStyles.Normal, UiTheme.TextInverse, autoSize: true);
        // descs can run long, let them wrap and shrink to fit the bubble
        labelText.fontSizeMin = 5f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.textWrappingMode = TextWrappingModes.Normal;
        labelText.raycastTarget = false;
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }
}
