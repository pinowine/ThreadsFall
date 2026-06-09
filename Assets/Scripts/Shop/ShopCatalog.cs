using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Threads Fall/Shop Catalog")]
public class ShopCatalog : ScriptableObject
{
    public List<ShopItemDefinition> items = new();

    public void AppendAvailableItems(List<ShopItemDefinition> results, int roundIndex, ISet<string> excludedItemIds)
    {
        if (results == null)
            return;

        results.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            ShopItemDefinition item = items[i];

            if (item == null || !item.IsValid)
                continue;

            if (item.minRound > roundIndex)
                continue;

            if (excludedItemIds != null && excludedItemIds.Contains(item.itemId))
                continue;

            results.Add(item);
        }
    }
}
