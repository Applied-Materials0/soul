using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 필드에서 몬스터를 만났을 때의 턴제 전투 진행.
//  - 시작할 때 Speed가 높은 쪽이 먼저 행동하고, 그 뒤로는 플레이어와 몬스터가 번갈아 행동한다.
//  - 플레이어: [공격] / [방어] / [도망]. 몬스터: 공격 또는 방어를 상황에 따라 고르고, 일정 확률로 플레이어를 제압한다.
//  - 계산식은 BattleCalc, 몬스터 능력치/보상은 Monster 에셋에 있다.
// FieldSearch가 같은 오브젝트에 자동으로 붙인다.
public class BattleSystem : MonoBehaviour
{
    private const float LineDelay = 0.9f;       // 문구 한 줄이 보이는 시간
    private const float HitResultDelay = 1.0f;  // "OO의 공격!" 뒤에 결과(데미지/피했다 등)가 나오기까지의 시간
    private static readonly Color HitFlashColor = new Color(0.9f, 0.05f, 0.05f); // 맞았을 때 화면이 붉어지는 색

    private FieldSearch field;
    private MonsterHUD hud;
    private ScreenFlash flash;
    private Monster monster;
    private int monsterHp;

    private bool active;           // 전투 중인가
    private bool busy;             // 문구/턴을 진행 중이라 입력을 받지 않는 상태
    private bool playerDefending;  // 플레이어가 [방어] 중 (몬스터의 다음 공격에 적용)
    private bool monsterDefending; // 몬스터가 방어 중 (플레이어의 다음 공격에 적용)
    private bool suppressed;       // 제압당함: 이번 플레이어 턴에는 도망칠 수 없음

    public bool Active { get { return active; } }

    public void Init(FieldSearch f)
    {
        field = f;
    }

    // =========================================================
    //  전투 시작
    // =========================================================
    public void Begin(Monster m)
    {
        monster = m;
        monsterHp = m.hpMax;
        active = true;
        busy = true;
        playerDefending = false;
        monsterDefending = false;
        suppressed = false;

        if (hud == null) hud = MonsterHUD.Create(field.SearchText.transform, field.SearchText.font);
        hud.Show(m.monsterName, monsterHp, m.hpMax);
        if (flash == null) flash = ScreenFlash.Create(field.SearchText.transform);

        StartCoroutine(BeginRoutine());
    }

    private IEnumerator BeginRoutine()
    {
        // 만났다는 문구(FieldSearch가 띄움)를 잠깐 보여 준 뒤 시작
        yield return new WaitForSeconds(LineDelay);

        // Speed가 높은 쪽이 먼저 행동 (같으면 플레이어가 먼저)
        bool playerFirst = GameManager.Speed >= monster.speed;
        if (playerFirst)
        {
            field.SearchText.SetText("먼저 움직일 수 있다! 무엇을 할까?");
        }
        else
        {
            yield return MonsterTurn(true); // 몬스터의 선제 행동
        }
        busy = false;
    }

    // =========================================================
    //  플레이어 행동 (FieldSearch의 버튼에서 호출)
    // =========================================================
    public void OnAttack()
    {
        if (!active || busy) return;
        StartCoroutine(AttackRoutine());
    }

    public void OnDefend()
    {
        if (!active || busy) return;
        StartCoroutine(DefendRoutine());
    }

    public void OnRun()
    {
        if (!active || busy) return;
        StartCoroutine(RunRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        busy = true;

        bool targetDefending = monsterDefending; // 몬스터가 지난 턴에 방어를 골랐는가
        BattleCalc.AttackResult r = BattleCalc.PlayerAttack(monster, targetDefending);
        monsterDefending = false; // 몬스터의 방어는 이 공격 한 번에만 적용

        string name = monster.monsterName;
        if (r.dodged || r.noEffect)
        {
            // 피하거나 막혀서 피해가 안 들어감
            if (targetDefending)
                yield return Say($"{name}의 방어에 성공했다!");
            else if (r.dodged)
                yield return Say($"{name}{Josa(name, "이", "가")} 공격을 피했다!");
            else
                yield return Say("효과가 없는 것 같다...");
        }
        else
        {
            monsterHp = Mathf.Max(0, monsterHp - r.damage);
            hud.SetHp(monsterHp, monster.hpMax);

            // 몬스터가 방어했는데 피해가 들어갔으면 방어 실패
            string head = targetDefending ? $"{name}의 방어가 실패했다... " : "";
            string crit = r.crit ? "치명타! " : "";
            yield return Say($"{head}{crit}{name}에게 {r.damage}의 피해를 주었다!");

            if (r.heal > 0)
            {
                GameManager.Hp = Mathf.Min(GameManager.Hp + r.heal, BattleCalc.PlayerMaxHp());
                RefreshPlayerUI();
                yield return Say($"체력을 {r.heal} 흡수했다.");
            }
        }

        if (monsterHp <= 0)
        {
            yield return VictoryRoutine();
            yield break;
        }

        yield return MonsterTurn();
        busy = false;
    }

    // [방어]: 턴을 소모하고, 몬스터의 다음 공격을 회피율로 먼저 피해 보고 못 피하면 방어력을 높여 막는다
    private IEnumerator DefendRoutine()
    {
        busy = true;
        playerDefending = true;
        yield return Say("방어 태세를 취했다.");
        yield return MonsterTurn();
        busy = false;
    }

    private IEnumerator RunRoutine()
    {
        busy = true;

        if (suppressed)
        {
            // 제압당해서 도망에 실패하고, 이 턴을 소모한다
            yield return Say($"{monster.monsterName}에게 제압당해 도망칠 수 없다!");
            yield return MonsterTurn();
            busy = false;
            yield break;
        }

        field.Flee(); // 소리, 문구, 버튼 되돌리기
        EndBattle();
        busy = false;
    }

    // =========================================================
    //  몬스터 턴
    // =========================================================
    // 몬스터가 선공이면(전투 시작 때 Speed가 높아서 먼저 행동) preemptive가 true
    //
    // 공격 문구 흐름:
    //  "{몬스터}의 공격!"(선공이면 "선제공격!") -> 맞았으면 바로 화면이 살짝 붉어짐 -> 잠시 뒤 결과
    //    - 회피: "피했다!"
    //    - 방어 중이고 피해가 0: "완벽하게 방어했다!"
    //    - 방어 중이고 일부만 막음: "방어에 성공했다!" -> "N의 데미지를 받았다!"
    //    - 피해 0: "효과가 없는 것 같다..."
    //    - 그 외: "N의 데미지를 받았다!"
    // 방어를 고르면: "{몬스터}의 방어!" (결과는 플레이어가 다음에 공격할 때 "방어에 성공했다!" 또는 "방어가 실패했다..."로 나옴)
    private IEnumerator MonsterTurn(bool preemptive = false)
    {
        // 이전 제압은 한 턴만 지속되므로 여기서 풀린다
        suppressed = false;

        string name = monster.monsterName;

        // 행동 선택: 체력이 30% 이하이면 방어를 고를 확률이 높아진다
        float hpRatio = monsterHp / (float)Mathf.Max(1, monster.hpMax);
        float defendChance = hpRatio <= 0.3f ? monster.defendChanceLowHp : monster.defendChance;

        if (BattleCalc.Roll(defendChance))
        {
            monsterDefending = true;
            yield return Say($"{name}의 방어!");
        }
        else
        {
            bool defended = playerDefending;
            BattleCalc.HitResult h = BattleCalc.MonsterAttack(monster, defended);

            // 1) 공격 알림. 이 문구가 떠 있는 동안 맞았다면 화면이 바로 붉어진다
            field.SearchText.SetText(preemptive ? $"{name}의 선제공격!" : $"{name}의 공격!");

            bool perfectBlock = defended && !h.dodged && h.noEffect;
            bool gotHit = !h.dodged && !perfectBlock; // 타격 여부: 회피하지도, 완전히 막지도 못함
            if (gotHit && flash != null) flash.Play(HitFlashColor);

            // 2) 잠시 뒤 결과
            yield return new WaitForSeconds(HitResultDelay);

            if (h.dodged)
            {
                yield return Say("피했다!");
            }
            else if (perfectBlock)
            {
                yield return Say("완벽하게 방어했다!");
            }
            else if (h.noEffect)
            {
                yield return Say("효과가 없는 것 같다...");
            }
            else
            {
                GameManager.Hp = Mathf.Max(0, GameManager.Hp - h.damage);
                RefreshPlayerUI();
                if (defended) yield return Say("방어에 성공했다!");
                yield return Say($"{h.damage}의 데미지를 받았다!");
            }
        }
        playerDefending = false; // [방어]는 몬스터의 한 번의 행동에만 적용

        if (GameManager.Hp <= 0)
        {
            yield return FaintRoutine();
            yield break;
        }

        // 제압: 일정 확률로 플레이어가 다음 턴에 도망치지 못하게 한다
        if (BattleCalc.Roll(monster.suppressChance))
        {
            suppressed = true;
            yield return Say($"{monster.monsterName}{Josa(monster.monsterName, "이", "가")} 당신을 제압했다! 다음 턴에는 도망칠 수 없다.");
        }
    }

    // =========================================================
    //  승리 / 패배
    // =========================================================
    private IEnumerator VictoryRoutine()
    {
        yield return Say($"{monster.monsterName}{Josa(monster.monsterName, "을", "를")} 쓰러뜨렸다!");

        // 골드와 경험치는 각각 보너스(증감률)를 곱해서 받는다
        int gold = Mathf.RoundToInt(monster.gold * BattleCalc.Mult(GameManager.GoldR));
        int exp = Mathf.RoundToInt(monster.exp * BattleCalc.Mult(GameManager.ExpR));
        GameManager.Gold += gold;
        GameManager.Exp += exp;

        // 아이템 드랍: 줄마다 확률을 따로 판정하고, 수량은 최소~최대 중에서 뽑는다
        List<string> drops = new List<string>();
        if (InventoryManager.Instance != null)
        {
            foreach (MonsterDrop d in monster.drops)
            {
                if (d == null || !BattleCalc.Roll(d.chance)) continue;
                Item item = InventoryManager.Instance.GetItemData(d.itemId);
                if (item == null) continue;

                int amount = Random.Range(d.amountMin, Mathf.Max(d.amountMin, d.amountMax) + 1);
                InventoryManager.Instance.AddItem(item, amount);
                drops.Add(amount > 1 ? $"{item.itemName} x{amount}" : item.itemName);
            }
        }
        RefreshPlayerUI();

        string reward = $"골드 +{gold}, 경험치 +{exp}";
        if (drops.Count > 0) reward += "\n획득: " + string.Join(", ", drops);
        field.SearchText.SetText(reward);

        field.RestoreExploreButtons();
        EndBattle();
        busy = false;
    }

    // 플레이어 체력이 0이 되었을 때
    // (임시 처리: 체력을 가득 채워 루프 타운으로 돌려보낸다. 기절 규칙을 정하면 여기만 고치면 된다.)
    private IEnumerator FaintRoutine()
    {
        yield return Say("눈앞이 캄캄해졌다...");

        GameManager.Hp = BattleCalc.PlayerMaxHp();
        GameManager.isfaint = false;
        RefreshPlayerUI();

        EndBattle();
        busy = false;
        field.ReturnToTown();
    }

    private void EndBattle()
    {
        active = false;
        monster = null;
        if (hud != null) hud.Hide();
    }

    // =========================================================
    //  도구
    // =========================================================
    // 문구를 보여 주고 잠깐 기다린다 (한 줄씩 순서대로 읽히도록)
    private IEnumerator Say(string text)
    {
        field.SearchText.SetText(text);
        yield return new WaitForSeconds(LineDelay);
    }

    private static void RefreshPlayerUI()
    {
        if (PlayerUI.Instance != null) PlayerUI.Instance.UpdateStatText();
    }

    // 받침이 있으면 withBatchim(이/을), 없으면 without(가/를)
    private static string Josa(string word, string withBatchim, string without)
    {
        if (string.IsNullOrEmpty(word)) return without;
        char last = word[word.Length - 1];
        if (last >= 0xAC00 && last <= 0xD7A3)
            return (last - 0xAC00) % 28 != 0 ? withBatchim : without;
        return without;
    }
}
