using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 전체가 잠깐 색깔로 번쩍였다가 사라지는 효과 (플레이어가 공격에 맞거나 위험한 것을 먹었을 때 살짝 붉어지는 용도).
// 클릭을 막지 않고(raycastTarget 꺼짐), 인스펙터 작업 없이 처음 필요할 때 Create()/Shared로 만든다.
// 인벤토리(가방) UI 위에도 보이도록 가장 위에 그리는 전용 캔버스를 쓴다.
public class ScreenFlash : MonoBehaviour
{
    private static ScreenFlash shared;
    private Image image;
    private Coroutine routine;

    // 어디서든 쓰는 하나뿐인 효과 (장면이 바뀌어도 유지됨)
    public static ScreenFlash Shared
    {
        get
        {
            if (shared == null) shared = Build();
            return shared;
        }
    }

    // 예전 호출 방식 호환: context는 쓰지 않는다
    public static ScreenFlash Create(Transform context)
    {
        return Shared;
    }

    private static ScreenFlash Build()
    {
        GameObject canvasGo = new GameObject("ScreenFlash Canvas", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        GameObject go = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasGo.transform, false);

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
