using UnityEngine;
using UnityEngine.UI;

// one purchased item in the hud: just the icon, full story lives in the hover tooltip
// gray = already spent, green frame = run-long perk still working
public class PurchasedItemIconView : MonoBehaviour
{
    private static readonly Color ConsumedTint = new(0.42f, 0.42f, 0.42f, 0.9f);
    private static readonly Color MissingIconColor = new(0.45f, 0.48f, 0.52f, 1f);

    private Image frameImage;
    private Image iconImage;
    private ShopItemHoverTarget hoverTarget;
    private HoverScaleEffect hoverScale;

    public void Bind(ShopItemDefinition item, ShopTooltipView tooltipView, ItemPerkStatus status)
    {
        EnsureUi();

        bool hasItem = item != null;
        iconImage.enabled = hasItem;
        iconImage.sprite = hasItem ? item.icon : null;

        Color tint = status == ItemPerkStatus.Consumed ? ConsumedTint : Color.white;
        iconImage.color = hasItem && item.icon != null ? tint : MissingIconColor;

        frameImage.enabled = status == ItemPerkStatus.Persistent;
        frameImage.color = UiTheme.AccentSafe;

        hoverTarget.Bind(item, tooltipView, status == ItemPerkStatus.Consumed);
    }

    private void EnsureUi()
    {
        if (iconImage != null)
            return;

        // the frame doubles as the hover hit area
        frameImage = GetOrAddComponent<Image>(gameObject);
        frameImage.raycastTarget = true;

        GameObject iconObject = new("Icon", typeof(RectTransform));
        iconObject.transform.SetParent(transform, false);

        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(1f, 1f);
        iconRect.offsetMax = new Vector2(-1f, -1f);

        iconImage = iconObject.AddComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;

        hoverTarget = GetOrAddComponent<ShopItemHoverTarget>(gameObject);
        hoverScale = GetOrAddComponent<HoverScaleEffect>(gameObject);
        hoverScale.SetHoverScale(1.25f);
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }
}
