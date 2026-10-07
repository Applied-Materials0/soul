using System.Linq;
using UnityEditor;
using UnityEngine;

// MonsterDatabase에 "Collect All Monsters" 버튼을 추가해 몬스터 에셋을 빠뜨리지 않게 한다.
[CustomEditor(typeof(MonsterDatabase))]
public class MonsterDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Collect All Monsters (프로젝트의 모든 Monster 에셋 찾기)"))
        {
            var db = (MonsterDatabase)target;
            Undo.RecordObject(db, "Collect All Monsters");
            db.monsters = AssetDatabase.FindAssets("t:Monster")
                .Select(guid => AssetDatabase.LoadAssetAtPath<Monster>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(m => m != null)
                .OrderBy(m => m.id)
                .ToList();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }
    }
}
