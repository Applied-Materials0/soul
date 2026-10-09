using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 제작 레시피 슬라이드 창: 오른쪽에서 밀려 들어오고, 제작 가능한 아이템(레시피가 있는 아이템) 목록을 보여 준다.
// 목록의 항목을 누르면 수량 입력 팝업(CraftQuantityPopup)이 열린다.
// 인스펙터 작업 없이 InventoryManager가 처음 열 때 Create()로 만든다.
public class CraftRecipePanel : MonoBehaviour
{
    private const float PanelWidth = 620f;
    private const float HiddenX = PanelWidth + 20f; // 화면 밖(오른쪽)
    private const float SlideTime = 0.25f;

    private TMP_FontAsset font;
    private RectTransform panel;   // 실제로 움직이는 창
    private RectTransform content; // 레시피 항목이 들어가는 목록
    private TextMeshProUGUI emptyText;
    private CraftQuantityPopup popup;
    private Coroutine slideRoutine;
    private ScrollRect scrollRect;
    private RectTransform viewportRt;

    // 키보드로 고르는 목록 항목
    private class Entry { public Item item; public Image bg; public bool canCraft; }
    private readonly List<Entry> entries = new List<Entry>();
    private int selIndex;

    public bool IsOpen { get; private set; }
    public bool PopupOpen { get { return popup != null && popup.gameObject.activeSelf; } } // 개별 제작 창이 떠 있는가

    // =========================================================
    //  생성
    // =========================================================
    public static CraftRecipePanel Create(Transform context, TMP_FontAsset font)
    {
        // 가방 UI보다 위에 그리기 위해 최상위 캔버스 밑에 만든다
        Canvas canvas = context.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.rootCanvas.transform : context;

        GameObject go = new GameObject("CraftRecipePanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        CraftRecipePanel p = go.AddComponent<CraftRecipePanel>();
        p.Build(font);
        go.SetActive(false);
        return p;
    }

    private void Build(TMP_FontAsset f)
    {
        font = f;

        // 전체 화면 크기의 투명한 틀 (클릭을 막지 않으므로 뒤의 가방을 계속 누를 수 있음)
        Stretch((RectTransform)transform, Vector2.zero, Vector2.zero);

        // 오른쪽에 붙는 창: 위쪽 버튼 줄(120px)과 아래쪽 여백(60px)을 비운 높이
        panel = CraftQuantityPopup.NewRect("Panel", transform, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, -180f);
        panel.anchoredPosition = new Vector2(HiddenX, -30f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.11f, 0.14f, 0.97f);

        Vector2 top = new Vector2(0.5f, 1f);
        TextMeshProUGUI title = CraftQuantityPopup.AddText(
            CraftQuantityPopup.NewRect("Title", panel, top, new Vector2(-40f, -15f), new Vector2(PanelWidth - 200f, 60f)),
            38, TextAlignmentOptions.Center, font);
        title.text = "제작 레시피";
        title.fontStyle = FontStyles.Bold;

        CraftQuantityPopup.CreateButton(panel, "Close", "닫기", font, new Color(0.55f, 0.25f, 0.25f),
            new Vector2(1f, 1f), new Vector2(-15f, -15f), new Vector2(110f, 56f), () => { PlayButtonSound(); Close(); });

        // 스크롤 목록
        RectTransform scroll = CraftQuantityPopup.NewRect("Scroll", panel, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(scroll, new Vector2(15f, 15f), new Vector2(-15f, -90f));

        RectTransform viewport = CraftQuantityPopup.NewRect("Viewport", scroll, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(viewport, Vector2.zero, Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // 드래그 스크롤용 (보이지 않음)

        content = CraftQuantityPopup.NewRect("Content", viewport, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect sr = scroll.gameObject.AddComponent<ScrollRect>();
        scrollRect = sr;
        viewportRt = viewport;
        sr.viewport = viewport;
        sr.content = content;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 40f;

        // 레시피가 하나도 없을 때 안내 문구
        RectTransform emptyRt = CraftQuantityPopup.NewRect("EmptyText", viewport, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(emptyRt, new Vector2(20f, 20f), new Vector2(-20f, -20f));
        emptyText = CraftQuantityPopup.AddText(emptyRt, 24, TextAlignmentOptions.Center, font);
        emptyText.text = "제작할 수 있는 아이템이 없습니다.\n(아이템 에셋의 Recipe에 재료를 설정하세요)";
        emptyText.color = new Color(1f, 1f, 1f, 0.6f);
    }

    private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    // =========================================================
    //  열기 / 닫기 (오른쪽에서 슬라이드)
    // =========================================================
    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    // 가방의 버튼음과 같은 소리 (제작 버튼으로 열 때 나는 소리와 맞춤)
    private static void PlayButtonSound()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv != null && inv.BtnAudio != null) inv.BtnAudio.Play();
        else SoundManager.Instance?.PlaySlotClickSound();
    }

    // ESC용: 가장 위의 한 겹만 닫고, 닫은 것이 있으면 true (수량 팝업 -> 레시피 창)
    public bool CloseTopLayer()
    {
        if (popup != null && popup.gameObject.activeSelf)
        {
            SoundManager.Instance?.PlaySlotClickSound();
            popup.Close();
            return true;
        }
        if (IsOpen)
        {
            PlayButtonSound();
            Close();
            return true;
        }
        return false;
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        // 완전히 숨겨진 상태에서 열 때만 화면 밖에서 시작 (닫히는 중이면 현재 위치에서 이어서)
        if (!gameObject.activeSelf)
            panel.anchoredPosition = new Vector2(HiddenX, panel.anchoredPosition.y);

        Rebuild();
        transform.SetAsLastSibling(); // 가방 UI보다 위에 표시
        gameObject.SetActive(true);
        SlideTo(0f, false);
    }

    // immediate가 true면 애니메이션 없이 즉시 닫음 (가방을 닫을 때)
    public void Close(bool immediate = false)
    {
        if (!IsOpen && !gameObject.activeSelf) return;
        IsOpen = false;

        if (popup != null) popup.Close();

        if (immediate || !gameObject.activeInHierarchy)
        {
            if (slideRoutine != null) StopCoroutine(slideRoutine);
            slideRoutine = null;
            panel.anchoredPosition = new Vector2(HiddenX, panel.anchoredPosition.y);
            gameObject.SetActive(false);
            return;
        }
        SlideTo(HiddenX, true);
    }

    private void SlideTo(float targetX, bool hideAfter)
    {
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(Slide(targetX, hideAfter));
    }

    private IEnumerator Slide(float targetX, bool hideAfter)
    {
        float startX = panel.anchoredPosition.x;
        float t = 0f;
        while (t < SlideTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / SlideTime);
            panel.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, k), panel.anchoredPosition.y);
            yield return null;
        }
        panel.anchoredPosition = new Vector2(targetX, panel.anchoredPosition.y);
        slideRoutine = null;
        if (hideAfter) gameObject.SetActive(false);
    }

    // =========================================================
    //  레시피 목록
    // =========================================================
    // 도감(ItemDatabase)에서 레시피가 있는 아이템을 id 순서로 모아 목록을 다시 만든다
    private void Rebuild()
    {
        entries.Clear();
        foreach (Transform child in content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        int shown = 0;
        InventoryManager inv = InventoryManager.Instance;
        if (inv != null && inv.itemDB != null)
        {
            List<Item> craftables = new List<Item>();
            foreach (Item item in inv.itemDB.items)
            {
                if (item != null && item.recipe != null && item.recipe.ingredients != null && item.recipe.ingredients.Count > 0)
                    craftables.Add(item);
            }
            craftables.Sort((a, b) => a.id.CompareTo(b.id));

            foreach (Item item in craftables)
            {
                BuildEntry(item);
                shown++;
            }
        }
        selIndex = Mathf.Clamp(selIndex, 0, Mathf.Max(0, entries.Count - 1));
        UpdateSelectionVisual();
        emptyText.gameObject.SetActive(shown == 0);
    }

    // 목록 한 줄: [아이콘] 이름 / 필요 재료(보유 수량). 누르면 수량 팝업이 열림
    private void BuildEntry(Item item)
    {
        Recipe recipe = item.recipe;
        bool canCraft = InventoryManager.Instance.GetMaxCraftableAmount(recipe) >= 1;

        GameObject go = new GameObject("Recipe_" + item.id,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(content, false);

        // 재료가 충분하면 초록빛으로 표시
        Image bg = go.GetComponent<Image>();
        bg.color = canCraft ? new Color(0.2f, 0.45f, 0.25f, 0.45f) : new Color(1f, 1f, 1f, 0.08f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        button.onClick.AddListener(() => OnClickRecipe(item));

        go.GetComponent<LayoutElement>().preferredHeight = 110f;
        entries.Add(new Entry { item = item, bg = bg, canCraft = canCraft });

        HorizontalLayoutGroup h = go.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 14f;
        h.padding = new RectOffset(10, 10, 8, 8);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGo.transform.SetParent(go.transform, false);
        Image icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.sprite = item.icon;
        icon.enabled = item.icon != null;
        icon.raycastTarget = false;
        LayoutElement iconLayout = iconGo.GetComponent<LayoutElement>();
        iconLayout.preferredWidth = 84f;
        iconLayout.preferredHeight = 84f;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(LayoutElement));
        textGo.transform.SetParent(go.transform, false);
        textGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
        TextMeshProUGUI text = CraftQuantityPopup.AddText((RectTransform)textGo.transform, 22, TextAlignmentOptions.Left, font);
        text.overflowMode = TextOverflowModes.Ellipsis;

        // 이름 (1회에 여러 개가 나오면 수량도 표시)
        string title = $"<size=28><b>{item.itemName}</b></size>";
        if (recipe.resultAmount > 1) title += $" x{recipe.resultAmount}";

        // 재료: 충분하면 흰색, 부족하면 빨간색
        List<string> parts = new List<string>();
        foreach (Ingredient ing in recipe.ingredients)
        {
            Item ingItem = InventoryManager.Instance.GetItemData(ing.itemId);
            string ingName = ingItem != null ? ingItem.itemName : $"(ID {ing.itemId} 없음)";
            int have = InventoryManager.Instance.GetItemCount(ing.itemId);
            string color = have >= ing.amount ? "#FFFFFF" : "#FF6666";
            parts.Add($"<color={color}>{ingName} {ing.amount}개 (보유 {have})</color>");
        }

        // 필요한 도구: 제작하면 내구도가 깎인다 (도구는 사라지지 않음)
        if (recipe.tools != null)
        {
            foreach (RecipeTool tool in recipe.tools)
            {
                if (tool == null) continue;
                int durability = InventoryManager.Instance.GetToolDurability(tool.toolType, tool.tier);
                string color = durability >= Mathf.Max(1, tool.durabilityCost) ? "#FFFFFF" : "#FF6666";
                string tier = tool.tier > 0 ? $" 티어{tool.tier}+" : "";
                parts.Add($"<color={color}>[{ToolTypeInfo.Name(tool.toolType)}{tier} 내구도 -{tool.durabilityCost}]</color>");
            }
        }
        // 필요한 SP: 모자라면 빨간색
        int spCost = InventoryManager.CraftSpCost(recipe);
        if (spCost > 0)
        {
            string spColor = GameManager.SP >= spCost ? "#FFFFFF" : "#FF6666";
            parts.Add($"<color={spColor}>[SP -{spCost}]</color>");
        }
        text.text = title + "\n" + string.Join("   ", parts);
    }

    // =========================================================
    //  키보드: W/S로 제작 항목 고르기, Space로 그 항목의 제작 창 열기 (제작 창 안의 키는 CraftQuantityPopup이 처리)
    // =========================================================
    void Update()
    {
        if (!IsOpen) return;
        if (popup != null && popup.gameObject.activeSelf) return;      // 제작 창이 열려 있으면 그쪽이 키를 쓴다
        if (CraftQuantityPopup.ClosedFrame == Time.frameCount) return; // 방금 제작 창을 닫은 Space는 무시

        int move = 0;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) move = -1;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) move = 1;
        if (move != 0 && entries.Count > 0)
        {
            selIndex = Mathf.Clamp(selIndex + move, 0, entries.Count - 1);
            UpdateSelectionVisual();
            ScrollToSelected();
            SoundManager.Instance?.PlaySlotClickSound();
        }

        if (Input.GetKeyDown(KeyCode.Space) && entries.Count > 0)
            OnClickRecipe(entries[selIndex].item);
    }

    // 고른 항목은 노란빛으로 보인다 (제작 가능한 항목은 초록빛)
    private void UpdateSelectionVisual()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.bg == null) continue;
            if (i == selIndex) e.bg.color = e.canCraft ? new Color(0.55f, 0.7f, 0.25f, 0.75f) : new Color(0.8f, 0.7f, 0.25f, 0.45f);
            else e.bg.color = e.canCraft ? new Color(0.2f, 0.45f, 0.25f, 0.45f) : new Color(1f, 1f, 1f, 0.08f);
        }
    }

    // 고른 항목이 목록 밖으로 벗어나면 그때만 한 칸씩 스크롤해서 보이게 한다 (보이는 안에서 움직일 때는 커서만 움직임)
    private const float EntryHeight = 110f, EntrySpacing = 8f;

    private void ScrollToSelected()
    {
        if (viewportRt == null || content == null || entries.Count == 0) return;

        float top = selIndex * (EntryHeight + EntrySpacing);
        float bottom = top + EntryHeight;
        float viewH = viewportRt.rect.height;
        Vector2 pos = content.anchoredPosition;
        float y = pos.y;

        if (top < y) y = top;                         // 위로 벗어남: 그 항목이 맨 위에 오도록
        else if (bottom > y + viewH) y = bottom - viewH; // 아래로 벗어남: 그 항목이 맨 아래에 오도록
        content.anchoredPosition = new Vector2(pos.x, Mathf.Max(0f, y));
    }

    private void OnClickRecipe(Item item)
    {
        SoundManager.Instance?.PlaySlotClickSound();
        if (popup == null) popup = CraftQuantityPopup.Create(transform, font);
        popup.Open(item, item.recipe, OnCrafted);
    }

    // 제작이 끝나면 재료 보유 수량이 바뀌었으므로 목록과 정보창 표시를 갱신
    private void OnCrafted()
    {
        Rebuild();
        ItemInfoPanel.Instance?.RefreshIfOpen();
    }
}
