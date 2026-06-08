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
        SetNextRoundButtonVisible(true);
        SetNextRoundButtonInteractable(canContinue);
        Refresh();
    }

    public void ShowRunComplete(RunStatsController stats)
    {
        currentMode = PanelMode.RunComplete;
        currentStats = stats;
        SetNextRoundButtonVisible(false);
        Refresh();
    }

    public void ShowGameOver(RunStatsController stats)
    {
        currentMode = PanelMode.GameOver;
        currentStats = stats;
        SetNextRoundButtonVisible(false);
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
            nextRoundButtonText.text = Loc.T("shop.next_round");

        switch (currentMode)
        {
            case PanelMode.Shop:
                SetText(titleText, Loc.T("shop.placeholder.title"));
                SetText(bodyText, FormatStats("shop.placeholder.body"));
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
