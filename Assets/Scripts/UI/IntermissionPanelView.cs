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

    private PanelMode currentMode = PanelMode.Shop;
    private RunStatsController currentStats;
    private string shopPrompt;

    private void OnEnable()
    {
        Loc.OnLocaleChanged += HandleLocaleChanged;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.OnLocaleChanged -= HandleLocaleChanged;
    }

    public void ShowShop(RunStatsController stats, bool canContinue)
    {
        currentMode = PanelMode.Shop;
        currentStats = stats;
        shopPrompt = string.Empty;
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(canContinue);
        Refresh();
    }

    public void ShowShop(RunStatsController stats, IReadOnlyList<string> options, bool canContinue)
    {
        currentMode = PanelMode.Shop;
        currentStats = stats;
        shopPrompt = FormatShopOptions(options);
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(canContinue);
        Refresh();
    }

    public void ShowShopResult(RunStatsController stats, string result)
    {
        currentMode = PanelMode.Shop;
        currentStats = stats;
        shopPrompt = result;
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(true);
        Refresh();
    }

    public void ShowRunComplete(RunStatsController stats)
    {
        currentMode = PanelMode.RunComplete;
        currentStats = stats;
        shopPrompt = string.Empty;
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(true);
        Refresh();
    }

    public void ShowGameOver(RunStatsController stats)
    {
        currentMode = PanelMode.GameOver;
        currentStats = stats;
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

    private void HandleLocaleChanged(string locale)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (nextRoundButtonText != null)
            nextRoundButtonText.text = currentMode == PanelMode.GameOver || currentMode == PanelMode.RunComplete
                ? "Restart"
                : Loc.T("shop.next_round");

        switch (currentMode)
        {
            case PanelMode.Shop:
                SetText(titleText, Loc.T("shop.placeholder.title"));
                SetText(bodyText, FormatShopBody());
                break;
            case PanelMode.RunComplete:
                SetText(titleText, Loc.T("run.complete"));
                SetText(bodyText, FormatStats("run.complete.body"));
                break;
            case PanelMode.GameOver:
                SetText(titleText, Loc.T("run.game_over"));
                SetText(bodyText, FormatStats("run.game_over.body"));
                break;
        }
    }

    private string FormatShopBody()
    {
        string body = FormatShopStats();

        if (string.IsNullOrWhiteSpace(shopPrompt))
            return body;

        return shopPrompt + "\n\n" + body;
    }

    private string FormatShopStats()
    {
        if (currentStats == null)
            return string.Empty;

        return string.Format(
            "Score: {0}\nAttention: {1}\nLines: {2}\nComposure: {3}\nNoise: {4}",
            currentStats.Score,
            currentStats.Attention,
            currentStats.TotalLinesCleared,
            currentStats.Composure,
            currentStats.Noise
        );
    }

    private string FormatShopOptions(IReadOnlyList<string> options)
    {
        if (options == null || options.Count <= 0)
            return "Press 4 or 0 to skip";

        string text = "Choose one shop option\n";

        for (int i = 0; i < options.Count; i++)
        {
            text += $"{i + 1}: {options[i]}\n";
        }

        return text + "4/0: Skip";
    }

    private string FormatStats(string key)
    {
        // the same stat payload feeds shop, completion, and failure copy
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

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
