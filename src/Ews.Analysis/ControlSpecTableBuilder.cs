namespace Ews.Analysis;

using System;
using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 制御仕様テーブル(FYRT820)へ 1 レコードを set する。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>MakeSgsTable</c>。
///
/// 系統テーブル(KEITOU)/行種テーブル(GYOSYU)/制御仕様文字列から制御仕様テーブルの
/// 1 レコードを構築する。制御対象機器データ追番・インターロック関連以外のデータを set する。
/// C 原典は <c>realloc</c> でエリアを拡張し件数(*i_SgsTabl)を更新するが、本移行では
/// <see cref="List{T}"/> への追加で表現する。
/// </summary>
public static class ControlSpecTableBuilder
{
    /// <summary>
    /// 制御仕様テーブルへ 1 レコードを追加する。【C原典】<c>MakeSgsTable(i_SgsTabl, P_SgsTable, Keitou, Gyosyu, control, seigno, keta)</c>。
    /// </summary>
    /// <param name="controlSpecTable">制御仕様テーブル。【C原典】P_SgsTable/i_SgsTabl。</param>
    /// <param name="system">系統テーブルデータ。【C原典】Keitou。</param>
    /// <param name="lineType">行種テーブルデータ。【C原典】Gyosyu。</param>
    /// <param name="control">制御仕様文字列。【C原典】control。</param>
    /// <param name="controlSpecGroup">制御仕様グループNo.。【C原典】seigno。</param>
    /// <param name="column">記述桁。【C原典】keta。</param>
    /// <returns>追加したレコード。</returns>
    public static ControlSpecEntry MakeSgsTable(
        List<ControlSpecEntry> controlSpecTable,
        SystemTableEntry system,
        LineTypeTableEntry lineType,
        string? control,
        short controlSpecGroup,
        short column)
    {
        ArgumentNullException.ThrowIfNull(controlSpecTable);
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(lineType);

        var entry = new ControlSpecEntry
        {
            // 【C原典】wksgs->kno = Keitou.K_No。
            SystemNumber = system.SystemNumber,

            // 【C原典】gyocd = Gyosyu.gyosyu(左詰め・空白埋め 3桁)。
            LineTypeCode = LeftAlign(lineType.LineType, 3),

            // 【C原典】gyono = Gyosyu.Gyosyu の gyosyu 以降(右詰め・'0'埋め 2桁)。
            LineTypeNumber = RightAlignZero(LineTypeSuffix(lineType), 2),

            // 【C原典】seigno = seigno。
            ControlSpecGroupNumber = controlSpecGroup,

            // 【C原典】gno = Gyosyu.G_No。
            GroupNumber = lineType.GroupNumber,

            // 【C原典】Pcstrg = strdup(control)。
            RawText = control ?? string.Empty,

            // 【C原典】kgyo = atoi(Gyosyu.K_Gyo)。
            DescriptionRow = (short)AtoiC(lineType.DescriptionRow),

            // 【C原典】keta = keta。
            DescriptionColumn = column,
        };

        // 【C原典】系統種別 set。1/3:P系統='1'、4:UP系統='2'、その他は初期値' '。
        if (system.SystemKind == '1' || system.SystemKind == '3')
        {
            entry.SystemKind = '1';
        }
        else if (system.SystemKind == '4')
        {
            entry.SystemKind = '2';
        }

        // 【C原典】(*i_SgsTabl)++ 後の値を cnameno に set(1 始まり)。
        controlSpecTable.Add(entry);
        entry.SpecNameSequence = (short)controlSpecTable.Count;

        return entry;
    }

    // 【C原典】wptr = Gyosyu.Gyosyu + strlen(Gyosyu.gyosyu)。原文(Gyosyu)の整形部(gyosyu)以降。
    private static string LineTypeSuffix(LineTypeTableEntry lineType)
    {
        string raw = lineType.LineTypeRaw ?? string.Empty;
        string formatted = lineType.LineType ?? string.Empty;
        return raw.Length > formatted.Length ? raw.Substring(formatted.Length) : string.Empty;
    }

    // 【C原典】memset(dst,' ',size); memcpy(dst, src, strlen(src))。左詰め・空白埋め。
    private static string LeftAlign(string? value, int size)
    {
        string s = value ?? string.Empty;
        if (s.Length >= size)
        {
            return s.Substring(0, size);
        }
        return s.PadRight(size, ' ');
    }

    // 【C原典】memset(dst,'0',size); memcpy(dst+size-strlen(src), src, strlen(src))。右詰め・'0'埋め。
    private static string RightAlignZero(string? value, int size)
    {
        string s = value ?? string.Empty;
        if (s.Length >= size)
        {
            return s.Substring(s.Length - size, size);
        }
        return s.PadLeft(size, '0');
    }

    // 【C原典】atoi。先頭空白を読み飛ばし、任意符号+先頭数字列を整数化する。
    private static int AtoiC(string? str)
    {
        string s = str ?? string.Empty;
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
