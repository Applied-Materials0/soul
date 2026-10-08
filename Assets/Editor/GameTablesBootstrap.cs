using System.IO;
using UnityEditor;
using UnityEngine;

// 규칙 표 에셋(SP 소모, 레벨, 숙련도, 기본 능력치, 효과음)이 없으면 에디터가 열릴 때 기본값으로 자동 생성한다.
// 에셋은 Assets/Resources/Tables/ 에 만들어지고, 게임은 이름으로 찾기 때문에 씬에 연결할 필요가 없다.
[InitializeOnLoad]
public static class GameTablesBootstrap
{
    private const string Folder = "Assets/Resources/Tables";

    static GameTablesBootstrap()
    {
        EditorApplication.delayCall += EnsureAll;
    }

    [MenuItem("Soul/규칙 표 에셋 만들기 (없는 것만)")]
    public static void EnsureAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Ensure<SPCostTable>("SPCostTable");
        Ensure<LevelTable>("LevelTable");
        Ensure<ProficiencyTable>("ProficiencyTable");
        Ensure<PlayerBaseTable>("PlayerBaseTable");
        Ensure<SearchTable>("SearchTable");

        // 효과음 표는 새로 만들 때 기본 소리 파일(war2, metal)을 이름으로 찾아 채워 준다
        SoundTable sounds = Ensure<SoundTable>("SoundTable", out bool created);
        if (created) FillDefaultSounds(sounds);
    }

    private static T Ensure<T>(string assetName) where T : ScriptableObject
    {
        return Ensure<T>(assetName, out bool created);
    }

    private static T Ensure<T>(string assetName, out bool created) where T : ScriptableObject
    {
        created = false;
        string path = $"{Folder}/{assetName}.asset";
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Tables");

        // 새 에셋은 클래스의 기본값(필드 초기값)으로 채워져서 만들어진다
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[GameTablesBootstrap] {path} 를 기본값으로 만들었습니다.");
        created = true;
        return asset;
    }

    // 효과음 표의 기본 소리 파일: 공격/공격받음 = war2, 방어 = metal (프로젝트에서 파일 이름으로 찾음)
    private static void FillDefaultSounds(SoundTable table)
    {
        if (table == null) return;

        foreach (SoundEntry e in table.entries)
        {
            if (e == null || e.clip != null) continue;

            string clipName = null;
            if (e.sound == SoundEvent.PlayerAttack || e.sound == SoundEvent.EnemyAttack) clipName = "war2";
            else if (e.sound == SoundEvent.Defend) clipName = "metal";
            if (clipName == null) continue;

            e.clip = FindClip(clipName);
        }

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
    }

    private static AudioClip FindClip(string clipName)
    {
        foreach (string guid in AssetDatabase.FindAssets(clipName + " t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == clipName)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }
}
