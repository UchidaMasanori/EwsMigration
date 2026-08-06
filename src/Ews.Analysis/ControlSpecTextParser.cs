namespace Ews.Analysis;

/// <summary>
/// 制御仕様記述テキストのパーサ群(制御回路サブシステムの最下位文字列処理リーフ)。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>SpaceNeguri</c> / <c>SetBtwnData</c> /
/// <c>GetBtwnData</c> / <c>GetAtCharData</c> / <c>GetIntrData</c> /
/// <c>GetSgkkYoyaku</c> / <c>GetSgkkKosu</c>。
///
/// 制御機器の記述文字列(予約語・個数・インターロック等)を組み立てる際の下位文字列処理。
/// 上位の制御仕様テーブル作成(FySgCheckSgkkSet)/構造体(FYRT820 等)は未移植のため、
/// 構造体非依存の純粋文字列関数のみを先行移植する。
/// C の副作用(入力文字列から取り出し部を除去する)は <c>ref string</c> で表現する。
/// </summary>
public static class ControlSpecTextParser
{
    /// <summary>
    /// 文字列の半角スペースを詰める。【C原典】SpaceNeguri(Fyss1k.c:800)。
    /// </summary>
    public static string SpaceNeguri(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return string.Empty;
        }

        // 【C原典】for(i,j){ if(' '!=data[i]) data[j++]=data[i]; }。半角スペースのみ除去。
        var sb = new System.Text.StringBuilder(data.Length);
        foreach (char c in data)
        {
            if (c != ' ')
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// start で始まり end で終わる部分文字列を(start/end を含めて)取り出す。入力は変更しない。
    /// 【C原典】SetBtwnData(Fyss1k.c:1054)。
    /// </summary>
    /// <returns>0:正常、1:start が無い、2:start はあるが end が無い。</returns>
    public static int SetBtwnData(string? input, out string output, string start, string end)
    {
        output = string.Empty;
        string s = input ?? string.Empty;

        int ps = s.IndexOf(start, StringComparison.Ordinal);
        if (ps < 0)
        {
            return 1;
        }

        // 【C原典】strstr(ptrs, end)。start 位置以降で end を探す。
        int pe = s.IndexOf(end, ps, StringComparison.Ordinal);
        if (pe < 0)
        {
            return 2;
        }

        pe += end.Length;
        output = s.Substring(ps, pe - ps);
        return 0;
    }

    /// <summary>
    /// start と end の間(start/end を含まず)を取り出し、入力から該当区間(start～end)を除去する。
    /// 【C原典】GetBtwnData(Fyss1k.c:891)。
    /// </summary>
    /// <returns>0:正常、1:start が無い又は取り出しが空、2:start はあるが end が無い。</returns>
    public static int GetBtwnData(ref string input, out string output, string start, string end)
    {
        output = string.Empty;
        string s = input ?? string.Empty;

        int ps = s.IndexOf(start, StringComparison.Ordinal);
        if (ps < 0)
        {
            return 1;
        }

        int cs = ps + start.Length;
        int ce = s.IndexOf(end, cs, StringComparison.Ordinal);
        if (ce < 0)
        {
            return 2;
        }

        int pe = ce + end.Length;
        output = s.Substring(cs, ce - cs);

        // 【C原典】memcpy(ptrs, ptre, ...)。start 位置～end 終端を詰めて除去する。
        input = s.Remove(ps, pe - ps);

        if (output.Length == 0)
        {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// 先頭から end 文字まで(end を含まず)を取り出し、入力から先頭～end を除去する。
    /// 【C原典】GetAtCharData(Fyss1k.c:936)。end が無ければ入力全体を取り出す(入力は不変)。
    /// </summary>
    /// <returns>0:正常、1:end が無い又は取り出しが空。</returns>
    public static int GetAtCharData(ref string input, out string output, char end)
    {
        string s = input ?? string.Empty;
        int ret = 0;

        int p = s.IndexOf(end);
        if (p < 0)
        {
            // 【C原典】strcpy(out, in)。in は変更しない。
            output = s;
            ret = 1;
        }
        else
        {
            output = s.Substring(0, p);
            input = s.Substring(p + 1);
        }

        if (output.Length == 0)
        {
            ret = 1;
        }
        return ret;
    }

    /// <summary>
    /// インターロック文字列('&lt;' 以降、括弧の外の ',' 又は末尾まで)を取り出し、入力から除去する。
    /// 【C原典】GetIntrData(Fyss1k.c:967)。
    /// </summary>
    public static string GetIntrData(ref string input)
    {
        string s = input ?? string.Empty;

        int start = s.IndexOf('<');
        if (start < 0)
        {
            // 【C原典】strchr が NULL なら未定義動作。呼出前提は '<' 有りだが防御的に空を返す。
            return string.Empty;
        }

        string ptr = s.Substring(start);
        int len = ptr.Length;
        int size = 0, lk = 0, rk = 0;

        while (true)
        {
            // 【C原典】strcspn(&ptr[size], "(,)")。区切り "(),"" まで進める。
            size += CountUntilAny(ptr, size, "(,)");

            if (size < len)
            {
                char c = ptr[size];
                if (c == '(')
                {
                    lk++;
                }
                else if (c == ')')
                {
                    rk++;
                }
                else if (c == ',' && lk == rk)
                {
                    break;   // 括弧の外の ',' で終了
                }
            }

            if (size >= len)
            {
                break;
            }
            size++;
        }

        if (size > len)
        {
            size = len;
        }

        string output = ptr.Substring(0, size);
        // 【C原典】memcpy(ptr, &ptr[size], ...)。'<' 位置から size 文字を除去。
        input = s.Substring(0, start) + s.Substring(start + size);
        return output;
    }

    /// <summary>
    /// 文字列の先頭から予約語を取り出す(入力は変更しない)。
    /// 【C原典】GetSgkkYoyaku(Fyss1k.c:822)。"PT" で始まり ")" で終わる部分があればそれを、
    /// 無ければ先頭からアルファベットが途切れるまでを取り出す。
    /// </summary>
    public static string GetSgkkYoyaku(string? input)
    {
        string s = input ?? string.Empty;

        // 【C原典】kakko[]={"PT",")"} を SetBtwnData で試す。
        if (SetBtwnData(s, out string bracket, "PT", ")") == 0)
        {
            return bracket;
        }

        // 【C原典】while: isalpha が途切れるまで。
        int i = 0;
        while (i < s.Length && IsAlpha(s[i]))
        {
            i++;
        }
        return s.Substring(0, i);
    }

    /// <summary>
    /// 文字列中の '*' の後ろの数字を機器個数として取り出す。'*' が無ければ 1。
    /// 【C原典】GetSgkkKosu(Fyss1k.c:854)。
    /// </summary>
    public static int GetSgkkKosu(string? input)
    {
        string s = input ?? string.Empty;

        int star = s.IndexOf('*');
        if (star < 0)
        {
            return 1;
        }

        // 【C原典】atoi(*の後の文字列)。atoi は先頭の数字列のみを解釈する。
        string after = s.Substring(star + 1);
        return EquipmentParameterFormatter.Stoi(after, after.Length);
    }

    /// <summary>【C原典】strcspn(&amp;s[from], set) 相当。set 内の文字に達するまでの文字数。</summary>
    private static int CountUntilAny(string s, int from, string set)
    {
        int i = from;
        while (i < s.Length && set.IndexOf(s[i]) < 0)
        {
            i++;
        }
        return i - from;
    }

    /// <summary>【C原典】isalpha 相当(ASCII 英字)。</summary>
    private static bool IsAlpha(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
}
