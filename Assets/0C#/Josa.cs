// 한국어 조사 도우미: 단어의 마지막 글자에 받침이 있는지에 따라 이/가, 을/를, 은/는을 골라 준다.
// 예) Josa.Iga("돌도끼") -> "가", Josa.Iga("낫") -> "이", Josa.WithEul("딸기") -> "딸기를"
public static class Josa
{
    // 받침이 있으면 true (한글이 아니면 받침 없음으로 본다)
    public static bool HasBatchim(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        char last = word[word.Length - 1];
        if (last >= 0xAC00 && last <= 0xD7A3) return (last - 0xAC00) % 28 != 0;
        return false;
    }

    public static string Pick(string word, string withBatchim, string without)
    {
        return HasBatchim(word) ? withBatchim : without;
    }

    public static string Iga(string word) { return Pick(word, "이", "가"); }
    public static string Eul(string word) { return Pick(word, "을", "를"); }
    public static string Eun(string word) { return Pick(word, "은", "는"); }

    public static string WithIga(string word) { return word + Iga(word); }
    public static string WithEul(string word) { return word + Eul(word); }
    public static string WithEun(string word) { return word + Eun(word); }
}
