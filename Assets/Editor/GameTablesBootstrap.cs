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
        Ensure<TraitTable>("TraitTable");
        Ensure<GradeTable>("GradeTable");
        EnsureProficiencies();
        MigrateTraitDefaults();

        // 효과음 표는 새로 만들 때 기본 소리 파일(war2, metal)을 이름으로 찾아 채워 준다
        SoundTable sounds = Ensure<SoundTable>("SoundTable", out bool created);
        if (created) FillDefaultSounds(sounds);
    }

    // 숙련도 표 정리: 옛 [채집] 숙련도를 이름에 맞는 종류로 옮기고, 빠진 기본 종류(전투, 벌목, 채광, 채석, 풀 베기, 제작, 회복)를 추가한다
    private static void EnsureProficiencies()
    {
        ProficiencyTable table = AssetDatabase.LoadAssetAtPath<ProficiencyTable>(Folder + "/ProficiencyTable.asset");
        if (table == null) return;
        bool changed = false;

        // 예전에 [Gather]로 만든 숙련도는 이름이 같은 종류로 바꾼다 (진행도는 번호로 구분되므로 그대로 유지됨)
        foreach (ProficiencyDef d in table.defs)
        {
            if (d == null || d.kind != ProficiencyKind.Gather || string.IsNullOrEmpty(d.label)) continue;
            string label = d.label.Replace(" ", "");
            ProficiencyKind? target = null;
            if (label == "벌목") target = ProficiencyKind.Logging;
            else if (label == "채광") target = ProficiencyKind.Mining;
            else if (label == "채석") target = ProficiencyKind.Quarrying;
            else if (label == "풀베기") target = ProficiencyKind.Mowing;
            else if (label == "제작") target = ProficiencyKind.Crafting;
            if (target.HasValue && !HasKind(table, target.Value)) { d.kind = target.Value; changed = true; }
        }

        // "레벨 경험치/회" 항목이 생기기 전의 표라면 전부 0이므로 기본값 1로 채운다
        bool allZero = true;
        foreach (ProficiencyDef d in table.defs) if (d != null && d.playerExpPerUse != 0) allZero = false;
        if (allZero)
        {
            foreach (ProficiencyDef d in table.defs) if (d != null) d.playerExpPerUse = 1;
            changed = true;
        }

        // 빠진 기본 종류 추가
        foreach (ProficiencyKind kind in ProficiencyTable.DefaultKinds)
        {
            if (HasKind(table, kind)) continue;
            ProficiencyDef def = ProficiencyTable.MakeDefault(kind);
            while (IdInUse(table, def.id)) def.id++; // 번호가 겹치면 다음 번호
            table.defs.Add(def);
            changed = true;
            Debug.Log($"[GameTablesBootstrap] 숙련도 표에 [{def.label}] 숙련도를 추가했습니다.");
        }

        if (changed) { EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
    }

    private static bool HasKind(ProficiencyTable table, ProficiencyKind kind)
    {
        foreach (ProficiencyDef d in table.defs) if (d != null && d.kind == kind) return true;
        return false;
    }

    private static bool IdInUse(ProficiencyTable table, int id)
    {
        foreach (ProficiencyDef d in table.defs) if (d != null && d.id == id) return true;
        return false;
    }

    // 지속 피해(상태이상) 항목이 생기기 전에 만든 독 특성에 기본값을 채운다 (아직 아무것도 안 적었을 때만)
    private static void MigrateTraitDefaults()
    {
        TraitTable table = AssetDatabase.LoadAssetAtPath<TraitTable>(Folder + "/TraitTable.asset");
        if (table == null) return;
        bool changed = false;
        foreach (TraitDef t in table.traits)
        {
            if (t == null || t.id != 1 || t.label != "독") continue;
            if (t.inflictChance == 0f && t.dotDamage == 0f && t.dotGrowth == 0f && t.dotTurns == 0)
            {
                t.inflictChance = 30f; t.dotDamage = 2f; t.dotGrowth = 1f; t.dotTurns = 5;
                changed = true;
            }
        }
        if (changed) { EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
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
