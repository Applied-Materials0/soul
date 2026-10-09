using UnityEngine;
using UnityEngine.UI;

// 밤이 되면 화면을 검푸른 색으로 어둡게 덮는다. 이 스크립트가 붙은 Image 오브젝트의 색(알파 포함)이 밤의 모습이다.
// 색과 어두운 정도는 씬의 NightOverlay 오브젝트에서 Image 색으로 직접 고친다 (알파가 클수록 어두움).
[RequireComponent(typeof(Image))]
public class NightOverlay : MonoBehaviour
{
    public float fadeSeconds = 1.2f;   // 어두워지고 밝아지는 데 걸리는 시간

    private Image image;
    private float nightAlpha;          // 밤일 때의 알파 (오브젝트에 설정해 둔 값)
    private float current;

    void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;   // 클릭을 막지 않는다
        nightAlpha = image.color.a;
        current = GameTime.Period == DayPeriod.Night ? nightAlpha : 0f;
        Apply();
    }

    void Update()
    {
        float target = GameTime.Period == DayPeriod.Night ? nightAlpha : 0f;
        float step = fadeSeconds > 0f ? nightAlpha / fadeSeconds * Time.unscaledDeltaTime : nightAlpha;
        current = Mathf.MoveTowards(current, target, step);
        Apply();
    }

    private void Apply()
    {
        Color c = image.color;
        c.a = current;
        image.color = c;
        image.enabled = current > 0.001f;
    }
}
