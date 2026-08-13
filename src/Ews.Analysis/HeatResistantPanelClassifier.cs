using Ews.Domain.Analysis;
using Ews.Domain.Circuits;

namespace Ews.Analysis;

/// <summary>
/// 耐熱盤の分類チェック。自由文字(回路内容記述 FYDF805)からコンスタント
/// (tainetuPT.cns)を突合し、系統ごとに分類を決定する。
///
/// 【C原典】Fysk01_Chk_TainetuBunrui(toku/sekkei/src/Fysk01.c:6615, 改訂&lt;13&gt;)。
///   自由文字から分類を決定し系統ごとに保持する。1系統に最大1つの分類とし、
///   2つ以上ある場合は分類なしとする。
///   - 行種"P  "で相数(P直前の1文字)/線数(W直前の1文字)を求め系統番号を更新。
///     同時に前系統の判定結果(該当1件のみ)を確定する。
///   - 自由文字に"F1+BOX"を含む行を比較対象とし、先頭空白までで切詰めた文字列を
///     コンスタント(行番号0=1行一致/1=次行と2行一致)と突合する。
/// </summary>
public static class HeatResistantPanelClassifier
{
    /// <summary>【C原典】#define KAIROARLEN 200(回路内容記述エリア長)。</summary>
    private const int CircuitTextLength = 200;

    private const string MarkerText = "F1+BOX";

    /// <summary>
    /// 自由文字から耐熱盤分類を系統単位で判定する。
    /// 【C原典】Fysk01_Chk_TainetuBunrui(tprm, cnum, cprm, imagec, imagea)。
    /// </summary>
    /// <param name="constants">コンスタント(taiPT_prm [])。【C原典】cprm・cnum=件数。</param>
    /// <param name="freeText">自由文字エリア(FYDF805 [])。【C原典】imagea・imagec=件数。</param>
    /// <returns>系統別分類データ(taiPT_tmp [])。該当なしは空リスト。【C原典】戻り値 tnum=件数。</returns>
    public static List<HeatResistantPanelClassificationResult> Classify(
        IReadOnlyList<HeatResistantPanelClassificationConstant> constants,
        IReadOnlyList<CircuitDescriptionLine> freeText)
    {
        ArgumentNullException.ThrowIfNull(constants);
        ArgumentNullException.ThrowIfNull(freeText);

        var results = new List<HeatResistantPanelClassificationResult>();
        int kno = 0;        // 系統番号
        int knoData = 0;    // 系統別分類データ数
        int sou = 0;        // 相数
        int sen = 0;        // 線数
        HeatResistantPanelClassificationResult? pending = null; // 判定結果ワーク(tprm_tmp)

        for (int i = 0; i < freeText.Count; i++)
        {
            CircuitDescriptionLine line = freeText[i];
            if (line.Command == 'D')
            {
                continue;
            }
            if (LineTypeIs(line, "#  "))
            {
                continue;
            }
            if (LineTypeIs(line, "END"))
            {
                break;
            }

            // 行種Pから相線を求める
            if (LineTypeIs(line, "P  "))
            {
                string circuit = Take(line.CircuitText, CircuitTextLength);
                int pIndex = circuit.IndexOf('P');
                if (pIndex >= 0)
                {
                    kno++;
                    sou = Atoi(CharBefore(circuit, pIndex));
                    int wIndex = circuit.IndexOf('W');
                    sen = wIndex >= 0 ? Atoi(CharBefore(circuit, wIndex)) : 0;
                }

                // 前系統の耐熱盤判定データを格納する(該当2件以上・0件は判定不可)。
                // 【C原典】判定不可(または未該当)時は knoData をリセットしない(原典どおり)。
                if (knoData >= 2 || knoData == 0)
                {
                    continue;
                }
                results.Add(pending!);
                knoData = 0;
                continue;
            }

            // 自由文字内に(F1+BOX)の記述がある行を比較対象とする
            string work = Take(line.CircuitText, CircuitTextLength);
            if (!work.Contains(MarkerText, StringComparison.Ordinal))
            {
                continue;
            }
            work = TrimAtFirstSpace(work);

            // 対象行とコンスタントファイルのデータを比較する
            for (int n = 0; n < constants.Count; n++)
            {
                if (constants[n].LineNumber >= 2)
                {
                    continue;
                }
                if (!string.Equals(work, constants[n].FreeText, StringComparison.Ordinal))
                {
                    continue;
                }

                // コンスタントの行番号が0ならここで一致とする
                if (constants[n].LineNumber == 0)
                {
                    pending = new HeatResistantPanelClassificationResult(kno, sou, sen, constants[n].Category);
                    knoData++;
                    break;
                }

                // コンスタントの行番号が1なら2行目の比較を行う
                if (constants[n].LineNumber == 1)
                {
                    string work2 = string.Empty;
                    for (int m = i + 1; m < freeText.Count; m++)
                    {
                        CircuitDescriptionLine next = freeText[m];
                        if (next.Command == 'D')
                        {
                            continue;
                        }
                        if (LineTypeIs(next, "#  "))
                        {
                            continue;
                        }
                        work2 = TrimAtFirstSpace(Take(next.CircuitText, CircuitTextLength));
                        break;
                    }

                    // 2行目比較(次コンスタントの行番号が2かつ自由文字一致)
                    if (n + 1 < constants.Count
                        && constants[n + 1].LineNumber == 2
                        && string.Equals(work2, constants[n + 1].FreeText, StringComparison.Ordinal))
                    {
                        pending = new HeatResistantPanelClassificationResult(kno, sou, sen, constants[n].Category);
                        knoData++;
                        break;
                    }
                }
            }
        }

        // 最後の系統の耐熱盤判定データを格納する(該当1件のみ)
        if (knoData == 1)
        {
            results.Add(pending!);
        }

        return results;
    }

    /// <summary>行種(gyosyu)の先頭を固定幅正規化して照合する。【C原典】strncmp(gyosyu, pattern, len)。</summary>
    private static bool LineTypeIs(CircuitDescriptionLine line, string pattern)
        => Take(line.LineType, pattern.Length) == pattern;

    /// <summary>先頭空白までで切り詰める。【C原典】work[n]==' ' で終端。</summary>
    private static string TrimAtFirstSpace(string value)
    {
        int index = value.IndexOf(' ');
        return index >= 0 ? value[..index] : value;
    }

    /// <summary>指定位置の直前の1文字。【C原典】*(strp-1)。先頭時は NUL 相当。</summary>
    private static char CharBefore(string value, int index)
        => index > 0 ? value[index - 1] : '\0';

    /// <summary>1文字を数値化する。【C原典】atoi(work)(work は1文字+NUL)。非数字は0。</summary>
    private static int Atoi(char c) => c is >= '0' and <= '9' ? c - '0' : 0;

    /// <summary>固定幅へ右空白パディングし先頭 width 文字を取る。【C原典】固定長 CHAR[width]。</summary>
    private static string Take(string value, int width)
        => (value ?? string.Empty).PadRight(width)[..width];
}
