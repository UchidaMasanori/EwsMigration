using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 機器検索の前処理(Fysk00_Kikisearch_SY_Sub)で行われる、WH/MC 系の機器タイプ・電気値補正をまとめる。
/// マスタ検索(Fysk01)より前段で、電気パラメータ(ep 文字列 / sep 数値)や表示タイプを直接修正する。
/// 【C原典】(全て toku/sekkei/src/Fysk00.c)
///   - <see cref="AdjustWhType"/>            : PropChgWHType(:2848, 改訂&lt;23&gt;)
///   - <see cref="AdjustMcMaker"/>           : PropChgMcMaker(:5087, 改訂&lt;37&gt;/&lt;38&gt;/&lt;66&gt;)
///   - <see cref="AdjustTaMcVoltage"/>       : PropChgTAMC_epav2(:5188, 改訂&lt;37&gt;)
///   - <see cref="AdjustWhmFukaDenFromChild"/>: PropWhmFukaDenFromChild(:6647, 改訂&lt;78&gt;/&lt;114&gt;/&lt;155&gt;)
/// </summary>
public static class McWhElectricalAdjuster
{
    private const string Blank7 = "       ";

    /// <summary>
    /// 電子式 WH+(KM) の表示タイプ2 をクリアし、検定無しタイプも選定可能にする。
    /// 【C原典】PropChgWHType(Fysk00.c:2848, 改訂&lt;23&gt;/&lt;49&gt;)。
    /// </summary>
    /// <param name="wh">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="displayTypes">表示用出力機器タイプ(変更対象)。【C原典】wtype[][7]。</param>
    public static void AdjustWhType(MainCircuitResult wh, string[] displayTypes)
    {
        ArgumentNullException.ThrowIfNull(wh);
        ArgumentNullException.ThrowIfNull(displayTypes);

        MainCircuitData d = wh.Data;
        // 【C原典】WH 以外は対象外。
        if (!Matches(d.ReservedWord, "WH ", 3))
        {
            return;
        }

        // 【C原典】SP枠(spkvn=='1') 以外で、タイプに KM があれば表示タイプ2 を空白に。
        if (d.AttachedParameter.SpFutureMountKind != '1')
        {
            for (int i = 0; i < 7; i++)
            {
                if (Matches(d.DataType[i], "KM ", 3))
                {
                    displayTypes[1] = Blank7;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 大陸製(TA) の MC を三菱製へ切り替え、3P50A+(SK) を選定させる。
    /// 【C原典】PropChgMcMaker(Fysk00.c:5087, 改訂&lt;37&gt;/&lt;38&gt;/&lt;66&gt;)。
    /// 改訂&lt;66&gt; により物件仕様(sshiykbn)判定は削除され、MC かつ mcod[0]==TA のみで動作する。
    /// </summary>
    /// <param name="mc">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4 スロット)。【C原典】mcod[][3]。</param>
    /// <param name="count">選定順位件数。【C原典】*msu。</param>
    /// <param name="dataTypes">主回路ファイルの機器タイプ。【C原典】dtype[][7]。</param>
    /// <param name="displayTypes">表示用出力機器タイプ。【C原典】wtype[][7]。</param>
    /// <param name="shapeTypeCount">変換形状タイプ数。【C原典】*tsu。</param>
    /// <param name="sep">電気パラメータ(数値)。【C原典】sep[]。</param>
    public static void AdjustMcMaker(MainCircuitResult mc, string[] makerCodes, ref int count,
                                     string[] dataTypes, string[] displayTypes,
                                     ref int shapeTypeCount, NumericElectricalParameters[] sep)
    {
        ArgumentNullException.ThrowIfNull(mc);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);
        ArgumentNullException.ThrowIfNull(sep);

        MainCircuitData d = mc.Data;
        // 【C原典】MC かつ mcod[0]==TA のみ対象。
        if (!Matches(d.ReservedWord, "MC ", 3) || !Matches(Slot(makerCodes, 0), "TA ", 3))
        {
            return;
        }

        ElectricalParameters ep0 = d.ElectricalParameterSlots[0];
        ElectricalParameters ep1 = d.ElectricalParameterSlots[1];
        ElectricalParameters ep2 = d.ElectricalParameterSlots[2];

        // 【C原典】3P50A(002/00050.000) or 3P20A(003/00020.000) は三菱製に固定。
        if ((Matches(ep1.P, "002", 3) && Matches(ep1.A2, "00050.000", 9)) ||
            (Matches(ep1.P, "003", 3) && Matches(ep1.A2, "00020.000", 9)))
        {
            makerCodes[0] = "MN ";
            for (int i = 1; i < count && i < 4; i++)
            {
                makerCodes[i] = "   ";
            }
            count = 1;
        }

        // 【C原典】改訂<38> 3P50A+(SK) を選定させる処置(タイプ未設定時)。
        if (Matches(ep1.P, "002", 3) && Matches(ep1.A2, "00050.000", 9) &&
            Matches(d.DataType[0], Blank7, 7))
        {
            dataTypes[0] = "SK     ";
            displayTypes[0] = "SK     ";
            shapeTypeCount = 1;

            // 【C原典】3P50A を選定後、定格値チェックを通す処置。
            sep[1].P = 3.0;
            ep1.P = "003";
            ep2.P = "003";

            if (Stof(ep0.V2[0], 8) == 0.0)
            {
                sep[1].V2[0] = 220.0;
                ep1.V2[0] = "000220.0";
                ep2.V2[0] = "000220.0";
            }

            if (Stof(ep0.Vc, 3) == 0.0)
            {
                string controlVoltage;
                double value;
                if (Stof(ep2.Vc, 3) >= 200)
                {
                    controlVoltage = "200";
                    value = 200.0;
                }
                else
                {
                    controlVoltage = "100";
                    value = 100.0;
                }
                sep[1].Vc = value;
                ep1.Vc = controlVoltage;
                ep2.Vc = controlVoltage;
            }
        }
    }

    /// <summary>
    /// 大陸製(TA) の MC の定格電圧・制御電圧を強制設定し、直近上下位選定を可能にする。
    /// 【C原典】PropChgTAMC_epav2(Fysk00.c:5188, 改訂&lt;37&gt;)。
    /// </summary>
    /// <param name="mc">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(4 スロット)。【C原典】mcod[][3]。</param>
    /// <param name="sep">電気パラメータ(数値)。【C原典】sep[]。</param>
    public static void AdjustTaMcVoltage(MainCircuitResult mc, string[] makerCodes,
                                         NumericElectricalParameters[] sep)
    {
        ArgumentNullException.ThrowIfNull(mc);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(sep);

        MainCircuitData d = mc.Data;
        // 【C原典】MC かつ mcod[0]==TA のみ対象。
        if (!Matches(d.ReservedWord, "MC ", 3) || !Matches(Slot(makerCodes, 0), "TA ", 3))
        {
            return;
        }

        ElectricalParameters ep0 = d.ElectricalParameterSlots[0];
        ElectricalParameters ep2 = d.ElectricalParameterSlots[2];

        // 【C原典】定格電圧の強制修正(入力なし時、ep[2] から 440/220 を決定)。
        if (Stof(ep0.V2[0], 8) == 0.0)
        {
            double value;
            if (Stof(ep2.V2[0], 8) > 400)
            {
                ep2.V2[0] = "000440.0";
                value = 440.0;
            }
            else
            {
                ep2.V2[0] = "000220.0";
                value = 220.0;
            }
            sep[1].V2[0] = value;
            sep[2].V2[0] = value;
        }

        // 【C原典】制御電圧の強制修正(入力なし時、ep[2] から 200/100 を決定)。
        if (Stof(ep0.Vc, 3) == 0.0)
        {
            double value;
            if (Stof(ep2.Vc, 3) >= 200)
            {
                ep2.Vc = "200";
                value = 200.0;
            }
            else
            {
                ep2.Vc = "100";
                value = 100.0;
            }
            sep[1].Vc = value;
            sep[2].Vc = value;
        }
    }

    /// <summary>
    /// 1P2W の WHM について、子機器の負荷電圧(LV=200V)から WHM の定格電圧を 200V へ設定する。
    /// 【C原典】PropWhmFukaDenFromChild(Fysk00.c:6647, 改訂&lt;78&gt;/&lt;114&gt;/&lt;155&gt;)。
    /// </summary>
    /// <param name="whm">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="records">主回路レコード列(子検索用)。【C原典】maina。</param>
    /// <param name="sep">電気パラメータ(数値)。【C原典】sep[]。</param>
    public static void AdjustWhmFukaDenFromChild(MainCircuitResult whm,
                                                 IReadOnlyList<MainCircuitResult> records,
                                                 NumericElectricalParameters[] sep)
    {
        ArgumentNullException.ThrowIfNull(whm);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(sep);

        MainCircuitData d = whm.Data;
        // 【C原典】改訂<155> 行種 SM/M 以外は対象外。
        if (!Matches(d.LineTypeCode, "SM ", 3) && !Matches(d.LineTypeCode, "M  ", 3))
        {
            return;
        }
        // 【C原典】予約語 WH 以外は対象外。
        if (!Matches(d.ReservedWord, "WH ", 3))
        {
            return;
        }

        // 【C原典】改訂<155> WHM 1P2W(epaph2=="10" && epawr2=="20") のみ対象。
        ElectricalParameters ep0 = d.ElectricalParameterSlots[0];
        if (!Matches(Phase2(ep0), "10", 2) || !Matches(Wire2(ep0), "20", 2))
        {
            return;
        }

        // 【C原典】改訂<114> 入力値がある場合は入力値を優先(何もしない)。
        if (Stof(ep0.V2[0], 8) != 0.0)
        {
            return;
        }

        foreach (MainCircuitResult record in records)
        {
            // 【C原典】自分の datano を親に持つ子機器のみ。
            if (!Matches(record.Data.ParentSequenceNumber, whm.SequenceNumber, 3))
            {
                continue;
            }

            // 【C原典】子に LV=200V の入力あり → WHM の電圧更新。
            if (Matches(record.Data.AttachedParameter.LoadVoltage[0], "200", 3))
            {
                sep[1].V2[0] = 200.0;
                sep[2].V2[0] = 200.0;
                d.ElectricalParameterSlots[1].V2[0] = "000200.0";
                d.ElectricalParameterSlots[2].V2[0] = "000200.0";
            }
        }
    }

    // 【C原典】ep.epaph2[2] は 1 桁 ×2 の連結(Ph2[0]+Ph2[1])。
    private static string Phase2(ElectricalParameters ep) => Concat1(ep.Ph2);

    // 【C原典】ep.epawr2[2] は 1 桁 ×2 の連結(Wr2[0]+Wr2[1])。
    private static string Wire2(ElectricalParameters ep) => Concat1(ep.Wr2);

    private static string Concat1(string[] pair)
    {
        string first = pair.Length > 0 ? pair[0] ?? string.Empty : string.Empty;
        string second = pair.Length > 1 ? pair[1] ?? string.Empty : string.Empty;
        return (first.Length > 0 ? first[..1] : " ") + (second.Length > 0 ? second[..1] : " ");
    }

    private static double Stof(string? value, int size) => EquipmentParameterFormatter.Stof(value, size);

    private static string Slot(string[] codes, int index)
    {
        string value = index < codes.Length ? codes[index] ?? string.Empty : string.Empty;
        return value.PadRight(3)[..3];
    }

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
