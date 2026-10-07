using System.Collections;
using UnityEngine;

public class UISlideController : MonoBehaviour
{
    [Header("슬라이드 컨테이너")]
    public RectTransform slideContainer; // 이동시킬 부모 패널

    [Header("설정")]
    public float pageWidth = 1920f;      // 한 페이지의 가로 너비 (해상도 너비)
    public float slideDuration = 1.0f;   // 슬라이드되는 시간 (초)

    private int currentPageIndex = 0;    // 현재 페이지 번호 (0, 1, 2...)
    private bool isSliding = false;       // 중복 클릭 방지 플래그

    // [다음 페이지] 버튼에 연결할 함수
    public void NextPage()
    {
        if (isSliding) return;

        currentPageIndex++;
        MoveToPage(currentPageIndex);
    }

    //  [이전 페이지] 버튼에 연결할 함수
    public void PreviousPage()
    {
        if (isSliding || currentPageIndex <= 0) return;

        currentPageIndex--;
        MoveToPage(currentPageIndex);
    }

    //  특정 페이지 번호로 이동시키는 공용 함수
    public void MoveToPage(int pageIndex)
    {
        if (isSliding) return;

        currentPageIndex = pageIndex;
        // 목표 X 위치 계산 (페이지 번호가 커질수록 컨테이너는 왼쪽(-)으로 이동)
        float targetX = -currentPageIndex * pageWidth;

        StartCoroutine(CoSlideToX(targetX));
    }

    private IEnumerator CoSlideToX(float targetX)
    {
        isSliding = true;

        Vector2 startPos = slideContainer.anchoredPosition;
        Vector2 targetPos = new Vector2(targetX, startPos.y);
        float timer = 0f;

        while (timer < slideDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / slideDuration;

            // Mathf.SmoothStep: 시작과 끝이 부드럽게 감속되는 효과 (PPT 스타일)
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            slideContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, smoothProgress);
            yield return null;
        }

        slideContainer.anchoredPosition = targetPos;
        isSliding = false;
    }
}