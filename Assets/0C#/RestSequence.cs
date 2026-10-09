using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 휴식 연출: 화면이 검게 변하고 -> 그 사이에 시간이 흐르며 회복하고 -> 검은 화면에 "휴식을 취했다."가 뜬 뒤
// -> 글자가 사라지면서 검은 화면도 옅어지며 걷힌다 (눈을 붙이는 느낌). 걷히기 시작할 때 회복 결과 문구를 보여 준다.
public class RestSequence : MonoBehaviour
{
    private const float DarkenTime = 0.8f;   // 어두워지는 시간
    private const float TextInTime = 0.5f;   // 글자가 나타나는 시간
    private const float TextHoldTime = 1.2f; // 글자가 또렷하게 보이는 시간
    private const float ClearTime = 1.2f;    // 글자와 검은 화면이 함께 사라지는 시간

    private static RestSequence instance;
    private CanvasGroup blackGroup;
    private CanvasGroup textGroup;
    private TextMeshProUGUI label;
    private bool playing;

    // 연출이 진행 중이면 true (그동안 또 휴식하지 못한다)
    public static bool Playing { get { return instance != null && instance.playing; } }

    // doRest: 화면이 완전히 어두워졌을 때 실행할 휴식 처리 (결과 문구를 돌려줌)
    public static void Play(Func<string> doRest)
    {
        if (instance == null) instance = Create();
        if (instance.playing) return;
        instance.StartCoroutine(instance.Run(doRest));
    }

    private static RestSequence Create()
    {
        GameObject canvasGo = new GameObject("RestSequence Canvas", typeof(Canvas), typeof(CanvasScaler));
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

        RestSequence seq = canvasGo.AddComponent<RestSequence>();
        seq.blackGroup = bg;
        seq.textGroup = tg;
        seq.label = t;
        return seq;
    }

    private IEnumerator Run(Func<string> doRest)
    {
        playing = true;
        label.text = "휴식을 취했다.";
        textGroup.alpha = 0f;
        blackGroup.blocksRaycasts = true;

        // 1) 화면이 완전히 어두워진다
        yield return Fade(blackGroup, 0f, 1f, DarkenTime);

        // 2) 어두운 동안 시간이 흐르고 회복한다
        string result = doRest != null ? doRest() : "";

        // 3) 검은 화면에 글자가 뜬다
        yield return Fade(textGroup, 0f, 1f, TextInTime);
        yield return new WaitForSecondsRealtime(TextHoldTime);

        // 4) 글자가 사라지면서 검은 화면도 옅어지며 걷힌다. 걷히기 시작할 때 결과 문구를 보여 준다
        if (!string.IsNullOrEmpty(result)) ItemGainToast.ShowMessage(result);
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
