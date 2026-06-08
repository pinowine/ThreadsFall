using TMPro;
using UnityEngine;

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

    private bool statsSubscribed;

    private void OnEnable()
    {
        Loc.OnLocaleChanged += HandleLocaleChanged;
        SubscribeStats();
        Refresh();
    }

    private void OnDisable()
    {
        Loc.OnLocaleChanged -= HandleLocaleChanged;
        UnsubscribeStats();
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

    private void HandleStatsChanged(RunStatsController stats)
    {
        Refresh();
    }

    private void HandleLocaleChanged(string locale)
    {
        Refresh();
    }

    private void Refresh()
    {
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
            // one combined block keeps small HUD prefabs usable
            combinedStatsText.text =
                Loc.Format("hud.round", statsController.CurrentRoundIndex) + "\n" +
                Loc.Format("hud.attention", statsController.Attention) + "\n" +
                Loc.Format("hud.composure", statsController.Composure) + "\n" +
                Loc.Format("hud.noise", statsController.Noise) + "\n" +
                Loc.Format("hud.score", statsController.Score) + "\n" +
                Loc.Format("hud.lines", statsController.TotalLinesCleared);
        }
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
