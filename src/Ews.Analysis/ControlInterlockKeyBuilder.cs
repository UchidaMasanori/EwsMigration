namespace Ews.Analysis;

using System;
using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// インターロック記述から制御回路仕様キー(SGKK)へ <c>&lt;THR</c>/<c>&lt;AL</c>/<c>&lt;CR</c> を設定する。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>setInterlockToSkey</c>(改訂&lt;21&gt;)。
///
/// 制御仕様文字列に <c>&lt;**</c> 記述(<c>&lt;THR</c>/<c>&lt;AL</c> を除く)が複数ある場合、
/// 1 つ目はインタロック名称に CR をセットするが、2 つ目以降は制御回路仕様キーの予約語へ
/// <c>&lt;CR</c> をセットする。
/// </summary>
public static class ControlInterlockKeyBuilder
{
    /// <summary>
    /// 制御仕様文字列を走査し、制御機器テーブル(Sgkk)へインターロックキーを追加する。
    /// 【C原典】setInterlockToSkey(Fyss1k.c:548)。<paramref name="controlEquipment"/> が
    /// C の <c>Sgkk[0..SCnt)</c> に相当し、末尾に追記する。
    /// </summary>
    /// <param name="controlSpecText">制御仕様文字列。【C原典】pcstrg。</param>
    /// <param name="controlEquipment">制御機器テーブル(SGKK)。追記対象。【C原典】Sgkk/SCnt。</param>
    public static void AppendInterlockKeys(string? controlSpecText, List<ControlEquipmentEntry> controlEquipment)
    {
        ArgumentNullException.ThrowIfNull(controlEquipment);

        string pcstrg = controlSpecText ?? string.Empty;

        // 【C原典】<THR が既に設定済みかチェック(改訂<23>)。
        bool thrFlag = false;
        foreach (ControlEquipmentEntry e in controlEquipment)
        {
            if (e.ReservedWord == "<THR")
            {
                thrFlag = true;
                break;
            }
        }

        // 【C原典】<AL が既に設定済みかチェック(改訂<25>)。
        bool alFlag = false;
        foreach (ControlEquipmentEntry e in controlEquipment)
        {
            if (e.ReservedWord == "<AL")
            {
                alFlag = true;
                break;
            }
        }

        int crCount = 0;
        int idx = 0;
        while (true)
        {
            int pos = pcstrg.IndexOf('<', idx);
            if (pos < 0)
            {
                break;
            }

            string remaining = pcstrg[pos..];
            if (remaining.StartsWith("<THR", StringComparison.Ordinal))
            {
                // 【C原典】memcmp(pt,"<THR",4)==0。thr_flg はループ内で更新しない。
                if (!thrFlag)
                {
                    controlEquipment.Add(new ControlEquipmentEntry { ReservedWord = "<THR", InternalCount = 1 });
                }
            }
            else if (remaining.StartsWith("<AL", StringComparison.Ordinal))
            {
                // 【C原典】<ALの後にカンマが無い場合のみキーに<ALを追加。
                if (remaining.IndexOf(',') < 0 && !alFlag)
                {
                    controlEquipment.Add(new ControlEquipmentEntry { ReservedWord = "<AL", InternalCount = 1 });
                }
            }
            else
            {
                // 【C原典】<CR をカウントする。
                crCount++;
            }

            idx = pos + 1;
        }

        // 【C原典】キーに <CR を追加(2 個以上で個数=cr_cnt-1)。
        if (crCount > 1)
        {
            controlEquipment.Add(new ControlEquipmentEntry { ReservedWord = "<CR", InternalCount = (short)(crCount - 1) });
        }
    }
}
