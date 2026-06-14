using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RunShopController : MonoBehaviour
{
    private const string DefaultCatalogResourcePath = "Data/Shop/ShopCatalog";

    [SerializeField] private ShopCatalog catalog;
    [SerializeField] private int shelfSlotCount = 3;
    [SerializeField] private string editorDefaultCatalogPath = "Assets/Resources/Data/Shop/ShopCatalog.asset";

    public event Action ShopChanged;
    public event Action<string> PurchaseFeedback;
    public event Action PurchaseHistoryChanged;

    public int ShelfSlotCount => Mathf.Max(1, shelfSlotCount);
    public IReadOnlyList<ShopItemDefinition> PurchasedItems => purchasedItems;

    private readonly List<ShopItemDefinition> shelfItems = new();
    private readonly List<bool> shelfLocks = new();
    private readonly List<ShopItemDefinition> purchasedItems = new();
    private readonly List<ShopItemDefinition> reusableAvailableItems = new();
    private readonly HashSet<string> purchasedUniqueItemIds = new();
    private readonly HashSet<string> reusableExcludedItemIds = new();
    private RunStatsController statsController;
    private RunEffectSystem effectController;
    private System.Random random;
    private int currentRoundIndex;

    public void Initialize(RunStatsController stats, RunEffectSystem effects)
    {
        statsController = stats;
        effectController = effects;
        random ??= new System.Random(Guid.NewGuid().GetHashCode());
        EnsureShelfSlots();
    }

    public void ResetRun()
    {
        random = new System.Random(Guid.NewGuid().GetHashCode());
        purchasedUniqueItemIds.Clear();
        purchasedItems.Clear();
        shelfItems.Clear();
        shelfLocks.Clear();
        EnsureShelfSlots();
        PurchaseHistoryChanged?.Invoke();
        ShopChanged?.Invoke();
    }

    public void OpenShop(int roundIndex)
    {
        currentRoundIndex = Mathf.Max(0, roundIndex);
        RefreshShelf();
        ShopChanged?.Invoke();
    }

    public ShopItemDefinition GetShelfItem(int slotIndex)
    {
        EnsureShelfSlots();
        return slotIndex >= 0 && slotIndex < shelfItems.Count ? shelfItems[slotIndex] : null;
    }

    public bool IsShelfSlotLocked(int slotIndex)
    {
        EnsureShelfSlots();
        return slotIndex >= 0 && slotIndex < shelfLocks.Count && shelfLocks[slotIndex] && shelfItems[slotIndex] != null;
    }

    public void ToggleShelfLock(int slotIndex)
    {
        EnsureShelfSlots();

        if (slotIndex < 0 || slotIndex >= shelfLocks.Count || shelfItems[slotIndex] == null)
            return;

        shelfLocks[slotIndex] = !shelfLocks[slotIndex];
        ShopChanged?.Invoke();
    }

    public int GetModifiedCost(ShopItemDefinition item)
    {
        if (item == null)
            return 0;

        int baseCost = Mathf.Max(0, item.baseAttentionCost);
        return statsController != null ? statsController.GetModifiedShopCost(baseCost) : baseCost;
    }

    public bool CanBuy(ShopItemDefinition item)
    {
        if (item == null || statsController == null)
            return false;

        return statsController.Attention >= GetModifiedCost(item);
    }

    public bool TryBuyShelfSlot(int slotIndex)
    {
        ShopItemDefinition item = GetShelfItem(slotIndex);

        if (!TryBuy(item))
            return false;

        shelfItems[slotIndex] = null;
        shelfLocks[slotIndex] = false;
        ShopChanged?.Invoke();
        return true;
    }

    private bool TryBuy(ShopItemDefinition item)
    {
        if (item == null || statsController == null)
            return false;

        int cost = GetModifiedCost(item);
        int attentionBeforePurchase = statsController.Attention;

        if (!statsController.TrySpendAttention(cost))
        {
            PurchaseFeedback?.Invoke(Loc.Format("shop.purchase.not_enough_attention", Loc.T(item.nameKey), cost));
            ShopChanged?.Invoke();
            return false;
        }

        effectController?.QueuePressureReliefFromPurchase(item, attentionBeforePurchase);
        bool removeEffectFailed = ApplyItem(item);

        purchasedItems.Add(item);
        PurchaseHistoryChanged?.Invoke();

        if (item.uniquePerRun)
            purchasedUniqueItemIds.Add(item.itemId);

        string feedback = Loc.Format("shop.purchase.success", Loc.T(item.nameKey), cost);

        if (removeEffectFailed)
            feedback += "\n" + Loc.T("shop.effect.remove_failed");

        PurchaseFeedback?.Invoke(feedback);
        return true;
    }

    private bool ApplyItem(ShopItemDefinition item)
    {
        if (item.setNoiseToZero)
            statsController.SetNoise(0);

        if (item.setComposureToMax)
            statsController.SetComposure(statsController.MaxComposure);

        if (item.noiseDelta != 0)
            statsController.ApplyNoiseDelta(item.noiseDelta);

        if (item.composureDelta != 0)
            statsController.ApplyComposureDelta(item.composureDelta);

        bool removeEffectFailed = false;

        if (effectController == null || item.effects == null)
            return removeEffectFailed;

        for (int i = 0; i < item.effects.Count; i++)
        {
            ShopEffectSpec effect = item.effects[i];

            if (effect == null)
                continue;

            // legacy shop specs flow through the shared effect pipeline now
            removeEffectFailed |= !effectController.Apply(effect.ToEffectSpec(), item.nameKey);
        }

        return removeEffectFailed;
    }

    private void RefreshShelf()
    {
        EnsureShelfSlots();

        for (int i = 0; i < shelfItems.Count; i++)
        {
            if (IsShelfSlotLocked(i))
                continue;

            shelfItems[i] = null;
            shelfLocks[i] = false;
        }

        for (int i = 0; i < shelfItems.Count; i++)
        {
            if (shelfItems[i] != null)
                continue;

            shelfItems[i] = SelectShelfItem();
        }
    }

    private ShopItemDefinition SelectShelfItem()
    {
        ShopCatalog activeCatalog = ResolveCatalog();

        if (activeCatalog == null)
            return null;

        BuildExcludedIds();
        activeCatalog.AppendAvailableItems(reusableAvailableItems, currentRoundIndex, reusableExcludedItemIds);

        if (reusableAvailableItems.Count <= 0)
            return null;

        float totalWeight = 0f;

        for (int i = 0; i < reusableAvailableItems.Count; i++)
        {
            totalWeight += Mathf.Max(0f, reusableAvailableItems[i].shelfWeight);
        }

        if (totalWeight <= 0f)
            return null;

        float roll = (float)(random.NextDouble() * totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < reusableAvailableItems.Count; i++)
        {
            ShopItemDefinition item = reusableAvailableItems[i];
            cumulative += Mathf.Max(0f, item.shelfWeight);

            if (roll <= cumulative)
                return item;
        }

        return reusableAvailableItems[reusableAvailableItems.Count - 1];
    }

    private void BuildExcludedIds()
    {
        reusableExcludedItemIds.Clear();

        foreach (string purchasedId in purchasedUniqueItemIds)
        {
            reusableExcludedItemIds.Add(purchasedId);
        }

        for (int i = 0; i < shelfItems.Count; i++)
        {
            ShopItemDefinition shelfItem = shelfItems[i];

            if (shelfItem != null && shelfItem.IsValid)
                reusableExcludedItemIds.Add(shelfItem.itemId);
        }
    }

    private void EnsureShelfSlots()
    {
        int count = ShelfSlotCount;

        while (shelfItems.Count < count)
        {
            shelfItems.Add(null);
        }

        while (shelfLocks.Count < count)
        {
            shelfLocks.Add(false);
        }

        while (shelfItems.Count > count)
        {
            shelfItems.RemoveAt(shelfItems.Count - 1);
        }

        while (shelfLocks.Count > count)
        {
            shelfLocks.RemoveAt(shelfLocks.Count - 1);
        }

        for (int i = 0; i < shelfLocks.Count; i++)
        {
            if (shelfItems[i] == null)
                shelfLocks[i] = false;
        }
    }

    private ShopCatalog ResolveCatalog()
    {
        if (catalog != null)
            return catalog;

        catalog = Resources.Load<ShopCatalog>(DefaultCatalogResourcePath);

#if UNITY_EDITOR
        if (catalog == null)
            catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(editorDefaultCatalogPath);
#endif

        if (catalog == null)
            Debug.LogWarning("Shop catalog is missing.");

        return catalog;
    }
}
