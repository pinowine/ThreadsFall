using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BossCatalog", menuName = "Threads Fall/Boss Catalog")]
public class BossCatalog : ScriptableObject
{
    private const string DefaultResourcePath = "Data/BossCatalog";
    private static BossCatalog cachedDefault;

    public List<BossDefinition> bosses = new();

    public static BossCatalog ResolveDefault()
    {
        if (cachedDefault == null)
            cachedDefault = Resources.Load<BossCatalog>(DefaultResourcePath);

        return cachedDefault;
    }

    public void AppendAvailableBosses(List<BossDefinition> results, RunNodeType nodeType, ISet<string> excludedBossKeys)
    {
        if (results == null)
            return;

        results.Clear();

        for (int i = 0; i < bosses.Count; i++)
        {
            BossDefinition boss = bosses[i];

            if (boss == null || !boss.IsValid || boss.nodeType != nodeType)
                continue;

            if (excludedBossKeys != null && excludedBossKeys.Contains(boss.bossKey))
                continue;

            results.Add(boss);
        }
    }

    public BossDefinition FindById(BossId bossId)
    {
        for (int i = 0; i < bosses.Count; i++)
        {
            BossDefinition boss = bosses[i];

            if (boss != null && boss.bossId == bossId)
                return boss;
        }

        return null;
    }
}
