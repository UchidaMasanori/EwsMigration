namespace Ews.Analysis;

using System.Collections.Generic;
using Ews.Domain.Analysis;

/// <summary>
/// 主回路エリア(FYRT800)から制御電源データを取得するリーフ群。
/// 【C原典】toku/sekkei/src/Fyss1k.c の <c>getCtlDenKno</c>(改訂&lt;20&gt;)/<c>GetSeivdnoUp</c>。
/// </summary>
public static class ControlPowerSystemLocator
{
    /// <summary>
    /// 主回路エリアを走査し、制御電源番号(fpac)が検索キーと一致する最初のレコードの
    /// 系統番号(kno)を取得する。
    /// 【C原典】getCtlDenKno(Fyss1k.c:3619, 改訂&lt;20&gt;)。
    /// fpac[2] と検索キーを 2 バイト(memcmp 相当)で比較し、一致すれば kno[3] を返す。
    /// </summary>
    /// <param name="searchKey">
    /// 検索キー。【C原典】gyono(呼出元は FYRT820.gyono)。制御電源番号(fpac[2])と 2 バイト比較する。
    /// </param>
    /// <param name="mainCircuits">主回路エリア。【C原典】maina(件数 mainc)。</param>
    /// <param name="systemNumber">一致時に取得する系統番号(kno)。【C原典】出力引数 kno。</param>
    /// <returns>0:取得成功、-1:該当なし。【C原典】0/-1。</returns>
    public static int GetControlPowerSystemNumber(
        string? searchKey,
        IReadOnlyList<MainCircuitResult> mainCircuits,
        out string systemNumber)
    {
        ArgumentNullException.ThrowIfNull(mainCircuits);

        string key = Normalize2(searchKey);

        foreach (MainCircuitResult record in mainCircuits)
        {
            // 【C原典】memcmp(fpac, gyono, 2)。制御電源番号を 2 バイトで比較。
            if (string.Equals(Normalize2(record.Data.AttachedParameter.ControlPowerNumber), key, StringComparison.Ordinal))
            {
                systemNumber = record.Data.SystemNumber;   // 【C原典】memcpy(kno, dt.kno, 3)。
                return 0;
            }
        }

        systemNumber = string.Empty;
        return -1;
    }

    /// <summary>
    /// 制御文の記述行(kgyo)から直近上位の主回路行をさかのぼり、"UP" 行(行種コード="UP ")の
    /// 制御電源データ追番(datano)と盤種類(ep[0].epabn)を取得する。
    /// 【C原典】GetSeivdnoUp(Fyss1k.c:3392)。
    /// </summary>
    /// <param name="controlSpec">
    /// 制御仕様テーブルエントリ。【C原典】SgsTable(FYRT820*)。記述行 kgyo のみ参照する。
    /// </param>
    /// <param name="mainCircuits">主回路エリア。【C原典】maina(件数 mainc)。</param>
    /// <param name="controlPowerNumber">"UP" 行のデータ追番(datano)。【C原典】出力引数 seivdno。</param>
    /// <param name="panelType">"UP" 行の盤種類(ep[0].epabn)。【C原典】出力引数 bn。</param>
    /// <returns>0:取得成功、-1:直上主回路行が無い/"UP" 行が無い。【C原典】0/-1。</returns>
    public static int GetUpstreamControlPowerData(
        ControlSpecEntry controlSpec,
        IReadOnlyList<MainCircuitResult> mainCircuits,
        out string controlPowerNumber,
        out char panelType)
    {
        ArgumentNullException.ThrowIfNull(controlSpec);
        ArgumentNullException.ThrowIfNull(mainCircuits);

        controlPowerNumber = string.Empty;
        panelType = '\0';

        // 【C原典】sprintf(work,"%03d",SgsTable->kgyo); memcpy(kgyou,work,3)。記述行を 3 桁固定へ整形。
        string descriptionRow = FormatRow3(controlSpec.DescriptionRow);

        // 【C原典】末尾から遡り、記述行 > dt.gyo(memcmp>0)となる最初(=直近上位)の主回路行を探す。
        int j = mainCircuits.Count - 1;
        for (; j >= 0; j--)
        {
            if (string.CompareOrdinal(descriptionRow, Truncate3(mainCircuits[j].Data.DescriptionRow)) > 0)
            {
                break;
            }
        }

        if (j < 0)
        {
            return -1;   // 【C原典】直上主回路行が無い。
        }

        // 【C原典】そこから遡り、行種コード="UP " の行のデータ追番/盤種類を取得する。
        for (; j >= 0; j--)
        {
            MainCircuitResult record = mainCircuits[j];
            if (string.Equals(Truncate3(record.Data.LineTypeCode), "UP ", StringComparison.Ordinal))
            {
                controlPowerNumber = Truncate3(record.SequenceNumber);   // 【C原典】memcpy(seivdno, datano, 3)。
                panelType = record.Data.ElectricalParameterSlots[0].Bn;   // 【C原典】*bn = dt.ep[0].epabn。
                return 0;
            }
        }

        return -1;
    }

    // 【C原典】fpac[2] 固定長比較。空白詰め 2 文字へ正規化する(既存 PadRight(2)[..2] 慣習)。
    private static string Normalize2(string? value)
    {
        return (value ?? string.Empty).PadRight(2)[..2];
    }

    // 【C原典】sprintf("%03d") 相当。3 桁未満は 0 埋め、超過分は先頭 3 バイト(memcpy 3)。
    private static string FormatRow3(short value)
    {
        string s = value.ToString("D3");
        return s.Length > 3 ? s[..3] : s;
    }

    // 【C原典】memcmp/memcpy 3 バイト相当。固定長 3 文字へ切り詰め(不足は空白詰め)。
    private static string Truncate3(string? value)
    {
        return (value ?? string.Empty).PadRight(3)[..3];
    }
}
