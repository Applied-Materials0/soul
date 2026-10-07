using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource; // 효과음 전용 AudioSource 1개
    [SerializeField] private AudioClip slotClickClip; // 슬롯 클릭 소리

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // 외부에서 호출할 슬롯 클릭 사운드 재생 함수
    public void PlaySlotClickSound()
    {
        if (sfxSource != null && slotClickClip != null)
        {
            // PlayOneShot은 오브젝트가 비활성화되어도 중앙 AudioSource에서 소리가 끝까지 출력됨
            sfxSource.PlayOneShot(slotClickClip);
        }
    }
}