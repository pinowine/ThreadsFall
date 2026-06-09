using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudStatRowView : MonoBehaviour
{
    private Image iconImage;
    private AnimatedImageView iconAnimator;
    private TMP_Text labelText;
    private TMP_Text valueText;

    private void Awake()
    {
        EnsureUi();
    }

    public void Bind(SpriteSequenceDefinition iconSequence, string label, string value)
    {
        EnsureUi();

        iconAnimator.SetSequence(iconSequence);
        iconImage.enabled = iconSequence != null && iconSequence.FirstFrame != null;
        labelText.text = label;
        valueText.text = value;
    }

    private void EnsureUi()
    {
        if (iconImage != null && labelText != null && valueText != null)
            return;

        RectTransform rectTransform = GetOrAddComponent<RectTransform>(gameObject);
        rectTransform.sizeDelta = new Vector2(0f, 11f);

        LayoutElement rowLayout = GetOrAddComponent<LayoutElement>(gameObject);
        rowLayout.minHeight = 11f;
        rowLayout.preferredHeight = 11f;
        rowLayout.flexibleHeight = 0f;

        HorizontalLayoutGroup layout = GetOrAddComponent<HorizontalLayoutGroup>(gameObject);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        iconImage = CreateIcon();
        iconAnimator = GetOrAddComponent<AnimatedImageView>(iconImage.gameObject);
        GetOrAddComponent<HoverAnimatedImageTrigger>(iconImage.gameObject);
        labelText = CreateText("Label", UiTheme.Small, FontStyles.Normal, 48f, TextAlignmentOptions.Left);
        valueText = CreateText("Value", UiTheme.Small, FontStyles.Bold, 26f, TextAlignmentOptions.Right);
    }

    private Image CreateIcon()
    {
        GameObject iconObject = new("Icon", typeof(RectTransform));
        iconObject.transform.SetParent(transform, false);

        Image image = iconObject.AddComponent<Image>();
        image.raycastTarget = true;
        image.preserveAspect = true;

        LayoutElement layoutElement = iconObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = 9f;
        layoutElement.preferredWidth = 9f;
        layoutElement.minHeight = 9f;
        layoutElement.preferredHeight = 9f;
        layoutElement.flexibleWidth = 0f;
        return image;
    }

    private TMP_Text CreateText(string objectName, int fontSize, FontStyles style, float width, TextAlignmentOptions alignment)
    {
        GameObject textObject = new(objectName, typeof(RectTransform));
        textObject.transform.SetParent(transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        UiTheme.Style(text, fontSize, style, UiTheme.TextInverse);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = objectName == "Label" ? 1f : 0f;
        return text;
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }
}
