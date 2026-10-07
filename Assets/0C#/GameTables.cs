using UnityEngine;

// 규칙 표(SP 소모, 레벨, 숙련도)를 불러오는 곳.
// 표 에셋은 Assets/Resources/Tables/ 에 있고, 씬에 연결하지 않아도 코드가 이름으로 찾는다.
// 에셋이 없으면(처음 한 번) 에디터가 자동으로 만들어 주고, 그 전에는 기본값으로 동작한다.
public static class GameTables
{
    private static SPCostTable spCosts;
    private static LevelTable levels;
    private static ProficiencyTable proficiency;
    private static PlayerBaseTable playerBase;
    private static SoundTable sounds;

    public static SPCostTable SPCosts { get { return Load(ref spCosts, "SPCostTable"); } }
    public static LevelTable Levels { get { return Load(ref levels, "LevelTable"); } }
    public static ProficiencyTable Proficiency { get { return Load(ref proficiency, "ProficiencyTable"); } }
    public static PlayerBaseTable PlayerBase { get { return Load(ref playerBase, "PlayerBaseTable"); } }
    public static SoundTable Sounds { get { return Load(ref sounds, "SoundTable"); } }

    private static T Load<T>(ref T cache, string assetName) where T : ScriptableObject
    {
        if (cache != null) return cache;

        cache = Resources.Load<T>("Tables/" + assetName);
        if (cache == null)
        {
            // 기본값을 가진 임시 표로 대체 (필드 초기값이 기본 표)
            cache = ScriptableObject.CreateInstance<T>();
            Debug.LogWarning($"[GameTables] Resources/Tables/{assetName} 에셋이 없어 기본값을 사용합니다. 에디터를 다시 열면 자동으로 만들어집니다.");
        }
        return cache;
    }
}
