using UnityEngine;

// 키보드 입력 설정. 윈도우의 한글 입력기(IME)가 켜져 있으면 W/A/S/D 같은 글자 키가 게임에 전달되지 않고
// Space/Enter/방향키/Esc/Tab만 되는 일이 있다. 게임이 시작될 때 IME를 꺼서 한글 상태에서도 글자 키가 먹게 한다.
// (수량 입력칸은 숫자만 받으므로 영향 없음)
public static class InputSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void DisableIme()
    {
        Input.imeCompositionMode = IMECompositionMode.Off;
    }
}
