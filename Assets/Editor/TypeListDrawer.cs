using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 데이터 표의 [타입] 탭: 게임에서 쓰는 종류(enum) 목록을 한곳에 모아 보여 주고,
// 설명(주석)을 고치거나 새 종류를 맨 뒤에 추가한다. 추가/수정은 해당 .cs 파일에 바로 기록되고 Unity가 다시 컴파일한다.
//
// 주의: 이름 바꾸기와 순서 바꾸기는 지원하지 않는다 (저장된 에셋이 번호로 종류를 기억하기 때문). 새 종류는 항상 맨 뒤에 붙는다.
// 새 종류를 만들어도 그 종류로 동작하는 코드는 따로 있어야 한다 (예: 새 ToolType을 쓰는 채집 규칙).
public static class TypeListDrawer
{
    private class Member
    {
        public string name;
        public long value;
        public string comment;   // 주석 글(// 뒤), 없으면 ""
        public string edit;      // 입력칸에 쓰는 값
    }

    private class EnumInfo
    {
        public string label;
        public string typeName;
        public string file;      // 정의된 .cs 파일 경로
        public List<Member> members = new List<Member>();
        public string newName = "", newComment = "";
    }

    // 보여 줄 종류 목록 (표시 이름, enum 형식 이름)
    private static readonly string[][] Targets =
    {
        new[] { "장착 부위 (Weapon, Body ...)", "EquipSlot" },
        new[] { "도구 종류 (Axe, Pickaxe ...)", "ToolType" },
        new[] { "숙련도 종류 (Logging, Gather ...)", "ProficiencyKind" },
        new[] { "SP 소모 행동 (Search, Gather ...)", "SPAction" },
        new[] { "효과음 (PlayerAttack ...)", "SoundEvent" },
        new[] { "채집 소리 (Bush, Logging ...)", "GatherSoundType" },
        new[] { "탐색 결과 (Resource, Event ...)", "SearchOutcomeKind" },
        new[] { "필드 버튼 (Search, Gather ...)", "FieldButtonType" },
    };

    private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");
    private static List<EnumInfo> infos;
    private static string message = "";

    public static void Refresh()
    {
        infos = new List<EnumInfo>();
        string[] files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        foreach (string[] t in Targets)
        {
            EnumInfo info = new EnumInfo { label = t[0], typeName = t[1] };
            Regex head = new Regex(@"\benum\s+" + t[1] + @"\b");
            foreach (string f in files)
            {
                string text = Latin1.GetString(File.ReadAllBytes(f));
                if (head.IsMatch(text)) { info.file = f; break; }
            }
            if (info.file != null) Parse(info);
            infos.Add(info);
        }
    }

    // enum 본문의 범위를 찾는다
    private static bool Locate(string text, string enumName, out int bodyStart, out int bodyEnd)
    {
        bodyStart = bodyEnd = -1;
        Match m = Regex.Match(text, @"\benum\s+" + enumName + @"\b[^{]*\{");
        if (!m.Success) return false;
        bodyStart = m.Index + m.Length;
        bodyEnd = text.IndexOf('}', bodyStart);
        return bodyEnd > bodyStart;
    }

    private static void Parse(EnumInfo info)
    {
        string text = Latin1.GetString(File.ReadAllBytes(info.file));
        if (!Locate(text, info.typeName, out int s, out int e)) return;
        long next = 0;
        foreach (string raw in text.Substring(s, e - s).Split('\n'))
        {
            Match m = Regex.Match(raw.Trim(), @"^(\w+)\s*(?:=\s*(-?\d+))?\s*,?\s*(?://(.*))?$");
            if (!m.Success) continue;
            long v = m.Groups[2].Success ? long.Parse(m.Groups[2].Value) : next;
            next = v + 1;
            string c = m.Groups[3].Success ? ToText(m.Groups[3].Value.Trim()) : "";
            info.members.Add(new Member { name = m.Groups[1].Value, value = v, comment = c, edit = c });
        }
    }

    // 파일 안의 CP949 글자 바이트(Latin1로 읽은 것) <-> 보이는 글자
    private static string ToText(string latin1)
    {
        try { return Encoding.GetEncoding(949).GetString(Latin1.GetBytes(latin1)); } catch { return latin1; }
    }

    private static string FromText(string text)
    {
        byte[] bytes;
        try { bytes = Encoding.GetEncoding(949).GetBytes(text); } catch { bytes = Encoding.ASCII.GetBytes(text); }
        return Latin1.GetString(bytes);
    }

    public static void Draw(ref Vector2 scroll)
    {
        if (infos == null) Refresh();

        EditorGUILayout.HelpBox(
            "게임에서 쓰는 종류 목록입니다. 설명은 고치면 [적용]으로 코드 파일에 기록되고, 새 종류는 맨 뒤에 추가됩니다.\n" +
            "이름/순서 바꾸기는 지원하지 않습니다 (저장된 아이템/표가 번호로 기억하기 때문). 기록하면 Unity가 다시 컴파일합니다.",
            MessageType.Info);
        if (message.Length > 0) EditorGUILayout.HelpBox(message, MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (EnumInfo info in infos)
        {
            EditorGUILayout.Space(6);
            string fileName = info.file != null ? Path.GetFileName(info.file) : "파일을 못 찾음";
            EditorGUILayout.LabelField(info.label + "   [" + fileName + "]", EditorStyles.boldLabel);
            if (info.file == null) continue;

            foreach (Member m in info.members)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(m.value.ToString(), GUILayout.Width(28));
                EditorGUILayout.LabelField(m.name, GUILayout.Width(150));
                m.edit = EditorGUILayout.TextField(m.edit);
                using (new EditorGUI.DisabledScope(m.edit == m.comment))
                {
                    if (GUILayout.Button("적용", GUILayout.Width(46))) { SetComment(info, m); GUIUtility.ExitGUI(); }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("+ 추가", GUILayout.Width(60));
            info.newName = EditorGUILayout.TextField(info.newName, GUILayout.Width(150));
            info.newComment = EditorGUILayout.TextField(info.newComment);
            bool valid = Regex.IsMatch(info.newName, @"^[A-Za-z_]\w*$") && info.members.All(x => x.name != info.newName);
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("추가", GUILayout.Width(46))) { AddMember(info); GUIUtility.ExitGUI(); }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private static void SetComment(EnumInfo info, Member m)
    {
        string text = Latin1.GetString(File.ReadAllBytes(info.file));
        if (!Locate(text, info.typeName, out int s, out int e)) return;
        string[] lines = text.Substring(s, e - s).Split('\n');
        Regex rx = new Regex(@"^(\s*" + m.name + @"\b[^/\r]*?)\s*(//.*?)?(\r?)$");
        for (int i = 0; i < lines.Length; i++)
        {
            Match r = rx.Match(lines[i]);
            if (!r.Success) continue;
            string add = m.edit.Trim().Length > 0 ? "  // " + FromText(m.edit.Trim()) : "";
            lines[i] = r.Groups[1].Value.TrimEnd() + add + r.Groups[3].Value;
            break;
        }
        Write(info, text.Substring(0, s) + string.Join("\n", lines) + text.Substring(e), m.name + " 설명을 고쳤습니다.");
    }

    private static void AddMember(EnumInfo info)
    {
        string text = Latin1.GetString(File.ReadAllBytes(info.file));
        if (!Locate(text, info.typeName, out int s, out int e)) return;
        bool crlf = text.Contains("\r\n");
        string[] lines = text.Substring(s, e - s).Split('\n');

        // 마지막 멤버 줄 찾기
        Regex rx = new Regex(@"^(\s*)(\w+)(\s*=\s*-?\d+)?(\s*,)?(\s*)(//.*?)?(\r?)$");
        int last = -1;
        for (int i = 0; i < lines.Length; i++)
            if (rx.IsMatch(lines[i]) && lines[i].Trim().Length > 0 && !lines[i].Trim().StartsWith("//")) last = i;
        if (last < 0) { message = "마지막 멤버를 찾지 못했습니다."; return; }

        Match r = rx.Match(lines[last]);
        string indent = r.Groups[1].Value;
        if (!r.Groups[4].Success) // 쉼표가 없으면 붙인다
            lines[last] = indent + r.Groups[2].Value + r.Groups[3].Value + "," + r.Groups[5].Value + r.Groups[6].Value + r.Groups[7].Value;
        string cm = info.newComment.Trim().Length > 0 ? "  // " + FromText(info.newComment.Trim()) : "";
        List<string> list = lines.ToList();
        list.Insert(last + 1, indent + info.newName + "," + cm + (crlf ? "\r" : ""));
        string added = info.newName;
        info.newName = info.newComment = "";
        Write(info, text.Substring(0, s) + string.Join("\n", list) + text.Substring(e), added + " 을(를) 맨 뒤에 추가했습니다.");
    }

    private static void Write(EnumInfo info, string text, string done)
    {
        File.WriteAllBytes(info.file, Latin1.GetBytes(text));
        message = done + " (컴파일이 끝나면 목록이 새로고침됩니다)";
        infos = null;
        AssetDatabase.Refresh();
    }
}
