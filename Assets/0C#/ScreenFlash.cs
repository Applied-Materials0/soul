using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 전체가 잠깐 색깔로 번쩍였다가 사라지는 효과 (플레이어가 공격에 맞았을 때 살짝 붉어지는 용도).
// 클릭을 막지 않고(raycastTarget 꺼짐), 인스펙터 작업 없이 BattleSystem이 처음 필요할 때 Create()로 만든다.
public class ScreenFlash : MonoBehaviour
{
    private Image image;
    private Coroutine routine;

    public static ScreenFlash Create(Transform context)
    {
        Canvas canvas = context.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : context;

        GameObject go = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = false;

        ScreenFlash flash = go.AddComponent<ScreenFlash>();
        flash.image = img;
        return flash;
    }

    // 색깔이 즉시 peakAlpha만큼 보였다가 duration초에 걸쳐 투명해진다
    public void Play(Color color, float peakAlpha = 0.35f, float duration = 0.6f)
    {
        transform.SetAsLastSibling(); // 다른 UI 위에 덮어서 표시
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(color, peakAlpha, duration));
    }

    private IEnumerator Fade(Color color, float peakAlpha, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(peakAlpha, 0f, t / duration);
            image.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        image.color = Color.clear;
        routine = null;
    }
}
