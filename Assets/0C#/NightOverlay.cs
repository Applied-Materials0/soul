using UnityEngine;
using UnityEngine.UI;

// 시간대와 날씨에 따라 화면을 검푸른 색으로 덮어 어둡게 한다. 이 스크립트가 붙은 Image 오브젝트의 색이 덮는 색이다.
//  - 밝음: 낮(오전/오후)이고 날씨가 맑음, 폭염, 한파 -> 덮지 않음
//  - 침침함: 날씨가 흐림, 비, 눈, 폭풍우, 뇌우 -> overcastAlpha만큼 덮음
//  - 어두움: 밤 -> 오브젝트 색의 알파(nightAlpha)만큼 덮음 (날씨와 상관없이)
// 색과 밤의 어두운 정도는 씬의 NightOverlay 오브젝트에서 Image 색(알파)으로, 흐린 날의 정도는 아래 [Overcast Alpha]에서 고친다.
[RequireComponent(typeof(Image))]
public class NightOverlay : MonoBehaviour
{
    public float fadeSeconds = 1.2f;   // 어두워지고 밝아지는 데 걸리는 시간
    [Range(0f, 1f)] public float overcastAlpha = 0.22f; // 흐리거나 비/눈/폭풍우/뇌우가 올 때 덮는 정도

    private Image image;
    private float nightAlpha;          // 밤일 때의 알파 (오브젝트에 설정해 둔 값)
    private float current;

    void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;   // 클릭을 막지 않는다
        nightAlpha = image.color.a;
        current = TargetAlpha();
        Apply();
    }

    void Update()
    {
        float target = TargetAlpha();
        float step = fadeSeconds > 0f ? Mathf.Max(nightAlpha, 0.01f) / fadeSeconds * Time.unscaledDeltaTime : 1f;
        current = Mathf.MoveTowards(current, target, step);
        Apply();
    }

    private float TargetAlpha()
    {
        if (GameTime.Period == DayPeriod.Night) return nightAlpha;
        switch (GameTime.Weather)
        {
            case WeatherType.Cloudy:
            case WeatherType.Rain:
            case WeatherType.Snow:
            case WeatherType.Storm:
            case WeatherType.Thunder:
                return Mathf.Min(overcastAlpha, nightAlpha);
            default:
                return 0f;
        }
    }

    private void Apply()
    {
        Color c = image.color;
        c.a = current;
        image.color = c;
        image.enabled = current > 0.001f;
    }
}
