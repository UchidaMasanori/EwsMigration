using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 機器検索の前処理(Fysk00_Kikisearch_SY_Sub)で行われる、特定機器のメーカーコード選定順位
/// (mcod[4][3]/msu)の上書き補正をまとめる。マスタ検索(Fysk01)より前段の純粋処理。
/// 【C原典】(全て toku/sekkei/src/Fysk00.c)
///   - <see cref="AdjustRtrMaker"/>  : PropChgRtrMaker(:4913, 改訂&lt;27&gt;)
///   - <see cref="AdjustRmcbMaker"/> : PropChgRmcbMaker(:6005, 改訂&lt;60&gt;)
///   - <see cref="AdjustNl63Maker"/> : PropChgNL63Maker(:11620, 改訂&lt;139&gt;/&lt;150&gt;)
///   - <see cref="AdjustWhMaker"/>   : PropChgWHMaker(:11897, 改訂&lt;144&gt;)
///   - <see cref="AdjustInvbpMaker"/>: PropChgINVBPMaker(:11942, 改訂&lt;148&gt;)
///   - <see cref="AdjustGpnMaker"/>  : PropChgGPNMaker(:11675, 改訂&lt;141&gt;, 制御 kikijg)
/// メーカーコード選定順位 mcod[4][3] は 4 スロット(各 3 桁)の <see cref="string"/>[] で、
/// 件数 msu は <c>ref int</c> で表現する。
/// </summary>
public static class EquipmentMakerOverrideAdjuster
{
    private const int SlotCount = 4;
    private const int CodeWidth = 3;
    private const string BlankCode = "   ";

    /// <summary>
    /// フル2線かつメーカー未指定の RTR を松下製(D)に固定する。
    /// 【C原典】PropChgRtrMaker(Fysk00.c:4913, 改訂&lt;27&gt;)。
    /// </summary>
    /// <param name="full2sen">フル2線フラグ(0:フル2線)。【C原典】full2sen。PropJdgFull2sen の判定結果。</param>
    /// <param name="rtr">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4 スロット)。【C原典】mcod[][3]。</param>
    /// <param name="count">選定順位件数。【C原典】*msu。</param>
    public static void AdjustRtrMaker(int full2sen, MainCircuitResult rtr,
                                      string[] makerCodes, ref int count)
    {
        ArgumentNullException.ThrowIfNull(rtr);
        ValidateSlots(makerCodes);

        // 【C原典】フル2線でない or RTR でない or メーカー指定ありは対象外。
        if (full2sen != 0 ||
            !Matches(rtr.Data.ReservedWord, "RTR ", 4) ||
            !Matches(rtr.Data.AttachedParameter.MakerCode, BlankCode, 3))
        {
            return;
        }
        SetSingle(makerCodes, "D  ", ref count);
    }

    /// <summary>
    /// コンポ仕様かつメーカー未指定の RMCB を松下製(D)に固定する。
    /// 【C原典】PropChgRmcbMaker(Fysk00.c:6005, 改訂&lt;60&gt;)。
    /// </summary>
    /// <param name="specKind">仕様(特注:0 コンポ:1)。【C原典】cpf。</param>
    /// <param name="rmcb">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4 スロット)。【C原典】mcod[][3]。</param>
    /// <param name="count">選定順位件数。【C原典】*msu。</param>
    public static void AdjustRmcbMaker(int specKind, MainCircuitResult rmcb,
                                       string[] makerCodes, ref int count)
    {
        ArgumentNullException.ThrowIfNull(rmcb);
        ValidateSlots(makerCodes);

        // 【C原典】コンポ(cpf==1)でない or RMCB でない or メーカー指定ありは対象外。
        if (specKind != 1 ||
            !Matches(rmcb.Data.ReservedWord, "RMCB ", 5) ||
            !Matches(rmcb.Data.AttachedParameter.MakerCode, BlankCode, 3))
        {
            return;
        }
        SetSingle(makerCodes, "D  ", ref count);
    }

    /// <summary>
    /// KM/TL タイプで協約(KN)以外の MCB について、河村協約(KKY)を選定順位の先頭へ挿入する。
    /// 【C原典】PropChgNL63Maker(Fysk00.c:11620, 改訂&lt;139&gt;/&lt;150&gt;)。
    /// </summary>
    /// <param name="mcb">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4 スロット)。【C原典】mcod[][3]。</param>
    /// <param name="count">選定順位件数。【C原典】*msu。</param>
    public static void AdjustNl63Maker(MainCircuitResult mcb, string[] makerCodes, ref int count)
    {
        ArgumentNullException.ThrowIfNull(mcb);
        ValidateSlots(makerCodes);

        MainCircuitData d = mcb.Data;
        // 【C原典】タイプ0!=KM or タイプ1!=TL or メーカー==KN(改訂<150>) は対象外。
        if (!Matches(d.DataType[0], "KM ", 3) ||
            !Matches(d.DataType[1], "TL ", 3) ||
            Matches(d.AttachedParameter.MakerCode, "KN ", 3))
        {
            return;
        }
        // 【C原典】予約語 MCB のみ対象。
        if (!Matches(d.ReservedWord, "MCB ", 4))
        {
            return;
        }

        // 【C原典】改訂<150> 選定順位を1つ後ろへずらし先頭に KKY を挿入(件数は最大4で++)。
        for (int i = SlotCount - 1; i > 0; i--)
        {
            makerCodes[i] = Slot(makerCodes, i - 1);
        }
        makerCodes[0] = "KKY";
        if (count < SlotCount)
        {
            count++;
        }
    }

    /// <summary>
    /// QrespoPlus(ZONECD 33333/33334/33335) の 1P2W 210V WH のメーカー順位を変更する。
    /// 【C原典】PropChgWHMaker(Fysk00.c:11897, 改訂&lt;144&gt;)。
    /// </summary>
    /// <param name="zoneCode">運用地区コード。【C原典】getenv("ZONECD")。</param>
    /// <param name="wh">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4 スロット)。【C原典】mcod[][3]。</param>
    /// <param name="count">選定順位件数。【C原典】*msu。</param>
    public static void AdjustWhMaker(string zoneCode, MainCircuitResult wh,
                                     string[] makerCodes, ref int count)
    {
        ArgumentNullException.ThrowIfNull(zoneCode);
        ArgumentNullException.ThrowIfNull(wh);
        ValidateSlots(makerCodes);

        // 【C原典】QrespoPlus のみ対象。
        if (zoneCode != "33335" && zoneCode != "33334" && zoneCode != "33333")
        {
            return;
        }
        if (!Matches(wh.Data.ReservedWord, "WH ", 3))
        {
            return;
        }

        // 【C原典】1P2W 210V のとき MS/MN/M/ON に変更。
        MainCircuitData d = wh.Data;
        if (d.CircuitPhaseCount == '1' && d.CircuitWireType == '2' &&
            Matches(d.CircuitVoltage[0], "210", 3))
        {
            makerCodes[0] = "MS ";
            makerCodes[1] = "MN ";
            makerCodes[2] = "M  ";
            makerCodes[3] = "ON ";
            count = 4;
        }
    }

    /// <summary>
    /// INVBP(特殊予約語区分 '7') の MC/THR のメーカーを負荷容量に応じて固定する。
    /// 【C原典】PropChgINVBPMaker(Fysk00.c:11942, 改訂&lt;148&gt;)。
    /// </summary>
    /// <param name="invbp">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4 スロット)。【C原典】mcod[][3]。</param>
    /// <param name="count">選定順位件数。【C原典】*msu。</param>
    public static void AdjustInvbpMaker(MainCircuitResult invbp, string[] makerCodes, ref int count)
    {
        ArgumentNullException.ThrowIfNull(invbp);
        ValidateSlots(makerCodes);

        MainCircuitData d = invbp.Data;
        // 【C原典】INVBP(tokkbn=='7') 以外は対象外。
        if (invbp.Data.SpecialReservedWordKind != '7')
        {
            return;
        }

        if (Matches(d.ReservedWord, "MC ", 3))
        {
            SetSingleAll(makerCodes, "MN ", ref count);
        }
        else if (Matches(d.ReservedWord, "THR ", 4))
        {
            // 【C原典】負荷容量(fpalw2)/1000 が 22.01～30.0 は三菱大形(MS)、他は三菱(MN)。
            double loadKw = EquipmentParameterFormatter.Stof(d.AttachedParameter.LoadCapacity, 7) / 1000.0;
            SetSingleAll(makerCodes, loadKw is >= 22.01 and <= 30.0 ? "MS " : "MN ", ref count);
        }
    }

    /// <summary>
    /// 制御機器(kikijg) GP/GPN/APN で、メーカー未指定なら選定順位の "OM" の直前に "OMN" を挿入する。
    /// 【C原典】PropChgGPNMaker(Fysk00.c:11675, 改訂&lt;141&gt;)。
    /// </summary>
    /// <param name="control">対象の制御機器レコード。【C原典】gk(=gk-&gt;u.k)。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4 スロット)。【C原典】mcod[][3]。</param>
    /// <param name="count">選定順位件数。【C原典】*msu。</param>
    public static void AdjustGpnMaker(ControlEquipmentInfo control, string[] makerCodes, ref int count)
    {
        ArgumentNullException.ThrowIfNull(control);
        ValidateSlots(makerCodes);

        // 【C原典】メーカー指定ありは対象外。
        if (FirstChar(control.MakerCode) != ' ')
        {
            return;
        }
        // 【C原典】GP/GPN/APN のみ対象。
        if (!Matches(control.ReservedWord, "GP ", 3) &&
            !Matches(control.ReservedWord, "GPN ", 4) &&
            !Matches(control.ReservedWord, "APN ", 4))
        {
            return;
        }

        // 【C原典】選定順位から "OM" の位置を探す。
        int index = -1;
        for (int i = 0; i < SlotCount; i++)
        {
            if (Matches(Slot(makerCodes, i), "OM ", 3))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            return;
        }

        // 【C原典】"OM" の直前に "OMN" を挿入(末尾は押し出される)。件数++。
        string[] shifted = ["", "", "", ""];
        for (int n = 0, i = 0; n < SlotCount; n++)
        {
            if (n == index)
            {
                shifted[n] = "OMN";
            }
            else
            {
                shifted[n] = Slot(makerCodes, i);
                i++;
            }
        }
        for (int i = 0; i < SlotCount; i++)
        {
            makerCodes[i] = shifted[i];
        }
        count++;
    }

    // 【C原典】mcod[0]=code; for(i=1;i<*msu;i++) mcod[i]="   "; *msu=1;(旧件数分のみクリア)
    private static void SetSingle(string[] makerCodes, string code, ref int count)
    {
        makerCodes[0] = code;
        for (int i = 1; i < count && i < SlotCount; i++)
        {
            makerCodes[i] = BlankCode;
        }
        count = 1;
    }

    // 【C原典】memcpy(mcod,"XX          ",12); *msu=1;(4スロット全てを code+空白で埋める)
    private static void SetSingleAll(string[] makerCodes, string code, ref int count)
    {
        makerCodes[0] = code;
        for (int i = 1; i < SlotCount; i++)
        {
            makerCodes[i] = BlankCode;
        }
        count = 1;
    }

    private static void ValidateSlots(string[] makerCodes)
    {
        ArgumentNullException.ThrowIfNull(makerCodes);
        if (makerCodes.Length != SlotCount)
        {
            throw new ArgumentException($"メーカーコード選定順位は {SlotCount} スロット必要です。", nameof(makerCodes));
        }
    }

    private static string Slot(string[] makerCodes, int index)
    {
        string value = makerCodes[index] ?? string.Empty;
        return value.PadRight(CodeWidth)[..CodeWidth];
    }

    private static char FirstChar(string value) => value.Length > 0 ? value[0] : ' ';

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
