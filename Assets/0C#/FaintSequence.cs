using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 기절 연출: 화면이 완전히 어두워지고 -> 마을로 이동한 뒤 -> 검은 화면에 "눈 앞이 깜깜해졌다..."가 뜨고
// -> 글자가 사라지면서 검은 화면도 옅어지며 걷힌다. 걷히기 시작할 때 병원 치료 문구를 화면에 보여 준다.
public class FaintSequence : MonoBehaviour
{
    private const float DarkenTime = 1.0f;   // 어두워지는 시간
    private const float TextInTime = 0.6f;   // 글자가 나타나는 시간
    private const float TextHoldTime = 1.6f; // 글자가 또렷하게 보이는 시간
    private const float ClearTime = 1.4f;    // 글자와 검은 화면이 함께 사라지는 시간

    private static FaintSequence instance;
    private CanvasGroup blackGroup;
    private CanvasGroup textGroup;
    private TextMeshProUGUI label;
    private bool playing;

    // darkText: 검은 화면에 뜨는 글자, afterMessage: 걷힌 뒤 보여 줄 문구(병원 치료/병원비 등)
    public static void Play(string darkText, string afterMessage)
    {
        if (instance == null) instance = Create();
        if (instance.playing) return;
        instance.StartCoroutine(instance.Run(darkText, afterMessage));
    }

    private static FaintSequence Create()
    {
        GameObject canvasGo = new GameObject("FaintSequence Canvas", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400; // 모든 UI 위
        canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        // 화면 전체를 덮는 검은색 (클릭도 막음)
        RectTransform black = CraftQuantityPopup.NewRect("Black", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        black.anchorMin = Vector2.zero;
        black.anchorMax = Vector2.one;
        black.offsetMin = Vector2.zero;
        black.offsetMax = Vector2.zero;
        black.gameObject.AddComponent<Image>().color = Color.black;
        CanvasGroup bg = black.gameObject.AddComponent<CanvasGroup>();
        bg.alpha = 0f;
        bg.blocksRaycasts = false;

        RectTransform textRt = CraftQuantityPopup.NewRect("Text", black, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 200f));
        TMP_FontAsset font = InventoryManager.Instance != null ? InventoryManager.Instance.UIFont : null;
        TextMeshProUGUI t = CraftQuantityPopup.AddText(textRt, 72, TextAlignmentOptions.Center, font);
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;
        CanvasGroup tg = textRt.gameObject.AddComponent<CanvasGroup>();
        tg.alpha = 0f;

        FaintSequence seq = canvasGo.AddComponent<FaintSequence>();
        seq.blackGroup = bg;
        seq.textGroup = tg;
        seq.label = t;
        return seq;
    }

    private IEnumerator Run(string darkText, string afterMessage)
    {
        playing = true;
        label.text = darkText;
        textGroup.alpha = 0f;
        blackGroup.blocksRaycasts = true;

        // 1) 화면이 완전히 어두워진다
        yield return Fade(blackGroup, 0f, 1f, DarkenTime);

        // 2) 어두운 동안 마을로 이동 (장면 전환)
        GameManager.selectedRegionID = 0;
        SceneManager.LoadScene("LoopTown");
        yield return null;
        yield return null;
        if (MapManager.Instance != null) MapManager.Instance.CloseMapQuiet();
        if (InventoryManager.Instance != null) InventoryManager.Instance.CloseInventoryQuiet();

        // 3) 검은 화면에 글자가 뜬다
        yield return Fade(textGroup, 0f, 1f, TextInTime);
        yield return new WaitForSecondsRealtime(TextHoldTime);

        // 4) 글자가 사라지면서 검은 화면도 옅어지며 걷힌다
        if (!string.IsNullOrEmpty(afterMessage)) ItemGainToast.ShowMessage(afterMessage);
        StartCoroutine(Fade(textGroup, 1f, 0f, ClearTime * 0.7f));
        yield return Fade(blackGroup, 1f, 0f, ClearTime);

        blackGroup.blocksRaycasts = false;
        playing = false;
    }

    private static IEnumerator Fade(CanvasGroup g, float from, float to, float time)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / time));
            yield return null;
        }
        g.alpha = to;
    }
}
