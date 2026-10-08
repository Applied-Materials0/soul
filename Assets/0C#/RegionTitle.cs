using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 지역에 도착하면 화면 가운데에 지역 이름을 크게 보여 줬다가 사라지게 한다.
// 반투명한 검은 띠가 화면을 가로로 가로지르고, 그 위에 흰 글씨로 "~ 포레스트 ~"가 나온다.
// 지도에서 입장 버튼을 누르면 Queue()로 이름을 맡겨 두고, 새 장면이 밝아질 때 ShowQueued()가 보여 준다.
public class RegionTitle : MonoBehaviour
{
    private const float FadeIn = 0.4f;
    private const float Hold = 1.6f;
    private const float FadeOut = 0.8f;
    private const float BandAlpha = 0.6f;   // 검은 띠의 투명도
    private const float BandHeight = 190f;

    private static RegionTitle instance;
    private static string queued;

    private CanvasGroup group;
    private TextMeshProUGUI label;
    private Coroutine routine;

    // 이동하면서 보여 줄 지역 이름을 맡겨 둔다
    public static void Queue(string regionName) { queued = regionName; }

    // 맡겨 둔 이름이 있으면 보여 준다
    public static void ShowQueued()
    {
        if (string.IsNullOrEmpty(queued)) return;
        string name = queued;
        queued = null;
        Show(name);
    }

    public static void Show(string regionName)
    {
        if (string.IsNullOrEmpty(regionName)) return;
        if (instance == null) instance = Create();
        instance.Play($"~ {regionName} ~");
    }

    private static RegionTitle Create()
    {
        GameObject canvasGo = new GameObject("RegionTitle Canvas", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;
        canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        // 화면 가운데를 가로로 가르는 반투명한 검은 띠
        RectTransform band = CraftQuantityPopup.NewRect("Band", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, BandHeight));
        band.anchorMin = new Vector2(0f, 0.5f);
        band.anchorMax = new Vector2(1f, 0.5f);
        band.offsetMin = new Vector2(0f, -BandHeight / 2f);
        band.offsetMax = new Vector2(0f, BandHeight / 2f);
        Image bg = band.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, BandAlpha);
        bg.raycastTarget = false;
        CanvasGroup g = band.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;

        // 흰색 큰 글씨
        RectTransform textRt = CraftQuantityPopup.NewRect("Text", band, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        TMP_FontAsset font = InventoryManager.Instance != null ? InventoryManager.Instance.UIFont : null;
        TextMeshProUGUI t = CraftQuantityPopup.AddText(textRt, 90, TextAlignmentOptions.Center, font);
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;

        RegionTitle title = band.gameObject.AddComponent<RegionTitle>();
        title.group = g;
        title.label = t;
        return title;
    }

    private void Play(string text)
    {
        label.text = text;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade());
    }

    private IEnumerator Fade()
    {
        float t = 0f;
        while (t < FadeIn) { t += Time.unscaledDeltaTime; group.alpha = Mathf.Clamp01(t / FadeIn); yield return null; }
        group.alpha = 1f;

        t = 0f;
        while (t < Hold) { t += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < FadeOut) { t += Time.unscaledDeltaTime; group.alpha = 1f - Mathf.Clamp01(t / FadeOut); yield return null; }
        group.alpha = 0f;
        routine = null;
    }
}
