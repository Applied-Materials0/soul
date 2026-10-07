using System.Collections.Generic;
using UnityEngine;

// 효과음 표. 행 하나 = 효과음 하나 (Soul > 데이터 표 > [효과음] 탭).
// 에셋은 Assets/Resources/Tables/SoundTable.asset. 소리 파일은 표의 칸에 끌어다 놓아 바꿀 수 있다.
public enum SoundEvent
{
    PlayerAttack,  // 플레이어가 공격할 때
    EnemyAttack,   // 몬스터가 공격해 올 때 (플레이어가 공격받을 때)
    Defend,        // 플레이어가 방어할 때
}

[System.Serializable]
public class SoundEntry
{
    public SoundEvent sound;
    public string label;                    // 표에 보이는 이름 (게임에는 영향 없음)
    public AudioClip clip;                  // 재생할 소리
    [Range(0f, 1f)] public float volume = 1f;
    public string note;                     // 메모
}

[CreateAssetMenu(fileName = "SoundTable", menuName = "Soul/Sound Table")]
public class SoundTable : ScriptableObject
{
    public List<SoundEntry> entries = CreateDefaultEntries();

    // 소리를 재생한다. 소리 파일이 비어 있거나 재생기가 없으면 아무 일도 없음
    public void Play(SoundEvent sound, AudioSource source)
    {
        if (source == null) return;
        foreach (SoundEntry e in entries)
        {
            if (e == null || e.sound != sound || e.clip == null) continue;
            source.PlayOneShot(e.clip, e.volume);
            return;
        }
    }

    private static SoundEntry E(SoundEvent s, string label, string note)
    {
        return new SoundEntry { sound = s, label = label, note = note };
    }

    // 소리 파일은 에디터가 이름으로 찾아 채워 준다 (GameTablesBootstrap)
    private static List<SoundEntry> CreateDefaultEntries()
    {
        return new List<SoundEntry>
        {
            E(SoundEvent.PlayerAttack, "공격", "플레이어가 공격할 때 (기본: war2)"),
            E(SoundEvent.EnemyAttack, "공격받음", "몬스터가 공격해 올 때 (기본: war2)"),
            E(SoundEvent.Defend, "방어", "플레이어가 방어할 때 (기본: metal)"),
        };
    }

    [ContextMenu("기본값으로 되돌리기")]
    public void ResetToDefaults()
    {
        entries = CreateDefaultEntries();
    }
}
