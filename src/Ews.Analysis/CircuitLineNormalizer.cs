using Ews.Domain.Circuits;

namespace Ews.Analysis;

/// <summary>
/// 回路内容記述(FYDF805)に対する行種別の前処理(正規化・変換)パイプライン。
///
/// 【C原典】toku/qrespo/sekkei/fyskews/src/FyskEwsMain.c の main() が
/// 主回路設計メイン(Fysk10_Main)を呼ぶ前に順次実行していた一連の関数群:
///
///   1. Fysk_CheckDuplicationComma   コンマ重複の除去
///   2. Fysk_LightCirCuitCheck       電灯回路 +(NT) 付与
///   3. Fysk_MPCHeck                 MP行 深さ補完(*15)
///   4. Fysk_TransAfAt               60AF→100AF 変換
///   5. Fysk_2ET_Check               C行 +(2ET) 削除
///   6. Fysk_TM_Consecutive_Check    連続TM行の結合
///   7. Fysk_SM_Consecutive_Check    SM行の直前行への結合
///   8. Fysk_WL_Consecutive_Check    PM行 F1A 変換
///   9. Fysk_BO_below_TM_Check       TM直下BO → TMをMへ
///  10. Fysk_Delete_Comma            括弧直前の不要コンマ削除
///  11. Fysk_Add_LWToMGMC            -MG/-MC へ直上の(LW=…)反映
///  12. Fysk_Chg_TMtoM_BetweenPandSP P-SP間のTMをMへ
///  13. Fysk_Chg_OtoBO_UnderM        P-SP間のM下のOをBOへ
///
/// 旧 C は固定長 CHAR 配列を LibCharBackSpaceCut(末尾空白トリム) /
/// LibCharBackSpaceSet(固定長へ詰め戻し) で出し入れしていたが、本移行では
/// <see cref="CircuitDescriptionLine.CircuitText"/> がトリム済み文字列のため直接操作する。
/// </summary>
public static class CircuitLineNormalizer
{
    /// <summary>
    /// 全変換を C 原典 main() と同一順序で適用する。
    /// </summary>
    public static void Normalize(List<CircuitDescriptionLine> lines)
    {
        CheckDuplicationComma(lines);   // Fysk_CheckDuplicationComma
        AddLightCircuitNt(lines);       // Fysk_LightCirCuitCheck
        CompleteMpDepth(lines);         // Fysk_MPCHeck
        TransformAfAt(lines);           // Fysk_TransAfAt
        RemoveTwoEt(lines);             // Fysk_2ET_Check
        MergeConsecutiveTm(lines);      // Fysk_TM_Consecutive_Check
        MergeConsecutiveSm(lines);      // Fysk_SM_Consecutive_Check
        ConvertWlF1a(lines);            // Fysk_WL_Consecutive_Check
        ChangeTmToMWhenBoFollows(lines);// Fysk_BO_below_TM_Check
        DeleteCommaBeforeParen(lines);  // Fysk_Delete_Comma
        ApplyLwToMgMc(lines);           // Fysk_Add_LWToMGMC
        ChangeTmToMBetweenPAndSp(lines);// Fysk_Chg_TMtoM_BetweenPandSP
        ChangeOToBoUnderM(lines);       // Fysk_Chg_OtoBO_UnderM
    }

    /// <summary>
    /// コンマ重複の除去(",,," → ",")。
    /// 【C原典】Fysk_CheckDuplicationComma。
    /// </summary>
    public static void CheckDuplicationComma(List<CircuitDescriptionLine> lines)
    {
        foreach (CircuitDescriptionLine line in lines)
        {
            string text = line.CircuitText;
            if (!text.Contains(",,"))
            {
                continue;
            }

            var result = new System.Text.StringBuilder(text.Length);
            bool prevComma = false;
            foreach (char c in text)
            {
                if (c == ',' && prevComma)
                {
                    continue; // 連続するコンマは読み飛ばす
                }

                result.Append(c);
                prevComma = c == ',';
            }

            line.CircuitText = result.ToString();
        }
    }

    /// <summary>
    /// 電灯回路(P行に 1P3W)の主幹 MCB/ELB が 500A/600A の場合 +(NT) を付与する。
    /// 【C原典】Fysk_LightCirCuitCheck (改訂&lt;8&gt; の +(...)内付与含む)。
    /// </summary>
    public static void AddLightCircuitNt(List<CircuitDescriptionLine> lines)
    {
        bool mainTrunkIsLightBoard = false;

        foreach (CircuitDescriptionLine line in lines)
        {
            string text = line.CircuitText;

            if (StartsWithLineType(line, "P", 1))
            {
                // 1. 電灯回路か判定 (1P3W を含むか)
                if (!text.Contains("1P3W"))
                {
                    continue;
                }

                mainTrunkIsLightBoard = true;
            }
            else if (StartsWithLineType(line, "M", 1))
            {
                if (!mainTrunkIsLightBoard)
                {
                    continue;
                }

                // 2. 対象の主幹(MCB/ELB)かつ 500A/600A か判定
                int mainStart = IndexOfMainBreaker(text);
                if (mainStart < 0)
                {
                    continue;
                }

                string main = text[mainStart..];
                if (!main.Contains("500A") && !main.Contains("600A"))
                {
                    continue;
                }

                // 3. +(NT) を付与 (既に NT/TLA があれば付与しない)
                if (!main.Contains("NT") && !main.Contains("TLA"))
                {
                    line.CircuitText = AppendNt(text);
                }

                mainTrunkIsLightBoard = false;
            }
        }
    }

    /// <summary>
    /// 【C原典】Fysk_LightCirCuitCheck の +(NT) 付与本体(カンマ以降の分離・+(...)内挿入)。
    /// </summary>
    private static string AppendNt(string text)
    {
        // カンマより後を分離する。
        string afterComma = string.Empty;
        int comma = text.IndexOf(',');
        if (comma >= 0)
        {
            afterComma = text[comma..];
            text = text[..comma];
        }

        int plusParen = text.IndexOf("+(", StringComparison.Ordinal);
        if (plusParen >= 0)
        {
            // +(...) がある場合、) の直前に +NT を挿入する。
            int closeParen = text.IndexOf(')', plusParen);
            if (closeParen < 0)
            {
                return text; // 不整合(C原典は NOGOOD)。ここでは無変換で返す。
            }

            string afterKakko = text[closeParen..];
            text = text[..closeParen] + "+NT" + afterKakko;
        }
        else
        {
            // 末尾に +(NT) を付与する。
            text += "+(NT)";
        }

        return text + afterComma;
    }

    /// <summary>
    /// MP行で '*' が1個かつ末尾が ')' の場合、深さ *15 を補完する。
    /// 【C原典】Fysk_MPCHeck ((SP= x * y) → *15) 補完)。
    /// </summary>
    public static void CompleteMpDepth(List<CircuitDescriptionLine> lines)
    {
        foreach (CircuitDescriptionLine line in lines)
        {
            if (!StartsWithLineType(line, "MP", 2))
            {
                continue;
            }

            string text = line.CircuitText;
            if (CountChar(text, '*') != 1)
            {
                continue;
            }

            // 最初の ')' が末尾(=唯一の閉じ括弧が末尾)の場合のみ補完する。
            int firstClose = text.IndexOf(')');
            if (firstClose >= 0 && firstClose == text.Length - 1)
            {
                line.CircuitText = text[..^1] + "*15)";
            }
        }
    }

    /// <summary>
    /// M行(MP除く)の MCB/ELB で 60AT かつ 60AF の場合、60AF→100AF に変換する。
    /// 【C原典】Fysk_TransAfAt。
    /// </summary>
    public static void TransformAfAt(List<CircuitDescriptionLine> lines)
    {
        foreach (CircuitDescriptionLine line in lines)
        {
            if (!StartsWithLineType(line, "M", 1) || StartsWithLineType(line, "MP", 2))
            {
                continue;
            }

            string text = line.CircuitText;
            if (!text.StartsWith("MCB", StringComparison.Ordinal) &&
                !text.StartsWith("ELB", StringComparison.Ordinal))
            {
                continue;
            }

            if (!text.Contains("60AT") || !text.Contains("60AF"))
            {
                continue;
            }

            line.CircuitText = ReplaceFirst(text, "60AF", "100AF");
        }
    }

    /// <summary>
    /// C行から +(2ET) を削除する。
    /// 【C原典】Fysk_2ET_Check。
    /// </summary>
    public static void RemoveTwoEt(List<CircuitDescriptionLine> lines)
    {
        foreach (CircuitDescriptionLine line in lines)
        {
            if (!StartsWithLineType(line, "C", 1))
            {
                continue;
            }

            if (line.CircuitText.Contains("+(2ET)"))
            {
                line.CircuitText = ReplaceFirst(line.CircuitText, "+(2ET)", string.Empty);
            }
        }
    }

    /// <summary>
    /// 連続する TM 行をコンマで結合し、後続行を削除する。
    /// 【C原典】Fysk_TM_Consecutive_Check。
    /// </summary>
    public static void MergeConsecutiveTm(List<CircuitDescriptionLine> lines)
    {
        for (int i = 0; i < lines.Count - 1; i++)
        {
            if (!ExactLineType(lines[i], "TM") || !ExactLineType(lines[i + 1], "TM"))
            {
                continue;
            }

            lines[i].CircuitText = lines[i].CircuitText + "," + lines[i + 1].CircuitText;
            lines.RemoveAt(i + 1);
            i--; // 同一行で再判定(さらに連続する TM に対応)
        }
    }

    /// <summary>
    /// 直前が TM(または直前で結合済み)の SM 行を直前行へ結合する。
    /// 【C原典】Fysk_SM_Consecutive_Check。
    /// </summary>
    public static void MergeConsecutiveSm(List<CircuitDescriptionLine> lines)
    {
        bool isConsecutive = false;

        for (int i = 1; i < lines.Count; i++)
        {
            if (!ExactLineType(lines[i - 1], "TM") && !isConsecutive)
            {
                isConsecutive = false;
                continue;
            }

            if (!ExactLineType(lines[i], "SM"))
            {
                isConsecutive = false;
                continue;
            }

            // 直前行へ結合し、当該 SM 行を削除する。
            lines[i].CircuitText = lines[i - 1].CircuitText + "," + lines[i].CircuitText;
            lines[i].LineType = lines[i - 1].LineType; // 結合後は直前行の位置を引き継ぐ
            lines.RemoveAt(i - 1);
            i--;
            isConsecutive = true;
        }
    }

    /// <summary>
    /// PM行が F1A で始まる場合、直前行に応じて F1A→F+(ST) または F1A→F に変換する。
    /// 【C原典】Fysk_WL_Consecutive_Check。
    ///   編集パターン1: 直前P行に 420V を含む → F+(ST)
    ///   編集パターン2: 上記以外で直前がP行またはTM行 → F
    /// </summary>
    public static void ConvertWlF1a(List<CircuitDescriptionLine> lines)
    {
        for (int i = 1; i < lines.Count; i++)
        {
            if (!StartsWithLineType(lines[i], "PM", 2))
            {
                continue;
            }

            if (!lines[i].CircuitText.StartsWith("F1A", StringComparison.Ordinal))
            {
                continue;
            }

            int editPattern = 0;
            CircuitDescriptionLine prev = lines[i - 1];

            if (StartsWithLineType(prev, "P", 1))
            {
                editPattern = prev.CircuitText.Contains("420V") ? 1 : 2;
            }
            else if (ExactLineType(prev, "TM"))
            {
                editPattern = 2;
            }

            if (editPattern == 0)
            {
                continue;
            }

            string rest = lines[i].CircuitText[3..]; // "F1A" の3文字を除いた残り
            string head = editPattern == 1 ? "F+(ST)" : "F";
            lines[i].CircuitText = head + rest;
        }
    }

    /// <summary>
    /// TM の直下が BO の場合、TM を M に変更する。
    /// 【C原典】Fysk_BO_below_TM_Check。
    /// </summary>
    public static void ChangeTmToMWhenBoFollows(List<CircuitDescriptionLine> lines)
    {
        for (int i = 0; i < lines.Count - 1; i++)
        {
            if (ExactLineType(lines[i], "TM") && StartsWithLineType(lines[i + 1], "BO", 2))
            {
                lines[i].LineType = "M";
            }
        }
    }

    /// <summary>
    /// 丸括弧 '(' の直前にあるコンマを削除する。
    /// 【C原典】Fysk_Delete_Comma。
    /// </summary>
    public static void DeleteCommaBeforeParen(List<CircuitDescriptionLine> lines)
    {
        foreach (CircuitDescriptionLine line in lines)
        {
            string text = line.CircuitText;
            int comma = text.IndexOf(',');
            if (comma < 0 || comma + 1 >= text.Length)
            {
                continue;
            }

            if (text[comma + 1] != '(')
            {
                continue;
            }

            line.CircuitText = text[..comma] + text[(comma + 1)..];
        }
    }

    /// <summary>
    /// 行種ブランクで -MG/-MC を含み (LW を持たない行に、直上行の (LW=…) を反映する。
    /// 【C原典】Fysk_Add_LWToMGMC (改訂&lt;6&gt;)。
    /// </summary>
    public static void ApplyLwToMgMc(List<CircuitDescriptionLine> lines)
    {
        for (int i = 1; i < lines.Count; i++)
        {
            CircuitDescriptionLine line = lines[i];
            string text = line.CircuitText;

            bool isBlankType = line.LineType.Length == 0;
            bool hasMgMc = text.Contains("-MG") || text.Contains("-MC");
            bool hasLw = text.Contains("(LW");

            if (!isBlankType || !hasMgMc || hasLw)
            {
                continue;
            }

            string prev = lines[i - 1].CircuitText;
            int lwStart = prev.IndexOf("(LW", StringComparison.Ordinal);
            if (lwStart < 0)
            {
                continue;
            }

            int lwClose = prev.IndexOf(')', lwStart);
            if (lwClose < 0)
            {
                continue;
            }

            // (LW … ) を切り出して付与する。
            string lwExpr = prev.Substring(lwStart, lwClose - lwStart + 1);
            line.CircuitText = text + lwExpr;
        }
    }

    /// <summary>
    /// 行種 P と SP の間に TM があり、かつ M が無い場合、TM を M に変更する。
    /// 【C原典】Fysk_Chg_TMtoM_BetweenPandSP (改訂&lt;7&gt;)。
    /// </summary>
    public static void ChangeTmToMBetweenPAndSp(List<CircuitDescriptionLine> lines)
    {
        bool existP = false;
        bool existM = false;
        CircuitDescriptionLine? tmLine = null;

        foreach (CircuitDescriptionLine line in lines)
        {
            if (ExactLineType(line, "P"))
            {
                existP = true;
                existM = false;
                tmLine = null;
            }
            else if (ExactLineType(line, "TM"))
            {
                tmLine = line;
            }
            else if (ExactLineType(line, "M"))
            {
                existM = true;
            }
            else if (ExactLineType(line, "SP"))
            {
                if (existP && !existM && tmLine is not null)
                {
                    tmLine.LineType = "M";
                }

                existP = false;
            }
        }
    }

    /// <summary>
    /// 行種 P と SP の間に M があり、その下に O がある場合、O を BO に変更する。
    /// 【C原典】Fysk_Chg_OtoBO_UnderM (改訂&lt;7&gt;)。
    /// </summary>
    public static void ChangeOToBoUnderM(List<CircuitDescriptionLine> lines)
    {
        int pIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            if (ExactLineType(lines[i], "P"))
            {
                pIndex = i;
            }
            else if (ExactLineType(lines[i], "SP"))
            {
                if (pIndex == -1)
                {
                    continue;
                }

                bool existM = false;
                for (int j = pIndex + 1; j < i; j++)
                {
                    if (ExactLineType(lines[j], "M"))
                    {
                        existM = true;
                    }
                    else if (ExactLineType(lines[j], "O") && existM)
                    {
                        lines[j].LineType = "BO";
                    }
                }

                pIndex = -1;
            }
        }
    }

    // ---- ヘルパ ----

    /// <summary>
    /// 行種の前方一致判定。【C原典】strncmp(imagea[i].gyosyu, prefix, length) == 0。
    /// </summary>
    private static bool StartsWithLineType(CircuitDescriptionLine line, string prefix, int length)
        => line.LineType.Length >= length && line.LineType.AsSpan(0, length).SequenceEqual(prefix.AsSpan(0, length));

    /// <summary>
    /// 行種の完全一致判定。【C原典】固定長5バイトの "P    " 等との strncmp。
    /// (CircuitDescriptionLine.LineType はトリム済みのため厳密一致で表現)。
    /// </summary>
    private static bool ExactLineType(CircuitDescriptionLine line, string value)
        => line.LineType == value;

    /// <summary>主幹ブレーカ(MCB/ELB)の出現位置。無ければ -1。</summary>
    private static int IndexOfMainBreaker(string text)
    {
        int mcb = text.IndexOf("MCB", StringComparison.Ordinal);
        if (mcb >= 0)
        {
            return mcb;
        }

        return text.IndexOf("ELB", StringComparison.Ordinal);
    }

    private static int CountChar(string text, char target)
    {
        int count = 0;
        foreach (char c in text)
        {
            if (c == target)
            {
                count++;
            }
        }

        return count;
    }

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        int index = text.IndexOf(oldValue, StringComparison.Ordinal);
        return index < 0 ? text : text[..index] + newValue + text[(index + oldValue.Length)..];
    }
}
