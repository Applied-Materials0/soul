using System;
using UnityEngine;

public enum DayPeriod { Morning, Afternoon, Night }   // 오전, 오후, 밤
public enum WeatherType { Sunny, Cloudy, Rain, Snow, Storm, Thunder, Heat, Cold } // 맑음, 흐림, 비, 눈, 폭풍우, 뇌우, 폭염, 한파

// 날짜 / 시간대 / 날씨. 게임을 켜 둔 동안 유지된다 (GameManager처럼 정적 변수).
// 시간은 마을의 [휴식] 버튼으로만 흐른다: 오전 -> (휴식) -> 오후 -> (휴식) -> 밤 -> (휴식) -> 다음 날 오전.
//  - 다음 날 오전: HP와 SP가 가득 참
//  - 오후가 됨: SP가 가득 참 (기본 100%)
//  - 밤이 됨: SP가 최대치의 일부(기본 50%)만 참. 화면이 검푸르게 어두워짐 (NightOverlay)
// 회복 비율은 기본 능력치 표(Soul > 데이터 표 > 기본 능력치)에서 고친다.
public static class GameTime
{
    public static int Day = 1;
    public static DayPeriod Period = DayPeriod.Morning;
    public static WeatherType Weather = WeatherType.Sunny;

    // 날짜/시간대/날씨가 바뀔 때마다 (HUD가 글자를 갱신함)
    public static event Action Changed;

    public static string PeriodName(DayPeriod p)
    {
        switch (p)
        {
            case DayPeriod.Morning: return "오전";
            case DayPeriod.Afternoon: return "오후";
            default: return "밤";
        }
    }

    public static string WeatherName(WeatherType w)
    {
        switch (w)
        {
            case WeatherType.Sunny: return "맑음";
            case WeatherType.Cloudy: return "흐림";
            case WeatherType.Rain: return "비";
            case WeatherType.Snow: return "눈";
            case WeatherType.Storm: return "폭풍우";
            case WeatherType.Thunder: return "뇌우";
            case WeatherType.Heat: return "폭염";
            default: return "한파";
        }
    }

    // 새 게임: 1일 오전, 날씨는 새로 뽑는다
    public static void StartNewGame()
    {
        Day = 1;
        Period = DayPeriod.Morning;
        Weather = RollWeather();
        Changed?.Invoke();
    }

    // 날씨가 나올 비중 (WeatherType 순서대로). 세부 효과는 아직 없고 화면에 표시만 한다
    private static readonly int[] WeatherWeights = { 35, 25, 15, 5, 5, 5, 5, 5 };

    private static WeatherType RollWeather()
    {
        int total = 0;
        foreach (int w in WeatherWeights) total += w;
        int roll = UnityEngine.Random.Range(0, total);
        for (int i = 0; i < WeatherWeights.Length; i++)
        {
            roll -= WeatherWeights[i];
            if (roll < 0) return (WeatherType)i;
        }
        return WeatherType.Sunny;
    }

    // 기절하면 시간대가 하나 건너뛰어진다 (오전 -> 오후, 오후 -> 밤, 밤 -> 다음 날 오전).
    // 회복은 하지 않는다 (체력은 병원 치료로 참). 다음 날이 되면 하루의 시작이므로 체력과 SP가 가득 찬다.
    public static void SkipPeriod()
    {
        switch (Period)
        {
            case DayPeriod.Morning:
                Period = DayPeriod.Afternoon;
                break;
            case DayPeriod.Afternoon:
                Period = DayPeriod.Night;
                break;
            default:
                Day++;
                Period = DayPeriod.Morning;
                Weather = RollWeather();
                GameManager.Hp = BattleCalc.PlayerMaxHp();
                Exhaustion.Reset();
                GameManager.FirstStrikeUsed = 0;
                GameManager.SP = GameManager.SPMax;
                GameManager.Mana = GameManager.ManaMax;
                break;
        }
        PlayerUI.RefreshAll();
        Changed?.Invoke();
    }

    // 휴식: 다음 시간대로 넘어가며 회복한다. 화면에 보여 줄 문구를 돌려준다.
    public static string Rest()
    {
        PlayerBaseTable b = GameTables.PlayerBase;
        string message;

        switch (Period)
        {
            case DayPeriod.Morning:
            {
                Period = DayPeriod.Afternoon;
                int gain = Mathf.RoundToInt(GameManager.SPMax * Mathf.Clamp(b.restAfternoonSpPercent, 0f, 100f) / 100f);
                GameManager.SP = Mathf.Min(GameManager.SPMax, GameManager.SP + gain);
                message = "오후가 되었다.\nSP를 회복했다.";
                break;
            }
            case DayPeriod.Afternoon:
            {
                Period = DayPeriod.Night;
                int gain = Mathf.RoundToInt(GameManager.SPMax * Mathf.Clamp(b.restNightSpPercent, 0f, 100f) / 100f);
                GameManager.SP = Mathf.Min(GameManager.SPMax, GameManager.SP + gain);
                message = "밤이 되었다.\nSP를 " +gain.ToString("N0") + " 회복했다.";
                break;
            }
            default:
            {
                Day++;
                Period = DayPeriod.Morning;
                Weather = RollWeather();
                GameManager.Hp = BattleCalc.PlayerMaxHp();
                Exhaustion.Reset();
                GameManager.FirstStrikeUsed = 0;
                GameManager.SP = GameManager.SPMax;
                GameManager.Mana = GameManager.ManaMax;
                message = "푹 자고 일어났다. " + Day + "일째 오전, 날씨는 " + WeatherName(Weather) + ".\n체력, SP, 마나가 가득 찼다.";
                break;
            }
        }

        PlayerUI.RefreshAll();
        Changed?.Invoke();
        return message;
    }
}
