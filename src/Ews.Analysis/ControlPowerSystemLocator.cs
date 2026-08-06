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

    /// <summary>
    /// 自系統の P 行(回路相数/線式/電圧)と一致する別系統を探し、その系統内で制御電源番号(fpac)が
    /// 行種番号(gyono)と一致する行のデータ追番(datano)と盤種類(ep[0].epabn)を取得する。
    /// 【C原典】GetSeivdnoOtherKeitou(Fyss1k.c:3051, 改訂&lt;15&gt;)。
    /// 複数一致時は「自系統番号より小さい系統を優先」「その中で自系統に近い系統を優先」で選ぶ。
    /// </summary>
    /// <param name="controlSpec">制御仕様テーブルエントリ。【C原典】SgsTable(FYRT820*)。kno/gyono を参照。</param>
    /// <param name="mainCircuits">主回路エリア。【C原典】maina(件数 mainc)。</param>
    /// <param name="controlPowerNumber">取得したデータ追番(datano)。【C原典】出力引数 seivdno。</param>
    /// <param name="panelType">取得した盤種類(ep[0].epabn)。【C原典】出力引数 bn。</param>
    /// <returns>0:取得成功、-1:一致する別系統が無い。【C原典】0/-1。</returns>
    public static int GetControlPowerDataFromOtherSystem(
        ControlSpecEntry controlSpec,
        IReadOnlyList<MainCircuitResult> mainCircuits,
        out string controlPowerNumber,
        out char panelType)
    {
        ArgumentNullException.ThrowIfNull(controlSpec);
        ArgumentNullException.ThrowIfNull(mainCircuits);

        controlPowerNumber = string.Empty;
        panelType = '\0';

        // 【C原典】sprintf(kno,"%03d",P_SgsTable->kno)。自系統番号を 3 桁へ整形。
        string ownKno = FormatRow3(controlSpec.SystemNumber);
        string lineTypeNumber = Normalize2(controlSpec.LineTypeNumber);   // gyono[2]

        // 【C原典】自系統(gyocd="P ", kno 一致)の P 行から回路相数/線式/電圧を取得。
        char kpaph = '\0';
        char kpawr = '\0';
        string kpav = string.Empty;   // 【C原典】未取得時は未初期化(=一致し得ない扱い)。
        foreach (MainCircuitResult record in mainCircuits)
        {
            MainCircuitData d = record.Data;
            if (string.Equals(Truncate3(d.LineTypeCode), "P  ", StringComparison.Ordinal)
                && string.Equals(Truncate3(d.SystemNumber), ownKno, StringComparison.Ordinal))
            {
                kpaph = d.CircuitPhaseCount;
                kpawr = d.CircuitWireType;
                kpav = JoinVoltage(d.CircuitVoltage);
                break;
            }
        }

        int mine = controlSpec.SystemNumber;   // 【C原典】(INT)P_SgsTable->kno。
        int knoOther = -1;

        // 【C原典】別系統で回路相数/線式/電圧が一致する P 行を探す。
        for (int i = 0; i < mainCircuits.Count; i++)
        {
            MainCircuitData di = mainCircuits[i].Data;
            if (!string.Equals(Truncate3(di.LineTypeCode), "P  ", StringComparison.Ordinal))
            {
                continue;
            }
            if (string.Equals(Truncate3(di.SystemNumber), ownKno, StringComparison.Ordinal))
            {
                continue;   // 【C原典】自系統は除外。
            }
            if (di.CircuitPhaseCount != kpaph
                || di.CircuitWireType != kpawr
                || !string.Equals(JoinVoltage(di.CircuitVoltage), kpav, StringComparison.Ordinal))
            {
                continue;
            }

            // 【C原典】一致した系統内で制御電源番号(fpac)==gyono の行を探す。
            string systemI = Truncate3(di.SystemNumber);
            for (int j = 0; j < mainCircuits.Count; j++)
            {
                MainCircuitResult rj = mainCircuits[j];
                MainCircuitData dj = rj.Data;
                if (!string.Equals(Truncate3(dj.SystemNumber), systemI, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!string.Equals(Normalize2(dj.AttachedParameter.ControlPowerNumber), lineTypeNumber, StringComparison.Ordinal))
                {
                    continue;
                }

                int knoW = AtoiKno(dj.SystemNumber);   // 【C原典】atoi(dt.kno)。

                // 【C原典】優先度: (1)自系統より小さい系統を優先 (2)自系統に近い系統を優先。
                bool update;
                if (knoOther == -1)
                {
                    update = true;
                }
                else if (knoW < mine)
                {
                    update = knoOther > mine || knoW > knoOther;
                }
                else
                {
                    update = knoOther > mine && knoW < knoOther;
                }

                if (update)
                {
                    controlPowerNumber = Truncate3(rj.SequenceNumber);
                    panelType = dj.ElectricalParameterSlots[0].Bn;
                    knoOther = knoW;
                }
            }
        }

        return knoOther == -1 ? -1 : 0;
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

    // 【C原典】kpav[3][3] の 9 バイト memcmp 相当。3 電圧スロットを各 3 文字に整形して連結。
    private static string JoinVoltage(string[] voltage)
    {
        return Truncate3(voltage[0]) + Truncate3(voltage[1]) + Truncate3(voltage[2]);
    }

    // 【C原典】atoi(work)。kno[3] を数値化(先頭空白無視、非数字で打ち切り)。
    private static int AtoiKno(string? kno)
    {
        string s = Truncate3(kno);
        int i = 0;
        while (i < s.Length && s[i] == ' ')
        {
            i++;
        }
        int value = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = (value * 10) + (s[i] - '0');
            i++;
        }
        return value;
    }
}
