using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance;

    [Header("UI 연결")]
    public CanvasGroup fadeCanvasGroup; // 화면을 가릴 검은색 이미지의 CanvasGroup
    public float fadeDuration = 0.5f;   // 페이드 되는 시간 (초)

    private bool isFading = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //다른 스크립트에서 씬 이동 시 호출할 핵심 함수!
    public void LoadSceneWithFade(string sceneName)
    {
        if (!isFading)
        {
            StartCoroutine(FadeAndLoadScene(sceneName));
        }
    }

    private IEnumerator FadeAndLoadScene(string sceneName)
    {
        isFading = true;

        // 1. 화면 검은색으로 스르륵 페이드 아웃
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(timer / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;

        // 2. 완전히 어두워진 상태에서 씬 로딩 시작!
        SceneManager.LoadScene(sceneName);

        // 3. 씬 이동이 완료될 때까지 한 프레임 대기
        yield return null;

        // 4. 지도 UI 닫기 (검은 화면 뒤에서 지도가 닫히므로 안 보임!)
        if (MapManager.Instance != null)
        {
            MapManager.Instance.CloseMap();
        }

        // 5. 새 씬에서 화면 스르륵 다시 밝아지기 (페이드 인)
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (timer / fadeDuration));
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;

        isFading = false;
    }
}