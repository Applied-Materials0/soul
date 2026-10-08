#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 개발용: 에디터에서 Field 같은 중간 씬을 바로 실행해도 게임이 정상 동작하도록 한다.
//
// 가방, 맵, 스탯 같은 전역 UI와 매니저는 LoopTown 씬에서 만들어져 씬이 바뀌어도 유지된다.
// 그래서 Field 씬에서 바로 실행하면 그것들이 없어서 가방/맵 버튼 등이 동작하지 않고, 지역 ID도 기본값(0)이라
// "잘못된 지역 ID" 에러가 났다. 이 코드는 그런 경우에 다음 순서로 처리한다.
//   1) LoopTown을 먼저 불러와 전역 UI와 매니저를 만든다.
//   2) 처음에 실행했던 씬에 해당하는 지역 ID를 맵 데이터에서 찾아 맞춘다.
//   3) 처음 씬을 다시 불러온다.
// 에디터에서만 동작하며(빌드에는 포함되지 않음), TitleScene이나 LoopTown에서 시작하면 아무 일도 하지 않는다.
public static class DevStartup
{
    private const string TownScene = "LoopTown";
    private const string TitleScene = "TitleScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Run()
    {
        string startScene = SceneManager.GetActiveScene().name;

        // 평소 시작 씬이거나, 이미 전역 UI가 만들어져 있으면 할 일이 없다
        if (startScene == TownScene || startScene == TitleScene) return;
        if (GlobalUI.Instance != null) return;

        GameObject host = new GameObject("DevStartup");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<Runner>().Begin(startScene);
    }

    private class Runner : MonoBehaviour
    {
        public void Begin(string startScene)
        {
            StartCoroutine(Routine(startScene));
        }

        private IEnumerator Routine(string startScene)
        {
            // 1) LoopTown을 불러와 전역 UI와 매니저를 만든다
            SceneManager.LoadScene(TownScene);
            yield return null;
            while (GlobalUI.Instance == null) yield return null;
            yield return null; // 다른 Awake/Start가 끝나도록 한 프레임 더

            // 2) 시작했던 씬에 해당하는 지역 ID를 맵 데이터에서 찾는다 (예: Field -> 숲 1)
            foreach (MapRegionButton button in FindObjectsByType<MapRegionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Region region = button.regionData;
                if (region == null || string.IsNullOrEmpty(region.sceneName)) continue;
                if (string.Equals(region.sceneName, startScene, System.StringComparison.OrdinalIgnoreCase))
                {
                    GameManager.selectedRegionID = region.id;
                    break;
                }
            }

            // 3) 처음 실행했던 씬으로 돌아간다
            SceneManager.LoadScene(startScene);
            Destroy(gameObject);
        }
    }
}
#endif
