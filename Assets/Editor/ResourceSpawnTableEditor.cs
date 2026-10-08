using UnityEditor;
using UnityEngine;

// ResourceSpawnTable 인스펙터를 "한 줄 = 자원 한 종류"인 표로 보여 준다. (표 그리기는 ResourceTableDrawer)
[CustomEditor(typeof(ResourceSpawnTable))]
public class ResourceSpawnTableEditor : Editor
{
    private Vector2 scroll;

    private void OnEnable()
    {
        ResourceTableDrawer.RefreshNames();
    }

    public override void OnInspectorGUI()
    {
        ResourceTableDrawer.Draw(serializedObject, ref scroll, false);
    }
}
