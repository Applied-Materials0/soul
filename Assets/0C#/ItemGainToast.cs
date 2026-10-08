using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아이템을 얻었을 때 화면 중앙에 아이템 이미지(와 이름, 수량)가 잠깐 보였다가 흐려지며 사라지는 표시.
// 여러 개를 한꺼번에 얻으면 가로로 나란히 보인다. InventoryManager.AddItem이 부르며, 인스펙터 작업은 필요 없다.
public class ItemGainToast : MonoBehaviour
{
    private const float FadeInTime = 0.15f;  // 나타나는 시간
    private const float HoldTime = 1.5f;     // 또렷하게 보이는 시간
    private const float FadeOutTime = 1.0f;  // 흐려지며 사라지는 시간
    private const int MaxShown = 6;          // 한 번에 보이는 최대 개수 (넘치면 오래된 것부터 지움)

    private static ItemGainToast instance;
    private RectTransform row;

    // 아이템 amount개를 얻었다고 화면 중앙에 표시한다
    public static void Show(Item item, int amount)
    {
        if (item == null || amount <= 0) return;
        if (instance == null) instance = Create();
        instance.Spawn(item, amount);
    }

    // 장면이 바뀌어도 남아 있는 전용 캔버스를 한 번만 만든다 (다른 UI 위에 그림)
    private static ItemGainToast Create()
    {
        GameObject canvasGo = new GameObject("ItemGainToast Canvas", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(canvasGo);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // 가방 UI(보통 100) 위에도 보이도록
        canvasGo.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        // 화면 중앙(조금 위)에 가로로 나란히
        RectTransform row = CraftQuantityPopup.NewRect("Row", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1400f, 220f));
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ItemGainToast toast = row.gameObject.AddComponent<ItemGainToast>();
        toast.row = row;
        return toast;
    }

    private void Spawn(Item item, int amount)
    {
        // 너무 많이 쌓이면 가장 오래된 것부터 지운다
        while (row.childCount >= MaxShown)
            DestroyImmediate(row.GetChild(0).gameObject);

        TMP_FontAsset font = InventoryManager.Instance != null ? InventoryManager.Instance.UIFont : null;

        // 한 칸: 위에 아이콘, 아래에 "이름 +수량"
        GameObject go = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(row, false);
        rt.sizeDelta = new Vector2(190f, 200f);
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false; // 클릭을 막지 않음

        Vector2 top = new Vector2(0.5f, 1f);
        if (item.icon != null)
        {
            Image icon = CraftQuantityPopup.NewRect("Icon", rt, top, Vector2.zero, new Vector2(140f, 140f)).gameObject.AddComponent<Image>();
            icon.sprite = item.icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        TextMeshProUGUI label = CraftQuantityPopup.AddText(
            CraftQuantityPopup.NewRect("Label", rt, top, new Vector2(0f, -146f), new Vector2(190f, 54f)),
            26, TextAlignmentOptions.Center, font);
        label.text = $"{item.itemName} +{amount}";
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);

        StartCoroutine(Fade(go, group, HoldTime));
    }

    // 지금 떠 있는 획득/알림 표시를 모두 즉시 지운다 (필드에서 얻은 표시가 지도/가방 화면에 남지 않도록)
    public static void ClearAll()
    {
        if (instance == null || instance.row == null) return;
        for (int i = instance.row.childCount - 1; i >= 0; i--)
            Destroy(instance.row.GetChild(i).gameObject);
    }

    // 장비/도구가 부서졌을 때: 아이템 이미지와 "~가 파괴되었다!" 문구를 화면 중앙에 띄운다
    public static void ShowBroken(Item item, string title)
    {
        if (item == null) return;
        if (instance == null) instance = Create();
        instance.SpawnBroken(item, title);
    }

    private void SpawnBroken(Item item, string title)
    {
        while (row.childCount >= MaxShown)
            DestroyImmediate(row.GetChild(0).gameObject);

        TMP_FontAsset font = InventoryManager.Instance != null ? InventoryManager.Instance.UIFont : null;

        GameObject go = new GameObject("ToastBroken", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(row, false);
        rt.sizeDelta = new Vector2(420f, 240f);
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        Vector2 top = new Vector2(0.5f, 1f);
        if (item.icon != null)
        {
            Image icon = CraftQuantityPopup.NewRect("Icon", rt, top, Vector2.zero, new Vector2(150f, 150f)).gameObject.AddComponent<Image>();
            icon.sprite = item.icon;
            icon.preserveAspect = true;
            icon.color = new Color(1f, 0.6f, 0.6f, 1f);
            icon.raycastTarget = false;
        }

        TextMeshProUGUI label = CraftQuantityPopup.AddText(
            CraftQuantityPopup.NewRect("Label", rt, top, new Vector2(0f, -154f), new Vector2(420f, 86f)),
            30, TextAlignmentOptions.Center, font);
        label.text = $"{Josa.WithIga(item.itemName)} 파괴되었습니다!"; // 예: 돌도끼가 파괴되었습니다!
        label.color = new Color(1f, 0.35f, 0.3f);
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = 0.3f;
        label.outlineColor = new Color32(0, 0, 0, 255);

        StartCoroutine(Fade(go, group, 2.2f));
    }

    private const float MessageHoldTime = 2.5f; // 글자 알림은 읽을 시간을 더 준다

    // 아이템 이미지 없이 글자만 화면 중앙에 잠깐 보여 준다 (아이템 사용 결과, 기절 페널티 안내 등)
    public static void ShowMessage(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (InventoryManager.Instance != null && InventoryManager.Instance.ShowBagMessage(text)) return; // 가방이 열려 있으면 가방 안 알림에 표시
        if (instance == null) instance = Create();
        instance.SpawnMessage(text);
    }

    private void SpawnMessage(string text)
    {
        while (row.childCount >= MaxShown)
            DestroyImmediate(row.GetChild(0).gameObject);

        TMP_FontAsset font = InventoryManager.Instance != null ? InventoryManager.Instance.UIFont : null;

        GameObject go = new GameObject("ToastMessage", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(row, false);
        rt.sizeDelta = new Vector2(1000f, 80f);
        CanvasGroup group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        TextMeshProUGUI label = CraftQuantityPopup.AddText(
            CraftQuantityPopup.NewRect("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 80f)),
            30, TextAlignmentOptions.Center, font);
        label.text = text;
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);

        StartCoroutine(Fade(go, group, MessageHoldTime));
    }

    // 나타남 -> 또렷하게 유지 -> 흐려지며 사라짐
    private IEnumerator Fade(GameObject go, CanvasGroup group, float holdTime)
    {
        float t = 0f;
        while (t < FadeInTime)
        {
            if (go == null) yield break;
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / FadeInTime);
            yield return null;
        }
        group.alpha = 1f;

        float hold = 0f;
        while (hold < holdTime)
        {
            if (go == null) yield break;
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < FadeOutTime)
        {
            if (go == null) yield break;
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / FadeOutTime);
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
