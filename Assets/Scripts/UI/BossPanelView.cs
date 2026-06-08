using TMPro;
using UnityEngine;

public class BossPanelView : MonoBehaviour
{
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private TMP_Text bossCommentText;
    [SerializeField] private TMP_Text bossEffectText;

    private BossId currentBossId = BossId.None;

    private void OnEnable()
    {
        Loc.OnLocaleChanged += HandleLocaleChanged;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.OnLocaleChanged -= HandleLocaleChanged;
    }

    public void ShowBoss(BossId bossId)
    {
        currentBossId = bossId;
        Refresh();
    }

    private void HandleLocaleChanged(string locale)
    {
        Refresh();
    }

    private void Refresh()
    {
        string bossSegment = currentBossId.ToLocSegment();
        string comment = Loc.T("boss." + bossSegment + ".comment.round_start");
        string effect = Loc.T("boss.effect.none");

        if (bossNameText != null)
            bossNameText.text = Loc.T("boss." + bossSegment + ".name");

        // if there is no separate effect field, fold the effect line into the comment block
        if (bossCommentText != null)
            bossCommentText.text = bossEffectText == null ? comment + "\n" + effect : comment;

        if (bossEffectText != null)
            bossEffectText.text = effect;
    }
}
