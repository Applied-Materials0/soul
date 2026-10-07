using System.Collections.Generic;
using UnityEngine;

// 모든 Monster 에셋의 목록. 코드는 id나 지역으로 여기서 몬스터를 찾는다.
// 에셋을 하나 만들고(Create > Soul > Monster Database) "Collect All Monsters" 버튼으로 채운 뒤 FieldSearch에 연결한다.
[CreateAssetMenu(fileName = "MonsterDatabase", menuName = "Soul/Monster Database")]
public class MonsterDatabase : ScriptableObject
{
    public List<Monster> monsters = new List<Monster>();

    private Dictionary<int, Monster> lookup;

    public Monster Get(int id)
    {
        if (lookup == null) BuildLookup();
        lookup.TryGetValue(id, out Monster monster);
        return monster;
    }

    // 지역에 출현하는 몬스터 중 비중(weight)대로 하나를 뽑는다. 출현하는 몬스터가 없으면 null
    public Monster PickForRegion(int regionId)
    {
        int total = 0;
        foreach (Monster m in monsters)
            if (m != null) total += WeightIn(m, regionId);
        if (total <= 0) return null;

        int roll = Random.Range(0, total);
        foreach (Monster m in monsters)
        {
            if (m == null) continue;
            int w = WeightIn(m, regionId);
            if (roll < w) return m;
            roll -= w;
        }
        return null;
    }

    // 이 몬스터가 해당 지역에서 가지는 비중 (같은 지역이 여러 줄이면 합산)
    private static int WeightIn(Monster m, int regionId)
    {
        int sum = 0;
        foreach (MonsterSpawn s in m.spawns)
            if (s != null && s.regionId == regionId) sum += Mathf.Max(0, s.weight);
        return sum;
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<int, Monster>();
        foreach (Monster m in monsters)
        {
            if (m == null) continue;
            if (lookup.ContainsKey(m.id))
            {
                Debug.LogError($"[MonsterDatabase] Duplicate monster id {m.id}: '{lookup[m.id].name}' and '{m.name}'", this);
                continue;
            }
            lookup.Add(m.id, m);
        }
    }

    private void OnEnable() { lookup = null; }

    private void OnValidate()
    {
        lookup = null;
        BuildLookup();
    }
}
