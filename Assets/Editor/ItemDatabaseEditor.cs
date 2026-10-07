using System.Linq;
using UnityEditor;
using UnityEngine;

// Adds a "Collect All Items" button to ItemDatabase so no item asset is ever forgotten.
[CustomEditor(typeof(ItemDatabase))]
public class ItemDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Collect All Items (find every Item asset in project)"))
        {
            var db = (ItemDatabase)target;
            Undo.RecordObject(db, "Collect All Items");
            db.items = AssetDatabase.FindAssets("t:Item")
                .Select(guid => AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null)
                .OrderBy(item => item.id)
                .ToList();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }
    }
}
