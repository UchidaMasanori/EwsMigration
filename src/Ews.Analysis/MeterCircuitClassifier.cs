using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路レコードを走査し、計器回路機器(CT/F/DSW/VT/PLTR)・LGR・ZCT の該当機器を
/// <see cref="MeterCircuitEntry"/>(WK_Keiki)リストへ分類・収集する。
/// これらのリストは <see cref="MeterCircuitBuilder"/>(=Fysk00_Make_Keiki)の入力となる。
/// 【C原典】<c>Keiki_Check</c>/<c>LGR_Check</c>/<c>ZCT_Check</c>(toku/sekkei/src/Fysk00.c:3815/3857/3890)。
///
/// C 原典は static カウンタ <c>k</c> と malloc/realloc で単一リストを成長させ、
/// <c>*m == NULL</c> で <c>k</c> をリセットする。本移行では呼び出し側が用意する
/// <see cref="IList{T}"/> へ追記する形に置き換える(空リスト開始が <c>*m == NULL</c> 相当)。
/// 出力件数 <c>*ken</c> はリストの要素数に対応する。
/// </summary>
public static class MeterCircuitClassifier
{
    /// <summary>
    /// 計器回路機器と判定する予約語(先頭 4 文字一致キー)。
    /// 【C原典】memcmp(yo,"CT  ",4)/"F   "/"DSW "/"VT  "/"PLTR"。
    /// </summary>
    private static readonly string[] MeterReservedWords = ["CT  ", "F   ", "DSW ", "VT  ", "PLTR"];

    /// <summary>計器予約語キーの比較幅。【C原典】memcmp(...,4)。</summary>
    private const int MeterKeyWidth = 4;

    /// <summary>回路数(epak)の桁数。【C原典】Stoi(ep[2].epak,3)。</summary>
    private const int CircuitCountWidth = 3;

    /// <summary>
    /// 計器回路機器(CT/F/DSW/VT/PLTR)なら該当レコードを計器リストへ追加する。
    /// 【C原典】<c>Keiki_Check(yo, i, ken, m)</c>。
    /// </summary>
    /// <param name="meters">計器機器リスト。【C原典】*m (WK_Keiki **)。</param>
    /// <param name="reservedWord">予約語。【C原典】yo (CHAR *)。</param>
    /// <param name="recordIndex">主回路レコード添字(0 始まり)。【C原典】i。</param>
    /// <returns>追加したら true(【C原典】return 1)、非該当なら false(return 0)。</returns>
    public static bool TryClassifyMeter(IList<MeterCircuitEntry> meters, string? reservedWord, int recordIndex)
    {
        ArgumentNullException.ThrowIfNull(meters);

        string key = (reservedWord ?? string.Empty).PadRight(MeterKeyWidth)[..MeterKeyWidth];
        foreach (string word in MeterReservedWords)
        {
            if (string.CompareOrdinal(key, word) == 0)
            {
                meters.Add(new MeterCircuitEntry { Rec = recordIndex, Katei = 0 });
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// LGR(漏電継電器)なら該当レコードを LGR リストへ追加する。【C原典】<c>LGR_Check(i, sk, ken, m)</c>。
    /// 条件: 回路数(ep[2].epak)&gt;0 かつ 行種コード 2 文字目(gyocd[1])が 'P' でない。
    /// </summary>
    /// <param name="relays">LGR リスト。【C原典】*m (WK_Keiki **)。</param>
    /// <param name="records">主回路データ配列。【C原典】sk (FYRT800 [])。</param>
    /// <param name="recordIndex">主回路レコード添字(0 始まり)。【C原典】i。</param>
    /// <returns>追加したら true(【C原典】return 1)、非該当なら false(return 0)。</returns>
    public static bool TryClassifyLeakageGroundRelay(IList<MeterCircuitEntry> relays, IReadOnlyList<MainCircuitResult> records, int recordIndex)
    {
        ArgumentNullException.ThrowIfNull(relays);
        ArgumentNullException.ThrowIfNull(records);

        MainCircuitData data = records[recordIndex].Data;

        // 【C原典】Stoi(sk[i].dt.ep[2].epak,3) > 0 && sk[i].dt.gyocd[1] != 'P'。
        int circuitCount = EquipmentParameterFormatter.Stoi(data.ElectricalParameterSlots[2].K, CircuitCountWidth);
        string lineTypeCode = data.LineTypeCode ?? string.Empty;
        char lineTypeSecond = lineTypeCode.Length > 1 ? lineTypeCode[1] : ' ';

        if (circuitCount > 0 && lineTypeSecond != 'P')
        {
            relays.Add(new MeterCircuitEntry { Rec = recordIndex, Katei = 0 });
            return true;
        }

        return false;
    }

    /// <summary>
    /// ZCT(零相変流器)の該当レコードを ZCT リストへ無条件で追加する。【C原典】<c>ZCT_Check(i, ken, m)</c>。
    /// </summary>
    /// <param name="transformers">ZCT リスト。【C原典】*m (WK_Keiki **)。</param>
    /// <param name="recordIndex">主回路レコード添字(0 始まり)。【C原典】i。</param>
    public static void ClassifyZeroCurrentTransformer(IList<MeterCircuitEntry> transformers, int recordIndex)
    {
        ArgumentNullException.ThrowIfNull(transformers);

        transformers.Add(new MeterCircuitEntry { Rec = recordIndex, Katei = 0 });
    }
}
