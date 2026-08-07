namespace Ews.Analysis;

using System;
using System.Collections.Generic;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;

/// <summary>
/// 制御仕様文字列から制御対象機器(SGTKK)/内部・外部制御機器(SGKK)のテーブルを構築する。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>GetSgData</c>(改訂&lt;21&gt;)。
///
/// 制御仕様文字列(FYRT820.Pcstrg)からインターロック機器・制御対象機器・内部/外部制御機器を
/// 取り出してそれぞれのテーブルへ set し、パターン番号(ptn_no)/インターロック指定フラグを確定する。
/// C 原典はグローバル <c>Sgtkk[30]</c>/<c>Sgkk[50]</c>/<c>ptn_no</c>/<c>intr_flag</c> を更新するが、
/// 本移行では結果集約 <see cref="ControlSpecData"/> を返す。既移植の下位リーフ
/// (<see cref="ControlReservedWordClassifier.CheckInterlock"/>=ChkInterp,
///  <see cref="ControlSpecTextParser"/>=GetIntrData/GetAtCharData/SpaceNeguri,
///  <see cref="ControlSpecPatternChecker"/>=PropChkINVPtn/PropChkKiKiPtn,
///  <see cref="CircuitAreaLineReader.GetCircuitAreaText"/>=Fysk11_FYDF805_GyoGet,
///  <see cref="CompoSpaceCutter.CutSpaces"/>=FyCpSpcutr,
///  <see cref="ControlInterlockKeyBuilder.AppendInterlockKeys"/>=setInterlockToSkey)を合成する。
/// </summary>
public static class ControlSpecDataBuilder
{
    // 【C原典】static CHAR *er[] = { "G","G1","G2","G3","G4","GI","GP","GPN", NULL };
    private static readonly string[] LiquidLevelReservedWords =
    {
        "G", "G1", "G2", "G3", "G4", "GI", "GP", "GPN",
    };

    /// <summary>
    /// 制御仕様データ(制御対象機器/内部・外部制御機器)を構築する。
    /// 【C原典】<c>GetSgData(SgsTbl, PCkikic, CKikiTabl, gyou, keta, gyogp)</c>。
    /// </summary>
    /// <param name="controlSpec">制御仕様テーブル。【C原典】SgsTbl(Pcstrg/cnameno を使用)。</param>
    /// <param name="controlEquipmentTable">制御機器テーブル。【C原典】CKikiTabl[0..PCkikic)。</param>
    /// <param name="descriptionRow">制御仕様文字列 記述行。【C原典】gyou。</param>
    /// <param name="descriptionColumn">制御仕様文字列 記述桁。【C原典】keta。</param>
    /// <param name="lineTypeGroup">行種グループNo。【C原典】gyogp。</param>
    /// <param name="circuitLines">回路内容記述レコード群(FYDF805)。【C原典】f805(THR/AL 判定に使用)。</param>
    /// <returns>構築した制御仕様データ。</returns>
    public static ControlSpecData BuildSgData(
        ControlSpecEntry controlSpec,
        IReadOnlyList<EquipmentTableEntry> controlEquipmentTable,
        short descriptionRow,
        short descriptionColumn,
        short lineTypeGroup,
        IReadOnlyList<CircuitDescriptionLine> circuitLines)
    {
        ArgumentNullException.ThrowIfNull(controlSpec);
        ArgumentNullException.ThrowIfNull(controlEquipmentTable);
        ArgumentNullException.ThrowIfNull(circuitLines);

        var result = new ControlSpecData();
        List<ControlTargetEntry> targets = result.ControlTargets;         // Sgtkk
        List<ControlEquipmentEntry> equipment = result.ControlEquipment;  // Sgkk

        // 【C原典】strcpy(strg, SgsTbl->Pcstrg); ptn_no=1; intr_flag=0;
        string strg = controlSpec.RawText ?? string.Empty;
        short patternNumber = 1;
        bool interlockFlag = false;

        // 【C原典】ChkInterp(strg)!=0 でインターロック指定有り → GetIntrData で strg から除去。
        if (ControlReservedWordClassifier.CheckInterlock(strg) != 0)
        {
            interlockFlag = true;
            _ = ControlSpecTextParser.GetIntrData(ref strg);
        }

        // 【C原典】GetAtCharData(strg, buf, ':') で制御対象機器の文字列を取得(ret==0 なら有り)。
        short keta = descriptionColumn;
        string targetBuf = strg;
        int ret = ControlSpecTextParser.GetAtCharData(ref targetBuf, out string equipmentList, ':');
        if (ret == 0)
        {
            while (true)
            {
                // 【C原典】GetAtCharData(buf, work, ',') で制御対象機器を1件取り出す。
                ret = ControlSpecTextParser.GetAtCharData(ref equipmentList, out string work, ',');

                var entry = new ControlTargetEntry
                {
                    DescriptionRow = descriptionRow,   // 【C原典】K_Gyo
                    DescriptionColumn = keta,          // 【C原典】K_Ket
                    GroupNumber = lineTypeGroup,       // 【C原典】G_No
                };
                keta += (short)(work.Length + 1);      // 【C原典】keta += strlen(work)+1

                // 【C原典】SpaceNeguri(work) で半角スペースを詰める。
                work = ControlSpecTextParser.SpaceNeguri(work);

                // 【C原典】改訂<19>: MGSH は MG として扱う。
                if (work.StartsWith("MGSH", StringComparison.Ordinal))
                {
                    work = "MG" + work.Substring(4);
                }

                // 【C原典】予約語(先頭アルファベット)と予約語番号(以降)に分ける。
                int split = 0;
                while (split < work.Length && IsAlpha(work[split]))
                {
                    split++;
                }
                entry.ReservedWord = work.Substring(0, split);
                string yno = work.Substring(split);

                // 【C原典】番号(数字部)とサフィックスに分ける。
                int digit = 0;
                while (digit < yno.Length && IsDigit(yno[digit]))
                {
                    digit++;
                }
                string suffix = yno.Substring(digit);
                if (suffix.Length == 0)
                {
                    suffix = " ";      // 【C原典】yssfx[0]=' '
                }
                entry.Suffix = suffix;
                // 【C原典】sprintf(yno, "%02d", atoi(numeric))。数字部を2桁整形。
                entry.ReservedWordNumber = AtoiC(yno.Substring(0, digit)).ToString("D2");

                targets.Add(entry);
                if (ret != 0)
                {
                    break;
                }
            }
        }
        // 【C原典】SgtCnt = targets.Count

        // 【C原典】改訂<13>: INVパターンチェック。
        int inv = ControlSpecPatternChecker.CheckInvPattern(controlSpec.RawText);
        if (inv != 0)
        {
            patternNumber = (short)inv;
        }

        // 【C原典】制御機器テーブルを走査して PTN/内部/外部制御機器を構築。
        for (int i = 0; i < controlEquipmentTable.Count; i++)
        {
            EquipmentTableEntry kiki = controlEquipmentTable[i];

            // 【C原典】制御回路仕様名称追番が同じデータが処理対象。
            if (controlSpec.SpecNameSequence != kiki.Rank)
            {
                continue;
            }

            // 【C原典】液面リレーで用途の指定が有る場合、用途により PTN を設定。
            for (int j = 0; j < LiquidLevelReservedWords.Length; j++)
            {
                if (!string.Equals(LiquidLevelReservedWords[j], kiki.ReservedWord, StringComparison.Ordinal))
                {
                    continue;
                }
                for (int k = 0; k < 7; k++)
                {
                    // 【C原典】改訂<11>: PropChkKiKiPtn が特定パターンなら PTN を変更しない。
                    if (ControlSpecPatternChecker.CheckEquipmentPattern(
                            LiquidLevelReservedWords[j], controlEquipmentTable, kiki.DType[k], lineTypeGroup) != 0)
                    {
                        continue;
                    }

                    string usage = kiki.DType[k];
                    if (usage == "YOU")
                    {
                        patternNumber = 3;
                    }
                    else if (usage == "HAI")
                    {
                        patternNumber = 2;
                    }
                    else if (usage == "KUU")
                    {
                        patternNumber = 4;
                    }
                    else if (usage == "MAN")
                    {
                        patternNumber = 2;
                    }
                    else if (usage == "GEN")
                    {
                        patternNumber = 3;
                    }
                }
                break;
            }

            if (string.Equals(kiki.ReservedWord, "PTN", StringComparison.Ordinal))
            {
                // 【C原典】PTN指定の場合、ptn_no = atoi(DIT)。
                patternNumber = (short)AtoiC(kiki.ItemName);
            }
            else if (kiki.Kakko1 == 12)
            {
                // 【C原典】外部 制御機器。
                equipment.Add(new ControlEquipmentEntry
                {
                    ReservedWord = kiki.ReservedWord,
                    ExternalCount = kiki.Quantity == 0 ? (short)1 : kiki.Quantity,
                });
            }
            else
            {
                // 【C原典】内部 制御機器。
                var inner = new ControlEquipmentEntry { ReservedWord = kiki.ReservedWord };

                // 【C原典】改訂<21>: THR/AL でインターロック名称先頭が '<' なら "<THR"/"<AL" とする。
                if (string.Equals(kiki.ReservedWord, "THR", StringComparison.Ordinal)
                    || string.Equals(kiki.ReservedWord, "AL", StringComparison.Ordinal))
                {
                    string work2 = CircuitAreaLineReader.GetCircuitAreaText(
                        kiki.DescriptionRow, kiki.DescriptionColumn, circuitLines);
                    work2 = CompoSpaceCutter.CutSpaces(work2);
                    int nket = EquipmentParameterFormatter.Stoi(kiki.DescriptionColumn, 3);
                    int idx = nket - 2;
                    if (idx >= 0 && idx < work2.Length && work2[idx] == '<')
                    {
                        inner.ReservedWord = "<" + kiki.ReservedWord;
                    }
                }

                inner.InternalCount = kiki.Quantity == 0 ? (short)1 : kiki.Quantity;
                equipment.Add(inner);
            }
        }
        // 【C原典】SCnt = equipment.Count

        // 【C原典】setInterlockToSkey(SgsTbl->Pcstrg)。
        ControlInterlockKeyBuilder.AppendInterlockKeys(controlSpec.RawText, equipment);

        result.PatternNumber = patternNumber;
        result.InterlockFlag = interlockFlag;
        return result;
    }

    // 【C原典】isalpha。予約語は ASCII 英字のみ。
    private static bool IsAlpha(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    // 【C原典】isdigit。
    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    // 【C原典】atoi。先頭空白を読み飛ばし、任意符号+先頭数字列を整数化する。
    private static int AtoiC(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        int i = 0;
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
        {
            i++;
        }

        int sign = 1;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            if (s[i] == '-')
            {
                sign = -1;
            }
            i++;
        }

        long value = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = (value * 10) + (s[i] - '0');
            i++;
        }
        return (int)(sign * value);
    }
}

/// <summary>
/// 制御仕様データ作成(GetSgData)の結果集約。
/// 【C原典】Fyss1k.c のグローバル <c>Sgtkk[30]</c>/<c>SgtCnt</c>/<c>Sgkk[50]</c>/<c>SCnt</c>/
/// <c>ptn_no</c>/<c>intr_flag</c> に相当する。
/// </summary>
public sealed class ControlSpecData
{
    /// <summary>制御対象機器文字列テーブル。【C原典】Sgtkk[0..SgtCnt)。</summary>
    public List<ControlTargetEntry> ControlTargets { get; } = new();

    /// <summary>内部・外部制御機器データテーブル。【C原典】Sgkk[0..SCnt)。</summary>
    public List<ControlEquipmentEntry> ControlEquipment { get; } = new();

    /// <summary>PTN指定番号。【C原典】ptn_no。</summary>
    public short PatternNumber { get; set; } = 1;

    /// <summary>インターロック指定フラグ。【C原典】intr_flag(0:無し 1:有り)。</summary>
    public bool InterlockFlag { get; set; }
}
