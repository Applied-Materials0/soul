using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 제작 팝업: 조합법(필요 재료의 이미지와 설명), 수량 입력, [제작] / [취소] 버튼.
// 인스펙터에 UI를 따로 만들 필요 없이 ItemInfoPanel이 Create()로 생성하고, 코드가 UI를 만들어 붙인다.
public class CraftQuantityPopup : MonoBehaviour
{
    // 재료 한 줄(아이콘 + 이름/수량/설명)
    private class Row
    {
        public GameObject root;
        public TextMeshProUGUI text;
        public Ingredient ingredient; // 재료 줄이면 값이 있음
        public Item item;
        public RecipeTool tool;       // 필요 도구 줄이면 값이 있음
    }

    private TMP_FontAsset font;
    private Image resultIcon;
    private TextMeshProUGUI resultTitle;
    private TextMeshProUGUI resultDesc;
    private RectTransform rowContainer;
    private TMP_InputField qtyInput;
    private Button confirmButton;
    private TextMeshProUGUI noticeText; // 제작에 실패했을 때 이유를 보여 주는 줄 (가방이 가득 참 등)
    private readonly List<Row> rows = new List<Row>();

    private Item resultItem; // 만들 아이템
    private Recipe recipe;
    private Action onCrafted;
    private int maxCraftable; // 현재 보유 재료로 만들 수 있는 최대 횟수

    // =========================================================
    //  생성
    // =========================================================
    public static CraftQuantityPopup Create(Transform context, TMP_FontAsset font)
    {
        // 화면 전체를 덮어야 하므로 최상위 캔버스 밑에 만든다
        Canvas canvas = context.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : context;

        GameObject go = new GameObject("CraftPopup", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        CraftQuantityPopup popup = go.AddComponent<CraftQuantityPopup>();
        popup.Build(font);
        go.SetActive(false);
        return popup;
    }

    private void Build(TMP_FontAsset f)
    {
        font = f;

        // 화면 전체를 덮는 반투명 배경 (뒤쪽 UI 클릭 차단)
        RectTransform root = (RectTransform)transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // 가운데 창
        RectTransform window = NewRect("Window", transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 780f));
        window.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 1f);

        // 이후 요소는 창의 위쪽 가운데를 기준으로 배치 (pos.y는 위에서 아래로 내려온 거리)
        Vector2 top = new Vector2(0.5f, 1f);

        // 목표 아이템: 이름, 아이콘, 설명
        resultTitle = NewText(NewRect("Title", window, top, new Vector2(0f, -20f), new Vector2(760f, 60f)), 40, TextAlignmentOptions.Center);
        resultTitle.fontStyle = FontStyles.Bold;

        resultIcon = NewRect("ResultIcon", window, top, new Vector2(-310f, -90f), new Vector2(120f, 120f)).gameObject.AddComponent<Image>();
        resultIcon.preserveAspect = true;

        resultDesc = NewText(NewRect("ResultDesc", window, top, new Vector2(40f, -90f), new Vector2(540f, 120f)), 24, TextAlignmentOptions.TopLeft);

        // 필요 재료 목록
        NewText(NewRect("IngredientsLabel", window, top, new Vector2(0f, -230f), new Vector2(760f, 40f)), 28, TextAlignmentOptions.Left).text = "필요 재료";

        rowContainer = NewRect("Ingredients", window, top, new Vector2(0f, -275f), new Vector2(760f, 290f));
        VerticalLayoutGroup layout = rowContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 수량 입력: 라벨, [-], 입력창, [+], [최대]
        NewText(NewRect("QtyLabel", window, top, new Vector2(-300f, -585f), new Vector2(100f, 56f)), 28, TextAlignmentOptions.Center).text = "수량";
        CreateButton(window, "Minus", "-", font, new Color(0.3f, 0.3f, 0.36f), top, new Vector2(-215f, -585f), new Vector2(60f, 56f), OnClickMinus);
        BuildInputField(window, new Vector2(-105f, -585f), new Vector2(140f, 56f));
        CreateButton(window, "Plus", "+", font, new Color(0.3f, 0.3f, 0.36f), top, new Vector2(5f, -585f), new Vector2(60f, 56f), OnClickPlus);
        CreateButton(window, "Max", "최대", font, new Color(0.3f, 0.3f, 0.36f), top, new Vector2(110f, -585f), new Vector2(110f, 56f), OnClickMax);

        // 제작 / 취소
        confirmButton = CreateButton(window, "Confirm", "제작", font, new Color(0.2f, 0.55f, 0.3f), top, new Vector2(-130f, -650f), new Vector2(220f, 60f), OnClickConfirm).GetComponent<Button>();
        CreateButton(window, "Cancel", "취소", font, new Color(0.55f, 0.25f, 0.25f), top, new Vector2(130f, -650f), new Vector2(220f, 60f), OnClickCancel);

        // 실패 이유 (버튼 아래)
        noticeText = NewText(NewRect("Notice", window, top, new Vector2(0f, -718f), new Vector2(760f, 50f)), 26, TextAlignmentOptions.Center);
        noticeText.color = new Color(1f, 0.45f, 0.45f);
    }

    private void BuildInputField(Transform parent, Vector2 pos, Vector2 size)
    {
        RectTransform rt = NewRect("QtyInput", parent, new Vector2(0.5f, 1f), pos, size);
        rt.gameObject.SetActive(false); // 필드 연결을 끝낸 뒤에 켠다
        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.22f, 0.22f, 0.26f, 1f);

        RectTransform area = NewRect("Text Area", rt, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(8f, 4f);
        area.offsetMax = new Vector2(-8f, -4f);
        area.gameObject.AddComponent<RectMask2D>();

        RectTransform textRt = NewRect("Text", area, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        TextMeshProUGUI text = NewText(textRt, 30, TextAlignmentOptions.Center);

        qtyInput = rt.gameObject.AddComponent<TMP_InputField>();
        qtyInput.textViewport = area;
        qtyInput.textComponent = text;
        qtyInput.targetGraphic = bg;
        qtyInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        qtyInput.characterLimit = 4;
        qtyInput.onValueChanged.AddListener(_ => RefreshRows());                // 입력 중에는 재료 수량 미리보기만 갱신
        qtyInput.onEndEdit.AddListener(_ => SetCount(ParseCount()));            // 입력을 마치면 범위 안으로 보정
        rt.gameObject.SetActive(true);
    }

    // =========================================================
    //  열기 / 닫기
    // =========================================================
    // result: 만들 아이템, r: 그 아이템의 제작법
    public void Open(Item result, Recipe r, Action crafted)
    {
        if (result == null || r == null || InventoryManager.Instance == null) return;
        resultItem = result;
        recipe = r;
        onCrafted = crafted;

        // 목표 아이템 표시
        resultTitle.text = result.itemName;
        resultDesc.text = (!string.IsNullOrEmpty(result.description) ? result.description + "\n\n" : "")
            + $"1회 제작 시 {r.resultAmount}개 생산";
        resultIcon.sprite = result.icon;
        resultIcon.enabled = resultIcon.sprite != null;

        // 재료 줄 다시 만들기
        foreach (Row old in rows)
        {
            old.root.SetActive(false);
            Destroy(old.root);
        }
        rows.Clear();
        foreach (Ingredient ing in r.ingredients)
        {
            rows.Add(BuildRow(ing, null));
        }
        if (r.tools != null)
        {
            foreach (RecipeTool tool in r.tools)
            {
                if (tool != null) rows.Add(BuildRow(null, tool)); // 필요한 도구도 한 줄로 표시
            }
        }

        noticeText.text = "";
        qtyInput.SetTextWithoutNotify("1");
        RefreshRows();

        transform.SetAsLastSibling(); // 다른 UI보다 위에 표시
        gameObject.SetActive(true);
    }

    public void Close()
    {
        onCrafted = null;
        gameObject.SetActive(false);
    }

    // 재료 한 줄: [아이콘] 이름 / 필요 수량 (보유 수량) / 설명.  도구 줄이면(tool이 있으면) 아이콘 없이 도구 정보만
    private Row BuildRow(Ingredient ing, RecipeTool tool)
    {
        Item item = ing != null ? InventoryManager.Instance.GetItemData(ing.itemId) : null;

        GameObject rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(rowContainer, false);
        rowGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        rowGo.GetComponent<LayoutElement>().preferredHeight = 66f;

        HorizontalLayoutGroup h = rowGo.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.padding = new RectOffset(8, 8, 5, 5);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGo.transform.SetParent(rowGo.transform, false);
        Image icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.sprite = item != null ? item.icon : null;
        icon.enabled = icon.sprite != null;
        LayoutElement iconLayout = iconGo.GetComponent<LayoutElement>();
        iconLayout.preferredWidth = 56f;
        iconLayout.preferredHeight = 56f;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
        textGo.transform.SetParent(rowGo.transform, false);
        textGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
        TextMeshProUGUI text = NewText((RectTransform)textGo.transform, 22, TextAlignmentOptions.Left);
        text.overflowMode = TextOverflowModes.Ellipsis;

        return new Row { root = rowGo, text = text, ingredient = ing, item = item, tool = tool };
    }

    // =========================================================
    //  수량 / 표시 갱신
    // =========================================================
    private int ParseCount()
    {
        int value = int.TryParse(qtyInput.text, out int v) ? v : 1;
        return ClampCount(value);
    }

    private int ClampCount(int value)
    {
        return Mathf.Clamp(value, 1, Mathf.Max(1, maxCraftable));
    }

    private void SetCount(int value)
    {
        qtyInput.SetTextWithoutNotify(ClampCount(value).ToString());
        RefreshRows();
    }

    // 입력한 수량에 맞춰 필요 재료 수량을 다시 계산해 표시. 부족한 재료는 빨간색.
    private void RefreshRows()
    {
        if (recipe == null || InventoryManager.Instance == null) return;

        maxCraftable = InventoryManager.Instance.GetMaxCraftableAmount(recipe);
        int count = ParseCount();

        foreach (Row row in rows)
        {
            // 필요한 도구 줄: 제작할 때 내구도가 깎임 (도구는 사라지지 않음)
            if (row.tool != null)
            {
                int durabilityNeed = Mathf.Max(1, row.tool.durabilityCost) * count;
                int durabilityHave = InventoryManager.Instance.GetToolDurability(row.tool.toolType, row.tool.tier);
                string toolColor = durabilityHave >= durabilityNeed ? "#FFFFFF" : "#FF6666";
                string tierText = row.tool.tier > 0 ? $" (티어 {row.tool.tier} 이상)" : "";
                row.text.text = $"<color={toolColor}><b>필요 도구: {ToolTypeInfo.Name(row.tool.toolType)}</b>{tierText}  내구도 -{durabilityNeed} (남은 내구도 {durabilityHave})</color>\n"
                    + "<size=18><color=#AAAAAA>제작하면 내구도가 줄어듭니다</color></size>";
                continue;
            }

            int need = row.ingredient.amount * count;
            int have = InventoryManager.Instance.GetItemCount(row.ingredient.itemId);
            string color = have >= need ? "#FFFFFF" : "#FF6666";
            string ingName = row.item != null ? row.item.itemName : $"(ID {row.ingredient.itemId} 없음)";
            string desc = row.item != null && !string.IsNullOrEmpty(row.item.description)
                ? row.item.description.Replace("\n", " ") : "";

            row.text.text = $"<color={color}><b>{ingName}</b>  필요 {need}개 (보유 {have}개)</color>\n"
                + $"<size=18><color=#AAAAAA>{desc}</color></size>";
        }

        // 재료가 한 번도 제작할 만큼 없으면 [제작] 비활성화
        if (confirmButton != null) confirmButton.interactable = maxCraftable >= 1;
    }

    // =========================================================
    //  버튼
    // =========================================================
    private void OnClickMinus() { SoundManager.Instance?.PlaySlotClickSound(); SetCount(ParseCount() - 1); }
    private void OnClickPlus() { SoundManager.Instance?.PlaySlotClickSound(); SetCount(ParseCount() + 1); }
    private void OnClickMax() { SoundManager.Instance?.PlaySlotClickSound(); SetCount(maxCraftable); }

    // [제작]: 입력한 수량만큼 재료를 차감하고 목표 아이템을 만든다
    private void OnClickConfirm()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        int count = ParseCount();

        if (InventoryManager.Instance.TryCraftItem(resultItem, recipe, count))
        {
            Action done = onCrafted;
            Close();
            done?.Invoke(); // 정보창의 수량 표시 갱신
        }
        else
        {
            RefreshRows();
            noticeText.text = InventoryManager.Instance.LastCraftFailReason; // 가방이 가득 참 등
        }
    }

    // [취소]: 수량 입력 창만 닫는다
    private void OnClickCancel()
    {
        SoundManager.Instance?.PlaySlotClickSound();
        Close();
    }

    // =========================================================
    //  UI 생성 도구
    // =========================================================
    public static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    // rt 위에 TMP 텍스트를 붙인다 (폰트는 인스턴스의 font를 쓰기 위해 정적 도구 대신 아래 오버로드 사용)
    private TextMeshProUGUI NewText(RectTransform rt, float size, TextAlignmentOptions align)
    {
        return AddText(rt, size, align, font);
    }

    public static TextMeshProUGUI AddText(RectTransform rt, float size, TextAlignmentOptions align, TMP_FontAsset font)
    {
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    // 단색 버튼 생성 (ItemInfoPanel의 제작 버튼도 이것을 사용). anchor는 앵커이자 피벗.
    public static GameObject CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Color color,
        Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick)
    {
        RectTransform rt = NewRect(name, parent, anchor, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        RectTransform labelRt = NewRect("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        AddText(labelRt, 30, TextAlignmentOptions.Center, font).text = label;
        return rt.gameObject;
    }
}
