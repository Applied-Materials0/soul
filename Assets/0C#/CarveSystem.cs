using System.Collections.Generic;
using UnityEngine;

// 몬스터를 쓰러뜨린 뒤 칼로 가죽과 고기를 도려내는 시스템.
//  - 쓰러뜨린 몬스터(Monster.carveCount > 0)의 사체가 생기고, [도려내기] 버튼(채집 버튼 자리)으로 정해진 횟수만큼 도려낼 수 있다.
//  - 한 번 도려낼 때마다 SP를 쓰고(SP 소모 표), 칼(ToolType.Knife)의 내구도가 1 줄어든다.
//  - 얻는 것은 Monster.carveDrops. 숙련도(도려내기)가 높을수록 얻는 수량이 늘고, 도려낼 때마다 숙련도 경험치가 오른다.
// FieldSearch가 같은 오브젝트에 자동으로 붙인다.
public class CarveSystem : MonoBehaviour
{
    private FieldSearch field;
    private Monster monster;
    private int remaining;
    private bool active;

    public bool Active { get { return active; } }

    public void Init(FieldSearch f)
    {
        field = f;
    }

    // 몬스터를 쓰러뜨렸을 때 호출. 도려낼 수 없는 몬스터면 아무 일도 없음
    public void Begin(Monster m)
    {
        if (m == null || m.carveCount <= 0)
        {
            active = false;
            return;
        }

        monster = m;
        remaining = m.carveCount;
        active = true;

        field.GatherText.SetText("도려내기");
        field.GatherBtn.SetActive(true);
        field.SearchText.SetText(field.SearchText.text + $"\n칼로 도려낼 수 있다. (남은 횟수 {remaining})");
    }

    public void End()
    {
        active = false;
        monster = null;
    }

    // [도려내기] 버튼 (채집 버튼이 눌렸을 때 FieldSearch가 호출)
    public void OnCarve()
    {
        if (!active || monster == null) return;

        // SP
        int cost = GameTables.SPCosts.Get(SPAction.Carve);
        if (GameManager.SP < cost)
        {
            field.BtnAudio.Play();
            field.SearchText.SetText("SP가 부족합니다!");
            return;
        }

        // 칼: 필요한 티어 이상의 칼 중 가장 낮은 티어, 같으면 앞쪽 슬롯의 칼을 쓴다 (채집 도구와 같은 규칙)
        if (InventoryManager.Instance == null) return;
        ToolCheckResult check;
        bool broken;
        Item knife = InventoryManager.Instance.ConsumeToolDurability(ToolType.Knife, monster.carveToolTier, out check, out broken);
        if (check == ToolCheckResult.NoTool)
        {
            field.BtnAudio.Play();
            field.SearchText.SetText("도려내려면 칼이 필요합니다!");
            return;
        }
        if (check == ToolCheckResult.LowTier)
        {
            field.BtnAudio.Play();
            field.SearchText.SetText($"{monster.carveToolTier}티어 이상의 칼이 필요합니다!");
            return;
        }

        GameManager.SP -= cost;
        field.GatherAudio.Play();

        // 얻는 것: 줄마다 확률 판정, 수량은 최소~최대 중에서 뽑고 숙련도 보너스만큼 늘림
        float bonus = Proficiency.Bonus(ProficiencyKind.Carving);
        List<string> gained = new List<string>();
        bool bagFull = false;
        foreach (MonsterDrop d in monster.carveDrops)
        {
            if (d == null || !BattleCalc.Roll(d.chance)) continue;
            Item item = InventoryManager.Instance.GetItemData(d.itemId);
            if (item == null) continue;

            int amount = Random.Range(d.amountMin, Mathf.Max(d.amountMin, d.amountMax) + 1);
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * BattleCalc.Mult(bonus)));

            // 가방의 슬롯/무게 한도 안에서 넣을 수 있는 만큼만 얻는다
            int got = InventoryManager.Instance.AddItem(item, amount);
            if (got < amount) bagFull = true;
            if (got > 0) gained.Add(got > 1 ? $"{item.itemName} x{got}" : item.itemName);
        }

        remaining--;

        string text = gained.Count > 0 ? $"{string.Join(", ", gained)}을(를) 도려냈다." : "아무것도 얻지 못했다...";
        if (bagFull) text += "\n" + InventoryManager.BlockMessage(InventoryManager.Instance.LastAddBlock);

        // 숙련도 경험치
        ProficiencyDef def = GameTables.Proficiency.Get(ProficiencyKind.Carving);
        if (def != null && Proficiency.AddExp(ProficiencyKind.Carving, def.expPerUse, out int newLevel))
            text += $"\n{def.label} 숙련도가 올랐다! Lv.{newLevel}";

        if (broken && knife != null) text += $"\n[{knife.itemName}]이(가) 파손되었습니다!";

        if (remaining <= 0)
        {
            text += "\n더 도려낼 곳이 없다.";
            field.GatherBtn.SetActive(false);
            End();
        }
        else
        {
            text += $" (남은 횟수 {remaining})";
        }

        field.SearchText.SetText(text);
        if (PlayerUI.Instance != null) PlayerUI.Instance.UpdateStatText();
    }
}
