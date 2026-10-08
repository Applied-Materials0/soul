using System;
using UnityEditor;
using UnityEngine;

// 엑셀의 "틀 고정"처럼 동작하는 표 그리기 도구.
//  - 맨 위 머리글 줄은 아래로 스크롤해도 항상 보인다.
//  - 왼쪽의 고정 열(예: ID, 이름)은 오른쪽으로 스크롤해도 항상 보인다.
//  - 나머지 칸만 스크롤된다.
// 모든 칸은 Rect로 직접 배치하므로(GUILayout을 쓰지 않음) 머리글과 칸이 어긋나지 않는다.
public static class FrozenGrid
{
    public const float ScrollbarSize = 15f;

    // area: 표가 차지할 영역
    // rowCount: 줄 수 / frozenWidth: 고정 열의 너비 / scrollWidth: 스크롤되는 열들의 전체 너비
    // 각 콜백은 "그 부분 안에서의 좌표"를 가진 Rect를 받는다 (x=0, 줄 번호만큼 아래로 내려간 위치)
    public static void Draw(Rect area, ref Vector2 scroll, int rowCount, float frozenWidth, float scrollWidth,
        float rowHeight, float headerHeight,
        Action<Rect> frozenHeader, Action<Rect> scrollHeader,
        Action<Rect, int> frozenRow, Action<Rect, int> scrollRow)
    {
        Rect body = new Rect(area.x + frozenWidth, area.y + headerHeight, area.width - frozenWidth, area.height - headerHeight);
        Rect content = new Rect(0f, 0f, scrollWidth, rowCount * rowHeight);

        bool needV = content.height > body.height;
        bool needH = content.width > body.width - (needV ? ScrollbarSize : 0f);
        float vbar = needV ? ScrollbarSize : 0f;
        float hbar = needH ? ScrollbarSize : 0f;
        float visibleHeight = body.height - hbar;

        // 고정 열 위에서 마우스 휠을 굴려도 세로 스크롤이 되게 한다
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && new Rect(area.x, body.y, frozenWidth, body.height).Contains(e.mousePosition))
        {
            scroll.y += e.delta.y * 20f;
            e.Use();
        }

        // 보이는 줄만 그린다 (줄이 많아도 가볍게)
        int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / rowHeight) - 1);
        int last = Mathf.Min(rowCount - 1, Mathf.CeilToInt((scroll.y + visibleHeight) / rowHeight) + 1);

        // 1) 스크롤되는 본문 (스크롤바 포함)
        scroll = GUI.BeginScrollView(body, scroll, content);
        for (int i = first; i <= last; i++)
        {
            Rect row = new Rect(0f, i * rowHeight, scrollWidth, rowHeight);
            if (i % 2 == 1) EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.03f));
            scrollRow(row, i);
        }
        GUI.EndScrollView();

        // 스크롤 값이 바뀌었을 수 있으니 다시 계산
        first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / rowHeight) - 1);
        last = Mathf.Min(rowCount - 1, Mathf.CeilToInt((scroll.y + visibleHeight) / rowHeight) + 1);

        // 2) 머리글 (오른쪽은 가로 스크롤을 따라 움직이고, 왼쪽은 고정)
        Rect headerRightArea = new Rect(body.x, area.y, body.width - vbar, headerHeight);
        EditorGUI.DrawRect(new Rect(area.x, area.y, area.width, headerHeight), new Color(0f, 0f, 0f, 0.3f));

        GUI.BeginGroup(headerRightArea);
        scrollHeader(new Rect(-scroll.x, 0f, scrollWidth, headerHeight));
        GUI.EndGroup();

        GUI.BeginGroup(new Rect(area.x, area.y, frozenWidth, headerHeight));
        frozenHeader(new Rect(0f, 0f, frozenWidth, headerHeight));
        GUI.EndGroup();

        // 3) 고정 열 본문 (세로 스크롤을 따라 움직임)
        GUI.BeginGroup(new Rect(area.x, body.y, frozenWidth, visibleHeight));
        for (int i = first; i <= last; i++)
        {
            Rect row = new Rect(0f, i * rowHeight - scroll.y, frozenWidth, rowHeight);
            if (i % 2 == 1) EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.03f));
            frozenRow(row, i);
        }
        GUI.EndGroup();

        // 고정 열과 스크롤 영역 사이의 구분선
        EditorGUI.DrawRect(new Rect(area.x + frozenWidth - 1f, area.y, 1f, area.height - hbar), new Color(1f, 1f, 1f, 0.15f));
    }
}
