using System.Collections.Generic;
using UnityEngine;

// Single registry of every Item asset. Recipes, drops and saves refer to items by id and look them up here.
// Create one asset (Create > Soul > Item Database) and assign it to InventoryManager.
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Soul/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<Item> items = new List<Item>();

    private Dictionary<int, Item> lookup;

    public Item Get(int id)
    {
        if (lookup == null) BuildLookup();
        lookup.TryGetValue(id, out Item item);
        return item;
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<int, Item>();
        foreach (Item item in items)
        {
            if (item == null) continue;
            if (lookup.ContainsKey(item.id))
            {
                Debug.LogError($"[ItemDatabase] Duplicate item id {item.id}: '{lookup[item.id].name}' and '{item.name}'", this);
                continue;
            }
            lookup.Add(item.id, item);
        }
    }

    private void OnEnable() { lookup = null; }

    private void OnValidate()
    {
        lookup = null;
        BuildLookup();
    }
}
