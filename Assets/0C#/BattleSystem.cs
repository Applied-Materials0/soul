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
        lineShownAt = Time.time; // FieldSearch가 방금 띄운 "OO이(가) 나타났다!"가 읽힐 시간을 센다

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
        StopNarration(); // 몬스터 턴의 남은 문구는 건너뛰고, 내 행동의 문구를 바로 보여 줌
        StartCoroutine(AttackRoutine());
    }

    public void OnDefend()
    {
        if (!active || busy) return;
        StopNarration(); // 몬스터 턴의 남은 문구는 건너뛰고, 내 행동의 문구를 바로 보여 줌
        StartCoroutine(DefendRoutine());
    }

    public void OnRun()
    {
        if (!active || busy) return;
        StopNarration(); // 몬스터 턴의 남은 문구는 건너뛰고, 내 행동의 문구를 바로 보여 줌
        StartCoroutine(RunRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        busy = true;

        if (!PaySP(SPAction.Attack))
        {
            yield return Say("SP가 부족해 움직일 수 없다!");
            yield return MonsterTurn();
            busy = false;
            yield break;
        }

        PlaySound(SoundEvent.PlayerAttack); // 공격 효과음 (효과음 표)

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

        if (!PaySP(SPAction.Defend))
        {
            yield return Say("SP가 부족해 움직일 수 없다!");
            yield return MonsterTurn();
            busy = false;
            yield break;
        }
        playerDefending = true;
        PlaySound(SoundEvent.Defend); // 방어 효과음 (효과음 표)
        yield return Say("방어 태세를 취했다.");
        yield return MonsterTurn();
        busy = false;
    }

    private IEnumerator RunRoutine()
    {
        busy = true;

        if (!PaySP(SPAction.Run))
        {
            yield return Say("SP가 부족해 도망칠 수 없다!");
            yield return MonsterTurn();
            busy = false;
            yield break;
        }

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
    // 몬스터의 행동은 두 부분으로 나뉜다.
    //  1) 판정: 공격 알림 문구가 뜨는 바로 그 순간에 끝난다 (체력 감소, 효과음, 맞았다면 화면이 붉어짐).
    //  2) 문구: 판정 결과를 알려 주는 문구들이 시간 간격을 두고 이어서 나온다 (백그라운드에서 진행).
    // 판정이 끝나면 곧바로 플레이어가 행동할 수 있고, 그 사이에 행동하면 남은 문구는 건너뛴다.
    // 아무것도 하지 않으면 문구가 끝까지 나오고, 마지막에 "무엇을 할까?"가 붙는다.
    //
    // 공격 문구 흐름:
    //  "{몬스터}의 공격!"(선공이면 "선제공격!") -> 맞았으면 바로 화면이 살짝 붉어짐 -> 잠시 뒤 결과
    //    - 회피: "피했다!"
    //    - 방어 중이고 피해가 0: "완벽하게 방어했다!"
    //    - 방어 중이고 일부만 막음: "방어에 성공했다!" -> "N의 데미지를 받았다!"
    //    - 피해 0: "효과가 없는 것 같다..."
    //    - 그 외: "N의 데미지를 받았다!"
    // 방어를 고르면: "{몬스터}의 방어!" (결과는 플레이어가 다음에 공격할 때 "방어에 성공했다!" 또는 "방어가 실패했다..."로 나옴)

    // 문구 한 줄: delayBefore초 기다린 뒤에 보여 준다
    private struct Step
    {
        public string text;
        public float delayBefore;
        public Step(string text, float delayBefore) { this.text = text; this.delayBefore = delayBefore; }
    }

    private Coroutine narration; // 몬스터 턴의 문구를 이어서 보여 주는 코루틴

    // 남은 문구를 건너뛰고, 다음 문구는 기다리지 않고 바로 띄우게 한다 (플레이어가 행동할 때 부름)
    private void StopNarration()
    {
        if (narration != null)
        {
            StopCoroutine(narration);
            narration = null;
        }
        lineShownAt = float.NegativeInfinity;
    }

    private IEnumerator Narrate(List<Step> steps)
    {
        foreach (Step step in steps)
        {
            if (step.delayBefore > 0f) yield return new WaitForSeconds(step.delayBefore);
            field.SearchText.SetText(step.text);
            lineShownAt = Time.time;
        }
        narration = null;
        EndTurn(); // 마지막 문구 아래에 "무엇을 할까?"
    }

    // 몬스터가 선공이면(전투 시작 때 Speed가 높아서 먼저 행동) preemptive가 true
    private IEnumerator MonsterTurn(bool preemptive = false)
    {
        // 이전 제압은 한 턴만 지속되므로 여기서 풀린다
        suppressed = false;

        string name = monster.monsterName;

        // 체력이 일정 비율 이하로 떨어지면 도망치는 몬스터 (예: 토끼). 도망치면 보상 없이 전투가 끝난다
        float fleeRatio = monsterHp / (float)Mathf.Max(1, monster.hpMax);
        if (monster.fleeHpPercent > 0f && fleeRatio * 100f <= monster.fleeHpPercent && BattleCalc.Roll(monster.fleeChance))
        {
            yield return Say($"{name}{Josa(name, "이", "가")} 도망쳤다!");
            field.RestoreExploreButtons();
            EndBattle();
            yield break;
        }

        List<Step> steps = new List<Step>(); // 판정 결과를 알려 주는 문구들

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

            // 1) 공격 알림과 동시에 판정을 적용한다: 효과음, 맞았다면 화면이 바로 붉어지고 체력이 줄어듦
            yield return Say(preemptive ? $"{name}의 선제공격!" : $"{name}의 공격!");
            PlaySound(SoundEvent.EnemyAttack);

            bool perfectBlock = defended && !h.dodged && h.noEffect;
            bool gotHit = !h.dodged && !perfectBlock; // 타격 여부: 회피하지도, 완전히 막지도 못함
            if (gotHit && flash != null) flash.Play(HitFlashColor);

            // 2) 결과 문구는 잠시 뒤에 나온다
            if (h.dodged)
            {
                steps.Add(new Step("피했다!", HitResultDelay));
            }
            else if (perfectBlock)
            {
                steps.Add(new Step("완벽하게 방어했다!", HitResultDelay));
            }
            else if (h.noEffect)
            {
                steps.Add(new Step("효과가 없는 것 같다...", HitResultDelay));
            }
            else
            {
                GameManager.Hp = Mathf.Max(0, GameManager.Hp - h.damage);
                RefreshPlayerUI();
                if (defended)
                {
                    steps.Add(new Step("방어에 성공했다!", HitResultDelay));
                    steps.Add(new Step($"{h.damage}의 데미지를 받았다!", LineDelay));
                }
                else
                {
                    steps.Add(new Step($"{h.damage}의 데미지를 받았다!", HitResultDelay));
                }
            }
        }
        playerDefending = false; // [방어]는 몬스터의 한 번의 행동에만 적용

        // 쓰러지는 장면은 건너뛸 수 없다: 결과 문구를 끝까지 보여 준 뒤 기절 처리
        if (GameManager.Hp <= 0)
        {
            foreach (Step step in steps)
            {
                if (step.delayBefore > 0f) yield return new WaitForSeconds(step.delayBefore);
                field.SearchText.SetText(step.text);
                lineShownAt = Time.time;
            }
            yield return FaintRoutine();
            yield break;
        }

        // 제압: 일정 확률로 플레이어가 다음 턴에 도망치지 못하게 한다 (판정은 지금, 알림 문구는 이어서)
        if (BattleCalc.Roll(monster.suppressChance))
        {
            suppressed = true;
            steps.Add(new Step($"{name}{Josa(name, "이", "가")} 당신을 제압했다! 다음 턴에는 도망칠 수 없다.", LineDelay));
        }

        // 판정은 끝났다. 문구는 백그라운드로 이어 보여 주고, 호출한 쪽은 바로 플레이어의 입력을 받는다
        narration = StartCoroutine(Narrate(steps));
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
        List<LevelSystem.LevelUp> levelUps = LevelSystem.AddExp(exp); // 레벨 표에 따라 레벨업

        // 아이템 드랍: 줄마다 확률을 따로 판정하고, 수량은 최소~최대 중에서 뽑는다
        List<string> drops = new List<string>();
        bool bagFull = false;
        if (InventoryManager.Instance != null)
        {
            foreach (MonsterDrop d in monster.drops)
            {
                if (d == null || !BattleCalc.Roll(d.chance)) continue;
                Item item = InventoryManager.Instance.GetItemData(d.itemId);
                if (item == null) continue;

                int amount = Random.Range(d.amountMin, Mathf.Max(d.amountMin, d.amountMax) + 1);

                // 가방의 슬롯/무게 한도 안에서 넣을 수 있는 만큼만 얻는다
                int got = InventoryManager.Instance.AddItem(item, amount);
                if (got < amount) bagFull = true;
                if (got > 0) drops.Add(got > 1 ? $"{item.itemName} x{got}" : item.itemName);
            }
        }
        RefreshPlayerUI();

        string reward = $"골드 +{gold}, 경험치 +{exp}";
        if (drops.Count > 0) reward += "\n획득: " + string.Join(", ", drops);
        if (bagFull) reward += "\n" + InventoryManager.BlockMessage(InventoryManager.Instance.LastAddBlock);
        foreach (LevelSystem.LevelUp up in levelUps)
        {
            reward += $"\n레벨이 올랐다! Lv.{up.newLevel}";
            string gains = "";
            if (up.hpMax != 0) gains += $" 체력 +{up.hpMax}";
            if (up.at != 0) gains += $" 공격력 +{up.at}";
            if (up.df != 0) gains += $" 방어력 +{up.df}";
            if (up.spMax != 0) gains += $" SP +{up.spMax}";
            if (up.speed != 0) gains += $" 속도 +{up.speed}";
            if (gains.Length > 0) reward += $" ({gains.Trim()})";
        }
        yield return Say(reward); // 직전 문구("쓰러뜨렸다!")가 충분히 보인 뒤에 보상을 보여 줌

        field.RestoreExploreButtons();
        field.StartCarving(monster); // 칼로 도려낼 수 있는 몬스터면 [도려내기] 버튼을 켠다
        EndBattle();
        busy = false;
    }

    // 플레이어 체력이 0이 되었을 때
    // (임시 처리: 체력을 가득 채워 루프 타운으로 돌려보낸다. 기절 규칙을 정하면 여기만 고치면 된다.)
    private IEnumerator FaintRoutine()
    {
        yield return Say("눈앞이 캄캄해졌다...");
        yield return new WaitForSeconds(LineDelay); // 문구를 읽을 시간

        GameManager.Hp = BattleCalc.PlayerMaxHp();
        GameManager.isfaint = false;

        // 기절하면 병원비를 내고 아이템 일부를 잃는다 (기본 능력치 표의 [기절] 설정)
        string penalty = InventoryManager.Instance != null ? InventoryManager.Instance.ApplyFaintPenalty() : "";
        RefreshPlayerUI();
        ItemGainToast.ShowMessage(string.IsNullOrEmpty(penalty) ? "병원에서 치료를 받았다." : "병원에서 치료를 받았다. " + penalty + ".");

        EndBattle();
        busy = false;
        field.ReturnToTown();
    }

    // 가방이나 맵이 열려 있는 동안에는 몬스터 이름/체력 게이지를 숨기고, 닫으면 다시 보여 준다
    private void Update()
    {
        if (!active || hud == null) return;

        bool uiOpen = (InventoryManager.Instance != null && InventoryManager.Instance.IsInventoryOpen)
            || (MapManager.Instance != null && MapManager.Instance.IsMapOpen);
        hud.SetVisible(!uiOpen);
    }

    private void EndBattle()
    {
        StopNarration(); // 이어지던 문구가 있으면 멈춤
        active = false;
        monster = null;
        if (hud != null) hud.Hide();
    }

    // =========================================================
    //  도구
    // =========================================================
    private float lineShownAt; // 마지막 문구를 띄운 시각

    // 문구를 보여 준다. 직전 문구가 LineDelay 동안은 보이도록 필요하면 그만큼 기다린 뒤 띄우고,
    // 띄운 뒤에는 기다리지 않는다. 그래서 한 턴의 마지막 문구는 바로 보이고 곧바로 다음 행동을 고를 수 있다.
    private IEnumerator Say(string text)
    {
        float remain = lineShownAt + LineDelay - Time.time;
        if (remain > 0f) yield return new WaitForSeconds(remain);
        field.SearchText.SetText(text);
        lineShownAt = Time.time;
    }

    // 한 턴이 끝났을 때: 마지막 문구 아래에 "무엇을 할까?"를 덧붙인다 (입력은 이미 가능한 상태)
    private void EndTurn()
    {
        if (!active) return;
        field.SearchText.SetText(field.SearchText.text + "\n무엇을 할까?");
    }

    // 효과음 표(SoundTable)의 소리를 재생
    private void PlaySound(SoundEvent sound)
    {
        GameTables.Sounds.Play(sound, field.BtnAudio);
    }

    // 행동에 필요한 SP를 낸다 (SP 소모 표). 모자라면 false: 호출한 쪽이 "움직일 수 없다"로 턴을 넘긴다
    private bool PaySP(SPAction action)
    {
        int cost = GameTables.SPCosts.Get(action);
        if (GameManager.SP < cost) return false;
        GameManager.SP -= cost;
        RefreshPlayerUI();
        return true;
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
