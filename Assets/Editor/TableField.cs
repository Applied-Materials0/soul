using UnityEditor;
using UnityEngine;

// 표의 칸 하나를 "한 줄, 정해진 너비"로 그리는 공용 도구.
// EditorGUILayout.PropertyField는 필드에 붙은 [Header("...")] 제목까지 같이 그려서 그 칸만 한 줄 아래로 밀리므로,
// 타입별로 입력칸만 직접 그린다. (표에서는 [Min]/[Range] 같은 제한이 적용되지 않으니 필요하면 호출한 쪽에서 보정)
public static class TableField
{
    public static void Draw(SerializedProperty p, float width)
    {
        if (p == null)
        {
            GUILayout.Label("?", GUILayout.Width(width));
            return;
        }

        GUILayoutOption w = GUILayout.Width(width);
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer:
                p.intValue = EditorGUILayout.IntField(p.intValue, w);
                break;
            case SerializedPropertyType.Float:
                p.floatValue = EditorGUILayout.FloatField(p.floatValue, w);
                break;
            case SerializedPropertyType.String:
                p.stringValue = EditorGUILayout.TextField(p.stringValue, w);
                break;
            case SerializedPropertyType.Boolean:
                p.boolValue = EditorGUILayout.Toggle(p.boolValue, w);
                break;
            case SerializedPropertyType.Enum:
                p.enumValueIndex = EditorGUILayout.Popup(p.enumValueIndex, p.enumDisplayNames, w);
                break;
            case SerializedPropertyType.ObjectReference:
                // 이미지(Sprite)와 소리(AudioClip) 칸은 그 종류만 고를 수 있게 하고, 그 외에는 Object로 받는다
                System.Type type = p.type.Contains("Sprite") ? typeof(Sprite)
                    : p.type.Contains("AudioClip") ? typeof(AudioClip)
                    : typeof(Object);
                p.objectReferenceValue = EditorGUILayout.ObjectField(p.objectReferenceValue, type, false, w);
                break;
            default:
                EditorGUILayout.PropertyField(p, GUIContent.none, w);
                break;
        }
    }

    // 칸 위치(Rect)를 직접 지정해서 그린다. 줄에서 칸마다 정해진 자리에 놓이므로, 칸 사이 여백이 쌓여
    // 뒤로 갈수록 머리글과 어긋나는 일이 없다. (표는 이 방식을 쓴다)
    public static void Draw(Rect rect, SerializedProperty p)
    {
        if (p == null)
        {
            GUI.Label(rect, "?");
            return;
        }

        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer:
                p.intValue = EditorGUI.IntField(rect, p.intValue);
                break;
            case SerializedPropertyType.Float:
                p.floatValue = EditorGUI.FloatField(rect, p.floatValue);
                break;
            case SerializedPropertyType.String:
                p.stringValue = EditorGUI.TextField(rect, p.stringValue);
                break;
            case SerializedPropertyType.Boolean:
                p.boolValue = EditorGUI.Toggle(rect, p.boolValue);
                break;
            case SerializedPropertyType.Enum:
                p.enumValueIndex = EditorGUI.Popup(rect, p.enumValueIndex, p.enumDisplayNames);
                break;
            case SerializedPropertyType.ObjectReference:
                System.Type type = p.type.Contains("Sprite") ? typeof(Sprite)
                    : p.type.Contains("AudioClip") ? typeof(AudioClip)
                    : typeof(Object);
                p.objectReferenceValue = EditorGUI.ObjectField(rect, p.objectReferenceValue, type, false);
                break;
            default:
                EditorGUI.PropertyField(rect, p, GUIContent.none);
                break;
        }
    }
}
