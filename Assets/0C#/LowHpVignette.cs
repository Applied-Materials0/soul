using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 체력이 낮을수록 화면 가장자리가 붉어지는 경고 효과.
// 단계(몇 % 이하에서 얼마나 붉어지는지)는 기본 능력치 표의 [위험 경고]에서 정한다.
// 게임이 시작되면 스스로 만들어지며 장면이 바뀌어도 유지된다 (인스펙터 작업 없음).
public class LowHpVignette : MonoBehaviour
{
    private Image image;
    private float intensity; // 부드럽게 따라가는 현재 세기 (0~1)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<LowHpVignette>() != null) return;

        GameObject canvasGo = new GameObject("LowHpVignette Canvas", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        GameObject go = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasGo.transform, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.sprite = MakeSprite();
        img.color = new Color(0.85f, 0f, 0f, 0f);
        img.raycastTarget = false;

        LowHpVignette v = go.AddComponent<LowHpVignette>();
        v.image = img;
    }

    // 가운데는 투명하고 가장자리로 갈수록 불투명해지는 작은 그림 (화면 크기로 늘려서 씀)
    private static Sprite MakeSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)) * 0.6f + Mathf.Sqrt(nx * nx + ny * ny) * 0.4f; // 0(가운데) ~ 약 1.2(모서리)
                float a = Mathf.Clamp01((d - 0.5f) / 0.7f);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    void Update()
    {
        float target = 0f;
        if (GameManager.Once && SceneManager.GetActiveScene().name != "TitleScene")
            target = TargetIntensity();

        intensity = Mathf.MoveTowards(intensity, target, Time.unscaledDeltaTime * 1.5f);

        // 아주 위독하면(마지막 단계) 숨 쉬듯 조금씩 깜빡임
        float pulse = target > 0.6f ? 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
        Color c = image.color;
        c.a = Mathf.Clamp01(intensity * pulse);
        image.color = c;
    }

    // 현재 체력 비율에 따른 세기(0~1). 단계 사이는 부드럽게 이어진다.
    private static float TargetIntensity()
    {
        int maxHp = BattleCalc.PlayerMaxHp();
        if (maxHp <= 0) return 0f;
        float ratio = Mathf.Clamp01(GameManager.Hp / (float)maxHp) * 100f; // 현재 체력 [%]

        PlayerBaseTable b = GameTables.PlayerBase;
        float p1 = b.lowHpPercent1, p2 = b.lowHpPercent2, p3 = b.lowHpPercent3;
        float a1 = b.lowHpAlpha1 / 100f, a2 = b.lowHpAlpha2 / 100f, a3 = b.lowHpAlpha3 / 100f;
        float aMax = Mathf.Max(a3, Mathf.Lerp(a3, 1f, 0.3f)); // 체력 0%에서의 세기

        if (ratio > p1 + 5f) return 0f;
        if (ratio > p1) return Mathf.Lerp(0f, a1, (p1 + 5f - ratio) / 5f); // 첫 단계 직전에 살짝 번져 들어옴
        if (ratio > p2) return Mathf.Lerp(a1, a2, Mathf.InverseLerp(p1, p2, ratio));
        if (ratio > p3) return Mathf.Lerp(a2, a3, Mathf.InverseLerp(p2, p3, ratio));
        return Mathf.Lerp(a3, aMax, Mathf.InverseLerp(p3, 0f, ratio));
    }
}
