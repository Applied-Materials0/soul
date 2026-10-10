using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 필드에서 몬스터를 만났을 때의 턴제 전투 진행.
//  - 시작할 때 Speed가 높은 쪽이 먼저 행동하고, 그 뒤로는 플레이어와 몬스터가 번갈아 행동한다.
//  - 플레이어: [공격] / [방어] / [도망]. 몬스터: 공격 또는 방어를 상황에 따라 고르고, 일정 확률로 플레이어를 제압한다.
//  - 계산식은 BattleCalc, 몬스터 능력치/보상은 Monster 에셋에 있다.
// FieldSearch가 같은 오브젝트에 자동으로 붙인다.
public class BattleSystem : MonoBehaviour
{
    private const float LineDelay = 1.0f;       // 문구 한 줄이 보이는 시간 (모든 문구 간격을 1초로 통일)
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

    // 지금 내 차례인가 (문구/턴을 진행 중이면 false)
    public bool CanAct { get { return active && !busy; } }

    // ===== 턴 표시 (필드 화면 왼쪽 위, 하이어라키의 TurnText 오브젝트) =====
    // 처음은 1턴이고, 플레이어와 몬스터가 서로 한 번씩 행동해야 다음 턴이 된다.
    private int turn = 1;
    private bool playerActed, monsterActed;
    private TextMeshProUGUI turnText;

    private void FindTurnText()
    {
        Transform parent = field != null && field.SearchText != null ? field.SearchText.transform.parent : null;
        Transform t = parent != null ? parent.Find("TurnText") : null;
        turnText = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        if (turnText != null) turnText.gameObject.SetActive(false); // 전투 중에만 보인다
    }

    private void ShowTurn(bool show)
    {
        if (turnText == null) return;
        turnText.gameObject.SetActive(show);
        if (show) turnText.text = "턴: " + turn;
    }

    private void MarkActed(bool player)
    {
        if (player) playerActed = true; else monsterActed = true;
        if (!playerActed || !monsterActed) return;
        turn++;
        playerActed = false;
        monsterActed = false;
        if (turnText != null) turnText.text = "턴: " + turn;
    }

    public void Init(FieldSearch f)
    {
        field = f;
        FindTurnText();
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
        statusTrait = null;
        statusTurnsLeft = 0;
        statusElapsed = 0;
        revivesUsed = 0;
        skillReadyTurn.Clear();
        InventoryManager binv = InventoryManager.Instance;
        sureHitLeft = binv != null ? Mathf.RoundToInt(binv.SumTrait(t => t.sureHitCount)) : 0;           // 필중 횟수 (한 전투)
        perfectLeft = binv != null ? Mathf.RoundToInt(binv.SumTrait(t => t.perfectDefendCount)) : 0;     // 완전 방어 횟수 (한 전투)
        ClearPlayerStatus();
        suppressed = false;
        lineShownAt = Time.time; // FieldSearch가 방금 띄운 "OO이(가) 나타났다!"가 읽힐 시간을 센다
        turn = 1;
        playerActed = false;
        monsterActed = false;
        ShowTurn(true);

        if (hud == null) hud = MonsterHUD.Create(field.SearchText.transform, field.SearchText.font);
        hud.Show(m.monsterName, monsterHp, m.hpMax);
        if (flash == null) flash = ScreenFlash.Create(field.SearchText.transform);

        StartCoroutine(BeginRoutine());
    }

    private IEnumerator BeginRoutine()
    {
        // 만났다는 문구(FieldSearch가 띄움)를 잠깐 보여 준 뒤 시작
        yield return new WaitForSeconds(LineDelay);

        InventoryManager inv = InventoryManager.Instance;

        // 전투를 시작할 때 한 번 체력을 회복하는 특성 (회복량 + 회복량%)
        float startHeal = inv != null ? inv.SumTrait(t => t.healAmount) + BattleCalc.PlayerMaxHp() * inv.SumTrait(t => t.healPercent) / 100f : 0f;
        if (startHeal > 0f && GameManager.Hp < BattleCalc.PlayerMaxHp())
        {
            int healed = HealPlayer(BattleCalc.FloorInt(startHeal));
            if (healed > 0) yield return Say($"체력을 {healed} 회복했다.");
        }

        // Speed가 높은 쪽이 먼저 행동 (같으면 플레이어가 먼저). 선제공격 횟수가 남아 있으면 하루에 정해진 횟수만큼 내가 먼저
        int firstStrikes = inv != null ? Mathf.RoundToInt(inv.SumTrait(t => t.firstStrikeCount)) : 0;
        bool quick = firstStrikes > GameManager.FirstStrikeUsed;
        if (quick && GameManager.Speed < monster.speed) GameManager.FirstStrikeUsed++; // 선제공격으로 먼저 움직이게 된 때만 횟수를 쓴다
        else quick = quick && GameManager.Speed < monster.speed;
        bool playerFirst = quick || GameManager.Speed >= monster.speed;
        if (playerFirst)
        {
            field.SearchText.SetText(quick ? "선제공격! 무엇을 할까?" : "먼저 움직일 수 있다! 무엇을 할까?");
        }
        else
        {
            yield return MonsterTurn(true); // 몬스터의 선제 행동
        }
        busy = false;
    }

    // 플레이어 체력을 회복한다 (회복량 감소 상태이상 적용, 최대 체력 한도). 실제로 회복한 양을 돌려줌
    private int HealPlayer(int amount)
    {
        int heal = BattleCalc.FloorInt(amount * BattleCalc.HealMult());
        int max = BattleCalc.PlayerMaxHp();
        int before = GameManager.Hp;
        GameManager.Hp = Mathf.Min(max, GameManager.Hp + Mathf.Max(0, heal));
        RefreshPlayerUI();
        return GameManager.Hp - before;
    }

    // =========================================================
    //  플레이어 행동 (FieldSearch의 버튼에서 호출)
    // =========================================================
    public void OnAttack()
    {
        if (!active || busy) return;
        StopNarration(); // 몬스터 턴의 남은 문구는 건너뛰고, 내 행동의 문구를 바로 보여 줌
        MarkActed(true);
        StartCoroutine(AttackRoutine());
    }

    public void OnDefend()
    {
        if (!active || busy) return;
        StopNarration(); // 몬스터 턴의 남은 문구는 건너뛰고, 내 행동의 문구를 바로 보여 줌
        MarkActed(true);
        StartCoroutine(DefendRoutine());
    }

    public void OnRun()
    {
        if (!active || busy) return;
        StopNarration(); // 몬스터 턴의 남은 문구는 건너뛰고, 내 행동의 문구를 바로 보여 줌
        MarkActed(true);
        StartCoroutine(RunRoutine());
    }

    // 전투 중 아이템을 썼다: 이번 턴을 쓴 것으로 치고 몬스터의 차례가 된다
    public void OnItemUsed(string message)
    {
        if (!active || busy) return;
        StopNarration();
        MarkActed(true);
        StartCoroutine(ItemTurnRoutine(message));
    }

    private IEnumerator ItemTurnRoutine(string message)
    {
        busy = true;
        yield return Say(message);
        yield return MonsterTurn();
        busy = false;
    }

    // 전투 중 위험한 아이템을 먹고 체력이 0이 되었다
    public void FaintByItem()
    {
        if (!active) return;
        StopNarration();
        busy = true;
        StartCoroutine(FaintByItemRoutine());
    }

    // 아이템 때문에 쓰러졌을 때도 부활 특성이 있으면 되살아나 전투를 이어 간다
    private IEnumerator FaintByItemRoutine()
    {
        List<Step> revive = new List<Step>();
        if (TryRevive(revive))
        {
            yield return Say(revive[0].text);
            GameManager.isfaint = false;
            yield return MonsterTurn();
            busy = false;
        }
        else
        {
            yield return FaintRoutine();
        }
    }

    // ===== 적에게 붙은 상태이상 (독 등. 특성 표에서 정함) =====
    private TraitDef statusTrait;   // 붙어 있는 상태이상의 특성
    private int statusTurnsLeft;    // 남은 턴
    private int statusElapsed;      // 지금까지 지난 턴 (지속 피해가 이만큼 커짐)

    // 이번 공격을 시작할 때의 특성 목록. 공격이 맞아 무기가 부서져 장착 해제돼도 이번 공격의 독/처형은 그대로 적용된다.
    private List<InventoryManager.TraitSource> traitSnapshot;

    private IEnumerator TryInflictStatus(string name)
    {
        if (InventoryManager.Instance == null) yield break;
        if (monsterHp <= 0) yield break; // 이미 쓰러뜨렸으면 독이 퍼졌다는 문구도 없다
        foreach (InventoryManager.TraitSource ts in (traitSnapshot ?? InventoryManager.Instance.ActiveTraits()))
        {
            TraitDef t = ts.trait;
            if (t.inflictChance <= 0f || (t.dotDamage <= 0f && t.dotHpPercent <= 0f)) continue;
            if (!BattleCalc.Roll(t.inflictChance)) continue;

            int duration = t.dotTurns > 0 ? t.dotTurns : 999;
            if (statusTrait != null && statusTrait.id == t.id && statusTurnsLeft > 0)
            {
                // 이미 걸려 있다: 지속 시간은 다시 채우지 않는다 (처음 걸린 때부터 정해진 턴만 지속)
                break;
            }

            statusTrait = t;
            statusTurnsLeft = duration;
            statusElapsed = 0;
            yield return Say($"{name}에게 {t.label}{Josa(t.label, "이", "가")} 퍼졌다!");
            break;
        }
    }

    // 처형 특성: 적 체력이 정해진 비율 미만이면 바로 쓰러뜨린다. 처형했으면 true
    private bool ShouldExecute()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null || monsterHp <= 0) return false;
        float pct = 0f;
        foreach (InventoryManager.TraitSource ts in (traitSnapshot ?? inv.ActiveTraits())) pct = Mathf.Max(pct, ts.trait.executeHpPercent);
        return pct > 0f && monsterHp * 100f < monster.hpMax * pct;
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
        InventoryManager inv = InventoryManager.Instance;
        traitSnapshot = inv != null ? inv.ActiveTraits() : null;
        string name = monster.monsterName;
        bool sureHit = sureHitLeft > 0;   // 필중 횟수가 남아 있으면 이번 공격은 회피와 방어 태세를 무시한다
        if (sureHit) sureHitLeft--;

        // 처형: 이미 체력이 충분히 낮으면 공격 판정 없이 바로 처형
        if (ShouldExecute())
        {
            monsterDefending = false;
            monsterHp = 0;
            hud.SetHp(0, monster.hpMax);
            yield return Say($"처형! {name}{Josa(name, "을", "를")} 쓰러뜨렸다!");
            string execReward = Proficiency.Reward(ProficiencyKind.Combat);
            if (execReward.Length > 0) yield return Say(execReward);
            yield return VictoryRoutine();
            yield break;
        }

        bool targetDefending = monsterDefending; // 몬스터가 지난 턴에 방어를 골랐는가
        monsterDefending = false;                // 몬스터의 방어는 이 공격 한 번에만 적용
        string stealNote = sureHit ? "필중! " : "";
        if (sureHit) targetDefending = false;

        BattleCalc.AttackResult r = BattleCalc.PlayerAttack(monster, monsterHp, targetDefending, sureHit);

        if (r.dodged || r.noEffect)
        {
            // 피하거나 막혀서 피해가 안 들어감
            if (targetDefending)
                yield return Say($"{stealNote}{name}{Josa(name, "이", "가")} 방어했다!");
            else if (r.dodged)
                yield return Say($"{stealNote}{name}{Josa(name, "이", "가")} 공격을 피했다!");
            else
                yield return Say($"{stealNote}효과가 없는 것 같다...");
        }
        else
        {
            monsterHp = Mathf.Max(0, monsterHp - r.damage);
            string wearInfo = inv != null ? inv.WearEquipment(true) : ""; // 공격이 맞았을 때만 무기가 닳는다 (피하거나 막히면 안 닳음)
            hud.SetHp(monsterHp, monster.hpMax);

            // 문구: 방어 중이던 적은 "{적}이 n의 데미지를 받았다!", 그 외는 "n의 데미지를 주었다!"
            string crit = r.crit ? "치명타! " : "";
            if (targetDefending)
                yield return Say($"{stealNote}{crit}{name}{Josa(name, "이", "가")} {r.damage}의 데미지를 받았다!{wearInfo}");
            else
                yield return Say($"{stealNote}{crit}{r.damage}의 데미지를 주었다!{wearInfo}");

            // 독 같은 특성이 있으면 확률적으로 적에게 상태이상을 건다
            yield return TryInflictStatus(name);

            if (r.heal > 0)
            {
                int healed = HealPlayerRaw(r.heal);
                if (healed > 0) yield return Say($"체력을 {healed} 흡수했다.");
            }

            // 처형: 공격으로 체력이 기준 아래로 떨어지면 마무리
            if (ShouldExecute())
            {
                monsterHp = 0;
                hud.SetHp(0, monster.hpMax);
                yield return Say($"처형! {name}{Josa(name, "을", "를")} 쓰러뜨렸다!");
            }
        }

        // 공격이 피해졌으면 경험치 없음. 아니면 전투 숙련도와 플레이어 레벨 경험치를 얻는다
        if (!r.dodged)
        {
            string combatReward = Proficiency.Reward(ProficiencyKind.Combat);
            if (combatReward.Length > 0) yield return Say(combatReward);
        }

        if (monsterHp <= 0)
        {
            yield return VictoryRoutine();
            yield break;
        }

        yield return MonsterTurn();
        busy = false;
    }

    // =========================================================
    //  스킬 (스킬 표). [스킬] 버튼 -> 스킬 창에서 고름 -> 마나/SP를 내고 이번 턴을 쓴다
    // =========================================================
    private readonly Dictionary<int, int> skillReadyTurn = new Dictionary<int, int>(); // 스킬 번호 -> 다시 쓸 수 있는 턴 (이번 전투)

    // 이 스킬의 실제 마나 소모 (특성 "스킬 마나 감소 %" 반영)
    public static int SkillManaCost(SkillDef s)
    {
        float reduce = Mathf.Clamp(GameManager.SkillManaReduce, 0f, 100f);
        return Mathf.Max(0, BattleCalc.FloorInt(s.manaCost * (1f - reduce / 100f)));
    }

    // 지금 쓸 수 없는 이유 (쓸 수 있으면 null)
    public string SkillBlockReason(SkillDef s)
    {
        if (s == null) return "스킬이 없다";
        if (GameManager.Level < s.unlockLevel) return $"Lv.{s.unlockLevel}부터 쓸 수 있다";
        if (s.weaponType != WeaponType.None && (InventoryManager.Instance == null || !InventoryManager.Instance.HasEquippedWeaponType(s.weaponType))) { string wn = WeaponTypeInfo.Name(s.weaponType); return $"{wn}{Josa(wn, "을", "를")} 장착해야 쓸 수 있다"; }
        if (skillReadyTurn.TryGetValue(s.id, out int ready) && turn < ready) return $"{ready - turn}턴 뒤에 쓸 수 있다";
        if (GameManager.Mana < SkillManaCost(s)) return "마나가 부족하다";
        if (GameManager.SP < s.spCost) return "SP가 부족하다";
        return null;
    }

    // 스킬을 쓴다. 쓸 수 없으면 false (턴을 쓰지 않음)
    public bool UseSkill(SkillDef s)
    {
        if (!active || busy || SkillBlockReason(s) != null) return false;
        StopNarration();
        int usedTurn = turn;
        MarkActed(true);
        StartCoroutine(SkillRoutine(s, usedTurn));
        return true;
    }

    private IEnumerator SkillRoutine(SkillDef s, int usedTurn)
    {
        busy = true;
        GameManager.Mana = Mathf.Max(0, GameManager.Mana - SkillManaCost(s));
        GameManager.SP = Mathf.Max(0, GameManager.SP - s.spCost);
        skillReadyTurn[s.id] = usedTurn + s.cooldown + 1;
        RefreshPlayerUI();

        InventoryManager inv = InventoryManager.Instance;
        traitSnapshot = inv != null ? inv.ActiveTraits() : null;
        string name = monster.monsterName;

        if (s.kind == SkillKind.Heal)
        {
            PlaySound(SoundEvent.Defend);
            int amount = s.healAmount + Mathf.RoundToInt(BattleCalc.PlayerMaxHp() * s.healPercent / 100f);
            int healed = HealPlayer(amount);
            string line = healed > 0 ? $"{s.label}! 체력을 {healed} 회복했다." : $"{s.label}! 체력은 이미 가득 차 있다.";
            if (s.cleanse && GameManager.StatusTraitId > 0)
            {
                ClearPlayerStatus();
                line += " 상태이상이 치료되었다.";
            }
            yield return Say(line);
            yield return MonsterTurn();
            busy = false;
            yield break;
        }

        yield return Say($"{s.label}!");

        bool sure = s.sureHit;
        if (!sure && sureHitLeft > 0) { sureHitLeft--; sure = true; } // 필중 특성 횟수가 남아 있으면 스킬에도 쓴다
        bool defending = monsterDefending && !sure;
        monsterDefending = false;

        bool worn = false, connected = false, notDodged = false;
        int absorbed = 0;

        for (int i = 0; i < s.hits && monsterHp > 0; i++)
        {
            PlaySound(SoundEvent.PlayerAttack);
            string prefix = s.hits > 1 ? $"{i + 1}타! " : "";
            if (sure && i == 0) prefix = "필중! " + prefix;

            BattleCalc.AttackResult r = BattleCalc.PlayerAttack(monster, monsterHp, defending, sure, s);
            if (!r.dodged) notDodged = true;

            if (r.dodged || r.noEffect)
            {
                if (defending) yield return Say($"{prefix}{name}{Josa(name, "이", "가")} 방어했다!");
                else if (r.dodged) yield return Say($"{prefix}{name}{Josa(name, "이", "가")} 공격을 피했다!");
                else yield return Say($"{prefix}효과가 없는 것 같다...");
            }
            else
            {
                connected = true;
                monsterHp = Mathf.Max(0, monsterHp - r.damage);
                string wear = "";
                if (!worn && inv != null) { wear = inv.WearEquipment(true); worn = true; } // 맞았을 때 한 번만 무기가 닳는다
                hud.SetHp(monsterHp, monster.hpMax);
                string crit = r.crit ? "치명타! " : "";
                if (defending)
                    yield return Say($"{prefix}{crit}{name}{Josa(name, "이", "가")} {r.damage}의 데미지를 받았다!{wear}");
                else
                    yield return Say($"{prefix}{crit}{r.damage}의 데미지를 주었다!{wear}");
                absorbed += r.heal;

                if (ShouldExecute())
                {
                    monsterHp = 0;
                    hud.SetHp(0, monster.hpMax);
                    yield return Say($"처형! {name}{Josa(name, "을", "를")} 쓰러뜨렸다!");
                }
            }
            defending = false; // 방어 태세는 첫 타에만 통한다
        }

        if (connected) yield return InflictSkillStatus(s);

        if (absorbed > 0)
        {
            int healed = HealPlayerRaw(absorbed);
            if (healed > 0) yield return Say($"체력을 {healed} 흡수했다.");
        }

        if (notDodged)
        {
            string combatReward = Proficiency.Reward(ProficiencyKind.Combat);
            if (combatReward.Length > 0) yield return Say(combatReward);
        }

        if (monsterHp <= 0)
        {
            yield return VictoryRoutine();
            yield break;
        }

        yield return MonsterTurn();
        busy = false;
    }

    // 스킬이 거는 상태이상 (스킬 표의 상태이상 ID, 부여 확률)
    private IEnumerator InflictSkillStatus(SkillDef s)
    {
        if (s.inflictTraitId <= 0 || monsterHp <= 0) yield break;
        TraitDef t = GameTables.Traits.Get(s.inflictTraitId);
        if (t == null || !BattleCalc.Roll(s.inflictChance)) yield break;
        if (statusTrait != null && statusTrait.id == t.id && statusTurnsLeft > 0) yield break; // 이미 걸려 있음

        statusTrait = t;
        statusTurnsLeft = t.dotTurns > 0 ? t.dotTurns : 999;
        statusElapsed = 0;
        string name = monster.monsterName;
        yield return Say($"{name}에게 {t.label}{Josa(t.label, "이", "가")} 퍼졌다!");
    }

    // 흡수처럼 이미 회복량 감소가 적용된 값을 체력에 더한다. 실제로 오른 양을 돌려줌
    private int HealPlayerRaw(int amount)
    {
        int before = GameManager.Hp;
        GameManager.Hp = Mathf.Min(BattleCalc.PlayerMaxHp(), GameManager.Hp + Mathf.Max(0, amount));
        RefreshPlayerUI();
        return GameManager.Hp - before;
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
        string defendReward = Proficiency.Reward(ProficiencyKind.Combat);
        if (defendReward.Length > 0) yield return Say(defendReward);
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
    //    - 방어 중이고 피해가 0: "{몬스터}의 공격을 방어했다!"
    //    - 방어 중이고 일부만 막음: "{몬스터}의 공격을 받아내 N의 데미지를 받았다!"
    //    - 피해 0: "효과가 없는 것 같다..."
    //    - 그 외: "N의 데미지를 받았다!"
    // 방어를 고르면: "{몬스터}의 방어!" (결과는 플레이어가 다음에 공격할 때 "{몬스터}이/가 방어했다!" 또는 "방어가 실패했다..."로 나옴)

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
        // 마지막 문구도 LineDelay(1초) 동안은 그대로 보인 뒤에 "무엇을 할까?"가 붙는다 (그 사이 행동하면 바로 넘어감)
        float remain = lineShownAt + LineDelay - Time.time;
        if (remain > 0f) yield return new WaitForSeconds(remain);
        narration = null;
        EndTurn(); // 마지막 문구 아래에 "무엇을 할까?"
    }

    // 몬스터가 선공이면(전투 시작 때 Speed가 높아서 먼저 행동) preemptive가 true
    private IEnumerator MonsterTurn(bool preemptive = false)
    {
        // 이전 제압은 한 턴만 지속되므로 여기서 풀린다
        suppressed = false;

        string name = monster.monsterName;
        InventoryManager inv = InventoryManager.Instance;

        // 상태이상(독 등)의 지속 피해: 턴이 지날수록 커지고, 최대 체력 비례 피해도 더해진다
        if (statusTrait != null && statusTurnsLeft > 0)
        {
            float dot = statusTrait.dotDamage + statusTrait.dotGrowth * statusElapsed
                + monsterHp * (statusTrait.dotHpPercent + statusTrait.dotHpGrowth * statusElapsed) / 100f; // 체력 퍼뎀은 현재 체력 비례
            int dmg = Mathf.Max(1, BattleCalc.FloorInt(dot));
            statusElapsed++;
            statusTurnsLeft--;
            yield return Say($"{name}{Josa(name, "이", "가")} {statusTrait.label}{Josa(statusTrait.label, "으로", "로")} 인해 {dmg}의 데미지를 입었다!"); // 문구가 보인 뒤에
            monsterHp = Mathf.Max(0, monsterHp - dmg);                                // 체력이 깎인다
            hud.SetHp(monsterHp, monster.hpMax);
            if (statusTurnsLeft <= 0) statusTrait = null;
            if (monsterHp <= 0)
            {
                yield return VictoryRoutine();
                yield break;
            }
        }

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
            bool perfect = defended && perfectLeft > 0; // 굳히기: 횟수가 남아 있으면 [방어]가 완전 방어
            if (perfect) perfectLeft--;
            BattleCalc.HitResult h = BattleCalc.MonsterAttack(monster, defended, perfect);

            // 1) 공격 알림과 동시에 판정을 적용한다: 효과음, 맞았다면 화면이 바로 붉어지고 체력이 줄어듦
            yield return Say(preemptive ? $"{name}의 선제공격!" : $"{name}의 공격!");
            PlaySound(SoundEvent.EnemyAttack);

            bool perfectBlock = defended && !h.dodged && h.noEffect;
            bool gotHit = !h.dodged && !perfectBlock; // 타격 여부: 회피하지도, 완전히 막지도 못함
            if (gotHit && flash != null) flash.Play(HitFlashColor);
            if (gotHit && inv != null) inv.WearEquipment(false); // 맞으면 방어구가 닳는다

            // 2) 결과 문구는 잠시 뒤에 나온다
            if (h.dodged)
            {
                steps.Add(new Step("피했다!", HitResultDelay));
            }
            else if (perfectBlock)
            {
                steps.Add(new Step($"{name}의 공격을 방어했다!", HitResultDelay));
            }
            else if (h.noEffect)
            {
                steps.Add(new Step("효과가 없는 것 같다...", HitResultDelay));
            }
            else
            {
                GameManager.Hp = Mathf.Max(0, GameManager.Hp - h.damage);
                RefreshPlayerUI();
                steps.Add(new Step($"{h.damage}의 데미지를 받았다!", HitResultDelay));

                // 가시: 받은 피해의 일부를 적에게 되돌린다
                float reflectPct = inv != null ? inv.SumTrait(t => t.reflectPercent) : 0f;
                if (reflectPct > 0f && monsterHp > 0)
                {
                    int reflect = Mathf.Max(1, BattleCalc.FloorInt(h.damage * reflectPct / 100f));
                    monsterHp = Mathf.Max(0, monsterHp - reflect);
                    hud.SetHp(monsterHp, monster.hpMax);
                    steps.Add(new Step($"{reflect}의 피해를 반사했다!", LineDelay));
                }

                // 몬스터가 특성(독 등)을 건다. 이미 그 특성을 가졌거나 이미 걸려 있으면 걸리지 않는다
                TryInflictOnPlayer(steps);
            }
        }
        playerDefending = false; // [방어]는 몬스터의 한 번의 행동에만 적용

        // 턴이 끝날 때의 효과: 내게 걸린 상태이상의 피해, 마나/체력 회복 특성
        TurnTick(steps);

        // 체력이 0이 됐다: 부활 특성이 있으면 부활
        if (GameManager.Hp <= 0) TryRevive(steps);

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

        // 반사 피해로 적이 쓰러졌다: 문구를 보여 준 뒤 승리
        if (monsterHp <= 0)
        {
            foreach (Step step in steps)
            {
                if (step.delayBefore > 0f) yield return new WaitForSeconds(step.delayBefore);
                field.SearchText.SetText(step.text);
                lineShownAt = Time.time;
            }
            yield return VictoryRoutine();
            yield break;
        }

        // 제압: 일정 확률로 플레이어가 다음 턴에 도망치지 못하게 한다 (판정은 지금, 알림 문구는 이어서)
        if (BattleCalc.Roll(monster.suppressChance))
        {
            suppressed = true;
            steps.Add(new Step($"{name}{Josa(name, "이", "가")} 당신을 제압했다! 다음 턴에는 도망칠 수 없다.", LineDelay));
        }

        // 판정은 끝났다. 문구는 백그라운드로 이어 보여 주고, 호출한 쪽은 바로 플레이어의 입력을 받는다
        MarkActed(false); // 몬스터도 행동했다
        narration = StartCoroutine(Narrate(steps));
    }

    // 몬스터가 맞춘 공격으로 플레이어에게 특성(상태이상)을 건다
    private void TryInflictOnPlayer(List<Step> steps)
    {
        if (monster.inflictTraitId <= 0 || !BattleCalc.Roll(monster.inflictChance)) return;
        TraitDef t = GameTables.Traits.Get(monster.inflictTraitId);
        if (t == null) return;
        if (GameManager.StatusTraitId == t.id && GameManager.StatusTurnsLeft > 0) return; // 이미 걸려 있음

        // 내가 이미 그 특성을 가지고 있으면 걸리지 않는다 (디버프 특성끼리의 면역)
        if (t.debuff && InventoryManager.Instance != null && InventoryManager.Instance.HasTrait(t.id))
        {
            steps.Add(new Step($"이미 {t.label} 특성이 있어 걸리지 않았다!", LineDelay));
            return;
        }
        // 정화 특성이 있으면 상태이상은 걸리지 않는다
        if (InventoryManager.Instance != null && InventoryManager.Instance.AnyTrait(x => x.cleanse)) return;

        GameManager.StatusTraitId = t.id;
        GameManager.StatusTurnsLeft = t.dotTurns > 0 ? t.dotTurns : 999;
        GameManager.StatusElapsed = 0;
        steps.Add(new Step($"{t.label}에 걸렸다!", LineDelay));
    }

    // 턴이 끝날 때마다: 내게 걸린 상태이상 피해, 마나 회복(마나 도둑), 체력 회복(회복)
    private void TurnTick(List<Step> steps)
    {
        InventoryManager inv = InventoryManager.Instance;

        // 정화: 내게 걸린 상태이상을 지운다
        if (GameManager.StatusTraitId > 0 && inv != null && inv.AnyTrait(t => t.cleanse))
        {
            TraitDef gone = GameTables.Traits.Get(GameManager.StatusTraitId);
            ClearPlayerStatus();
            if (gone != null) steps.Add(new Step($"정화로 {gone.label}{Josa(gone.label, "이", "가")} 사라졌다!", LineDelay));
        }

        // 몬스터가 건 상태이상(독 등): 턴마다 지속 피해. 독 내성만큼 피해가 줄어든다
        if (GameManager.StatusTraitId > 0)
        {
            TraitDef st = GameTables.Traits.Get(GameManager.StatusTraitId);
            if (st != null && GameManager.StatusTurnsLeft > 0)
            {
                float dot = st.dotDamage + st.dotGrowth * GameManager.StatusElapsed
                    + GameManager.Hp * (st.dotHpPercent + st.dotHpGrowth * GameManager.StatusElapsed) / 100f; // 현재 체력 비례
                dot -= InventoryManager.PoisonResist() * st.resistPercent / 100f; // 독 내성 x 내성 적용%
                int dmg = Mathf.Max(1, BattleCalc.FloorInt(dot));
                GameManager.StatusElapsed++;
                GameManager.StatusTurnsLeft--;
                GameManager.Hp = Mathf.Max(0, GameManager.Hp - dmg);
                RefreshPlayerUI();
                steps.Add(new Step($"{st.label}{Josa(st.label, "으로", "로")} 인해 {dmg}의 데미지를 받았다!", LineDelay));
                if (GameManager.StatusTurnsLeft <= 0)
                {
                    ClearPlayerStatus();
                    steps.Add(new Step($"{st.label}{Josa(st.label, "이", "가")} 사라졌다.", LineDelay));
                }
            }
        }

        if (inv == null || GameManager.Hp <= 0) return;

        // 마나 도둑: 턴마다 마나 회복
        int manaGain = BattleCalc.FloorInt(inv.SumTrait(t => t.manaPerTurn) + GameManager.ManaMax * inv.SumTrait(t => t.manaPercentPerTurn) / 100f);
        if (manaGain > 0 && GameManager.Mana < GameManager.ManaMax)
        {
            int before = GameManager.Mana;
            GameManager.Mana = Mathf.Min(GameManager.ManaMax, GameManager.Mana + manaGain);
            RefreshPlayerUI();
            steps.Add(new Step($"마나를 {GameManager.Mana - before} 회복했다.", LineDelay));
        }

        // 회복: 턴마다 체력 회복 (정량 + 최대 체력 비례)
        float regen = inv.SumTrait(t => t.regenPerTurn) + BattleCalc.PlayerMaxHp() * inv.SumTrait(t => t.regenHpPercent) / 100f;
        if (regen > 0f && GameManager.Hp < BattleCalc.PlayerMaxHp())
        {
            int healed = HealPlayer(BattleCalc.FloorInt(regen));
            if (healed > 0) steps.Add(new Step($"체력을 {healed} 회복했다.", LineDelay));
        }
    }

    private static void ClearPlayerStatus()
    {
        GameManager.StatusTraitId = 0;
        GameManager.StatusTurnsLeft = 0;
        GameManager.StatusElapsed = 0;
    }

    // 부활 특성: 체력이 0이 되면 한 전투에 정해진 횟수만큼 되살아난다. 부활했으면 true
    private int revivesUsed;
    private int sureHitLeft, perfectLeft;   // 이번 전투에서 남은 필중 / 완전 방어 횟수
    private bool TryRevive(List<Step> steps)
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return false;
        int allowed = Mathf.RoundToInt(inv.SumTrait(t => t.reviveCount));
        if (revivesUsed >= allowed) return false;

        revivesUsed++;
        float pct = Mathf.Max(1f, inv.MaxTrait(t => t.reviveHpPercent));
        GameManager.Hp = Mathf.Max(1, Mathf.RoundToInt(BattleCalc.PlayerMaxHp() * Mathf.Clamp(pct, 1f, 100f) / 100f));
        ClearPlayerStatus();
        RefreshPlayerUI();
        steps.Add(new Step("부활 특성으로 부활하였다!", LineDelay));
        return true;
    }

    // =========================================================
    //  승리 / 패배
    // =========================================================
    private IEnumerator VictoryRoutine()
    {
        yield return Say($"{monster.monsterName}{Josa(monster.monsterName, "을", "를")} 쓰러뜨렸다!");

        // 골드와 경험치는 각각 보너스(증감률)를 곱해서 받는다
        int gold = BattleCalc.FloorInt(monster.gold * BattleCalc.Mult(GameManager.GoldR));
        int exp = BattleCalc.FloorInt(monster.exp * BattleCalc.Mult(GameManager.ExpR));
        GameManager.Gold += gold;
        int oldLevel = GameManager.Level;
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
        ItemGainToast.ShowCenter(LevelSystem.Describe(oldLevel, levelUps)); // 레벨이 올랐다! 는 숙련도 알림과 같이 화면 중앙에
        yield return Say(reward); // 직전 문구("쓰러뜨렸다!")가 충분히 보인 뒤에 보상을 보여 줌

        field.RestoreExploreButtons();
        field.StartCarving(monster); // 칼로 도려낼 수 있는 몬스터면 [도려내기] 버튼을 켠다
        EndBattle();
        busy = false;
    }

    // 플레이어 체력이 0이 되었을 때: 체력을 채우고 병원비/아이템 손실을 처리한 뒤,
    // 화면이 완전히 어두워지고 -> 마을로 이동하고 -> "눈 앞이 깜깜해졌다..."가 뜬 뒤 글자와 검은 화면이 함께 걷히는 연출
    private IEnumerator FaintRoutine()
    {
        yield return new WaitForSeconds(LineDelay); // 직전 문구(데미지 등)를 읽을 시간

        GameManager.Hp = BattleCalc.PlayerMaxHp();
        GameManager.isfaint = false;

        // 기절하면 병원비를 내고 아이템 일부를 잃는다 (기본 능력치 표의 [기절] 설정)
        string penalty = InventoryManager.Instance != null ? InventoryManager.Instance.ApplyFaintPenalty() : "";
        RefreshPlayerUI();
        string after = string.IsNullOrEmpty(penalty) ? "병원에서 치료를 받았다." : "병원에서 치료를 받았다. " + penalty + ".";

        EndBattle();
        busy = false;
        FaintSequence.Play("눈 앞이 깜깜해졌다...", after);
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
        ShowTurn(false);
        ClearPlayerStatus(); // 몬스터가 건 상태이상은 전투가 끝나면 사라진다
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
        if (action == SPAction.Attack || action == SPAction.Defend)
            cost = Proficiency.ReducedSp(ProficiencyKind.Combat, cost); // 전투 숙련도가 SP를 줄여 줌
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
