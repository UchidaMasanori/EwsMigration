namespace Ews.Analysis;

using System;
using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 制御機器テーブル(KIKITABLE)への同一機器認識番号(E_No)設定。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>SetCkikiDkkno</c>(改訂&lt;26&gt;)。
///
/// 予約語番号付き機器から予約番号テーブル(YKNO)を構築し、予約語+予約語番号でソート
/// (<see cref="ReservedNumberComparer"/> = sortcmp3)した上で、同一予約語グループへ
/// 一意の同一機器認識番号を採番する。6A リレー(RRY 6A4K)は接点分機器を増やすため
/// あらかじめ番号を設定する特例を含む。
/// </summary>
public static class ControlEquipmentIdentityAssigner
{
    /// <summary>
    /// 制御機器テーブルへ同一機器認識番号を設定する。【C原典】SetCkikiDkkno(Fyss1k.c:2807)。
    /// 対象機器の <see cref="EquipmentTableEntry.EquipmentIdentityNumber"/> を直接更新する。
    /// </summary>
    /// <param name="controlEquipment">制御機器テーブル(Ckk)。件数=Ccnt。</param>
    /// <param name="startIdentityNumber">同一機器認識番号の開始値(dkno+1)。【C原典】dkkno。</param>
    /// <param name="controlSpecs">制御仕様テーブル(Sgs)。件数=Sgscnt。</param>
    public static void AssignIdentityNumbers(
        IReadOnlyList<EquipmentTableEntry> controlEquipment,
        short startIdentityNumber,
        IReadOnlyList<ControlSpecEntry> controlSpecs)
    {
        ArgumentNullException.ThrowIfNull(controlEquipment);
        ArgumentNullException.ThrowIfNull(controlSpecs);

        int ccnt = controlEquipment.Count;
        if (ccnt == 0)
        {
            return;                                     // 【C原典】if( 0 == Ccnt ) return;
        }

        short dkkno = startIdentityNumber;

        // 【C原典】予約語番号の有るﾃﾞ-ﾀ(ysno > "00")をﾃ-ﾌﾞﾙにset。
        var ykno = new List<ReservedNumberEntry>();
        for (int i = 0; i < ccnt; i++)
        {
            EquipmentTableEntry e = controlEquipment[i];
            if (string.CompareOrdinal(e.ReservedWordNumber ?? string.Empty, "00") > 0)
            {
                ykno.Add(new ReservedNumberEntry
                {
                    ReservedKey = (e.ReservedWord ?? string.Empty) + (e.ReservedWordNumber ?? string.Empty),
                    DataNumber = (short)i,
                });
            }
        }

        int cnt = ykno.Count;

        // 【C原典】qsort( ykno, cnt, sizeof(YKNO), sortcmp3 )。予約語+予約語番号昇順。
        ykno.Sort(ReservedNumberComparer.Instance);

        for (int i = 0; i < cnt; i++)
        {
            // 【C原典】同一機器の場合(次要素と予約語が一致)。
            // ※C原典は ykno[i+1] を i==cnt-1 でも参照する(malloc+memset 済み領域=空文字が番兵)。
            //   KeyAt() で範囲外を空文字として扱い忠実に再現する。
            if (string.CompareOrdinal(KeyAt(ykno, cnt, i), KeyAt(ykno, cnt, i + 1)) == 0)
            {
                for (; i < cnt;)
                {
                    controlEquipment[ykno[i].DataNumber].EquipmentIdentityNumber = dkkno;
                    i++;
                    if (string.CompareOrdinal(KeyAt(ykno, cnt, i), KeyAt(ykno, cnt, i + 1)) != 0)
                    {
                        controlEquipment[ykno[i].DataNumber].EquipmentIdentityNumber = dkkno;
                        break;
                    }
                }

                dkkno++;
            }

            // 【C原典】改訂<26> RRY 6A4K(6A リレー)特例。
            else if ((controlEquipment[ykno[i].DataNumber].ReservedWord ?? string.Empty) == "RRY"
                     && (controlEquipment[ykno[i].DataNumber].DType[1] ?? string.Empty) == "6A4K")
            {
                short rank = controlEquipment[ykno[i].DataNumber].Rank;

                // 【C原典】制御回路仕様名称追番が同じ機器ﾃ-ﾌﾞﾙをｻ-ﾁ。
                for (int j = 0; j < controlSpecs.Count; j++)
                {
                    if (controlSpecs[j].SpecNameSequence == rank)
                    {
                        if ((controlSpecs[j].RawText ?? string.Empty) == "G(RRY)")
                        {
                            break;                      // 【C原典】主回路側の6Aリレー。
                        }

                        // 【C原典】制御回路側の6Aリレー。接点数カウント文字列 "RRY%d-" 作成。
                        string work = "RRY" + Atoi(controlEquipment[ykno[i].DataNumber].ReservedWordNumber)
                            .ToString(System.Globalization.CultureInfo.InvariantCulture) + "-";

                        // 【C原典】接点数カウント(Pcstrg に work を含む Sgs の数)。
                        int rry6a4kCnt = 0;
                        for (j = 0; j < controlSpecs.Count; j++)
                        {
                            if ((controlSpecs[j].RawText ?? string.Empty).Contains(work, StringComparison.Ordinal))
                            {
                                rry6a4kCnt++;
                            }
                        }

                        // 【C原典】2個以上あったら同一機器認識番号を設定。
                        if (rry6a4kCnt >= 2)
                        {
                            controlEquipment[ykno[i].DataNumber].EquipmentIdentityNumber = dkkno;
                            dkkno++;
                        }

                        break;
                    }
                }
            }
        }
    }

    // 【C原典】ykno[idx].yoyaku。範囲外(idx>=cnt)は malloc+memset 済みの空文字を番兵として返す。
    private static string KeyAt(IReadOnlyList<ReservedNumberEntry> ykno, int cnt, int idx)
    {
        return idx < cnt ? (ykno[idx].ReservedKey ?? string.Empty) : string.Empty;
    }

    // 【C原典】atoi(ysno)。先頭空白・符号をスキップし数字列を整数化する。
    private static int Atoi(string? s)
    {
        s ??= string.Empty;
        int idx = 0;
        while (idx < s.Length && char.IsWhiteSpace(s[idx]))
        {
            idx++;
        }

        int sign = 1;
        if (idx < s.Length && (s[idx] == '+' || s[idx] == '-'))
        {
            if (s[idx] == '-')
            {
                sign = -1;
            }

            idx++;
        }

        int val = 0;
        while (idx < s.Length && s[idx] >= '0' && s[idx] <= '9')
        {
            val = (val * 10) + (s[idx] - '0');
            idx++;
        }

        return sign * val;
    }
}
