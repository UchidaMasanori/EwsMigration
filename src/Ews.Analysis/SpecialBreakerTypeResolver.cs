using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 機器検索の前処理(Fysk00_Kikisearch_SY_Sub)で行われる、回路内容記述(KkGet)を参照する
/// TS/400V/耐熱ブレーカのタイプ・メーカー・電気値補正をまとめる。
/// マスタ検索(Fysk01)より前段で、回路設計エリアの記述に基づき機器選定を調整する。
/// 【C原典】(全て toku/sekkei/src/Fysk00.c)
///   - <see cref="AdjustTsType"/>/<see cref="AdjustTsTypeControl"/>: PropChgTsType(:5681, 改訂&lt;71&gt;)
///   - <see cref="Adjust400vBreaker"/> : PropChg400vBreaker(:7759, 改訂&lt;115&gt;)
///   - <see cref="AdjustF2Breaker"/>   : PropChgF2Breaker(:7864, 改訂&lt;116&gt;)
/// </summary>
public sealed class SpecialBreakerTypeResolver
{
    private const string MtType = "MT     ";
    private const string EtType = "ET     ";

    private readonly CircuitDescriptionArea _circuitDescriptions;

    /// <param name="circuitDescriptions">回路内容記述エリア(=Fysk11_FYDF805_KkGet)。</param>
    public SpecialBreakerTypeResolver(CircuitDescriptionArea circuitDescriptions)
    {
        ArgumentNullException.ThrowIfNull(circuitDescriptions);
        _circuitDescriptions = circuitDescriptions;
    }

    /// <summary>
    /// 主回路 TS 機器で松下製(D)かつタイプ指定なしのとき、タイプ2 を MT に設定する。
    /// 【C原典】PropChgTsType(Fysk00.c:5681, 改訂&lt;71&gt;) の sk(主回路)経路。
    /// </summary>
    /// <param name="ts">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位。【C原典】mcod[][3]。</param>
    /// <param name="dataTypes">主回路ファイルの機器タイプ。【C原典】dtype[][7]。</param>
    /// <param name="displayTypes">表示用出力機器タイプ。【C原典】wtype[][7]。</param>
    public void AdjustTsType(MainCircuitResult ts, string[] makerCodes,
                             string[] dataTypes, string[] displayTypes)
    {
        ArgumentNullException.ThrowIfNull(ts);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);

        if (!Matches(ts.Data.ReservedWord, "TS ", 3))
        {
            return;
        }
        string kairoar = _circuitDescriptions.GetDescriptionAt(ts.Data.DescriptionRow, ts.Data.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;
        }
        if (ApplyTsMtType(kairoar, makerCodes, dataTypes, displayTypes))
        {
            ts.Data.DataType[1] = MtType;
        }
    }

    /// <summary>
    /// 制御 TS 機器で松下製(D)かつタイプ指定なしのとき、タイプ2 を MT に設定する。
    /// 【C原典】PropChgTsType(Fysk00.c:5681, 改訂&lt;71&gt;) の kk(制御 kikijg)経路。
    /// </summary>
    /// <param name="ts">対象の制御機器レコード。【C原典】kk。</param>
    /// <param name="makerCodes">メーカーコード選定順位。【C原典】mcod[][3]。</param>
    /// <param name="dataTypes">機器タイプ。【C原典】dtype[][7]。</param>
    /// <param name="displayTypes">表示用出力機器タイプ。【C原典】wtype[][7]。</param>
    public void AdjustTsTypeControl(ControlEquipmentInfo ts, string[] makerCodes,
                                    string[] dataTypes, string[] displayTypes)
    {
        ArgumentNullException.ThrowIfNull(ts);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);

        if (!Matches(ts.ReservedWord, "TS ", 3))
        {
            return;
        }
        string kairoar = _circuitDescriptions.GetDescriptionAt(ts.DescriptionRow, ts.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;
        }
        if (ApplyTsMtType(kairoar, makerCodes, dataTypes, displayTypes))
        {
            ts.DataType[1] = MtType;
        }
    }

    /// <summary>
    /// 400V 以上のブレーカ(MCB/ELB)について、タイプを経済型(ET)へ、メーカーを主電源/フレーム容量で調整する。
    /// 【C原典】PropChg400vBreaker(Fysk00.c:7759, 改訂&lt;115&gt;)。
    /// </summary>
    /// <param name="breaker">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="mainPowerReceiving">主電源受電フラグ。【C原典】bknm-&gt;com.mei.ko_syuden('Y':該当)。</param>
    /// <param name="makerCodes">メーカーコード選定順位。【C原典】mcod[][3]。</param>
    /// <param name="displayTypes">表示用出力機器タイプ。【C原典】wtype[][7]。</param>
    /// <returns>取得成功:true / 回路記述取得失敗:false(C原典 return -1)。</returns>
    public bool Adjust400vBreaker(MainCircuitResult breaker, char mainPowerReceiving,
                                  string[] makerCodes, string[] displayTypes)
    {
        ArgumentNullException.ThrowIfNull(breaker);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(displayTypes);

        MainCircuitData d = breaker.Data;
        // 【C原典】ブレーカ(MCB/ELB)のみ対象。
        if (!Matches(d.ReservedWord, "MCB     ", 8) && !Matches(d.ReservedWord, "ELB     ", 8))
        {
            return true;
        }

        ElectricalParameters ep1 = d.ElectricalParameterSlots[1];
        double ratedVoltage = Stof(ep1.V2[0], 8);   // 定格電圧
        double frameCurrent = Stof(ep1.Af, 9);      // フレーム電流

        string kairoar = _circuitDescriptions.GetDescriptionAt(d.DescriptionRow, d.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return false;   // 【C原典】取得失敗 return -1。
        }

        if (ratedVoltage < 400.0)
        {
            return true;
        }

        // 【C原典】タイプ指定(+()なしのとき経済型(ET)へ変更(選定順位を1段ずらす)。
        if (!kairoar.Contains("+(", StringComparison.Ordinal))
        {
            displayTypes[3] = Slot7(displayTypes, 2);
            displayTypes[2] = Slot7(displayTypes, 1);
            displayTypes[1] = Slot7(displayTypes, 0);
            d.DataType[0] = EtType;
            displayTypes[0] = EtType;
        }

        // 【C原典】メーカー指定(MK=)ありは変更しない。
        if (kairoar.Contains("MK=", StringComparison.Ordinal))
        {
            return true;
        }

        if (mainPowerReceiving == 'Y')
        {
            makerCodes[0] = "TS ";
            makerCodes[1] = "M  ";
            makerCodes[2] = "KTS";
            makerCodes[3] = "   ";
        }
        else if (frameCurrent >= 400.0)
        {
            makerCodes[0] = "M  ";
            makerCodes[1] = "KM ";
            makerCodes[2] = "   ";
            makerCodes[3] = "   ";
        }
        else
        {
            makerCodes[0] = "M  ";
            makerCodes[1] = "KTS";
            makerCodes[2] = "   ";
            makerCodes[3] = "   ";
        }

        return true;
    }

    /// <summary>
    /// 耐熱ブレーカ(MCB タイプ2=F2)でフレーム容量指定なし・225AT のとき、250AT/250AF で選定する。
    /// 三菱以外が指定されている場合は対象外。
    /// 【C原典】PropChgF2Breaker(Fysk00.c:7864, 改訂&lt;116&gt;)。
    /// </summary>
    /// <param name="breaker">対象の主回路レコード。【C原典】sk。</param>
    /// <param name="sep">電気パラメータ(数値)。【C原典】sep[]。</param>
    public void AdjustF2Breaker(MainCircuitResult breaker, NumericElectricalParameters[] sep)
    {
        ArgumentNullException.ThrowIfNull(breaker);
        ArgumentNullException.ThrowIfNull(sep);

        MainCircuitData d = breaker.Data;
        // 【C原典】耐熱ブレーカ(MCB & タイプ2=F2)以外は対象外。
        if (!Matches(d.ReservedWord, "MCB ", 4) || !Matches(d.DataType[1], "F2 ", 3))
        {
            return;
        }

        ElectricalParameters ep0 = d.ElectricalParameterSlots[0];
        // 【C原典】フレーム容量指定がある場合は入力値優先。
        if (Stof(ep0.Af, 9) != 0.0)
        {
            return;
        }
        // 【C原典】225A の場合のみ。
        if (Stof(ep0.At, 9) != 225.0)
        {
            return;
        }

        string kairoar = _circuitDescriptions.GetDescriptionAt(d.DescriptionRow, d.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;
        }

        // 【C原典】MK= の指定メーカーが三菱(M)以外なら対象外。
        string? maker = ExtractMakerCode(kairoar);
        if (maker is not null && maker != "M")
        {
            return;
        }

        sep[1].Af = 250.0;
        sep[2].Af = 250.0;
        d.ElectricalParameterSlots[1].Af = "00250.000";
        d.ElectricalParameterSlots[2].Af = "00250.000";

        sep[1].At = 250.0;
        sep[2].At = 250.0;
        d.ElectricalParameterSlots[1].At = "00250.000";
        d.ElectricalParameterSlots[2].At = "00250.000";
    }

    // 【C原典】mcod[0]=="D  " かつ "+(" 無しのとき wtype[1]/dtype[1]="MT"。datatype[1] は呼び出し側で設定。
    private static bool ApplyTsMtType(string kairoar, string[] makerCodes,
                                      string[] dataTypes, string[] displayTypes)
    {
        if (!Matches(Slot(makerCodes, 0), "D  ", 3))
        {
            return false;
        }
        if (kairoar.Contains("+(", StringComparison.Ordinal))
        {
            return false;
        }
        displayTypes[1] = MtType;
        dataTypes[1] = MtType;
        return true;
    }

    // 【C原典】strstr(kairoar,"MK=") の後、')' までを指定メーカーとして取り出す。未指定は null。
    private static string? ExtractMakerCode(string kairoar)
    {
        int keyPos = kairoar.IndexOf("MK=", StringComparison.Ordinal);
        if (keyPos < 0)
        {
            return null;
        }
        int start = keyPos + 3;
        int endPos = kairoar.IndexOf(')', start);
        if (endPos < 0)
        {
            return null;
        }
        return kairoar[start..endPos];
    }

    private static double Stof(string? value, int size) => EquipmentParameterFormatter.Stof(value, size);

    private static string Slot(string[] codes, int index)
    {
        string value = index < codes.Length ? codes[index] ?? string.Empty : string.Empty;
        return value.PadRight(3)[..3];
    }

    private static string Slot7(string[] types, int index) =>
        index < types.Length ? types[index] ?? string.Empty : string.Empty;

    // 【C原典】strncmp(a, b, width): 先頭 width バイトの一致。空白右詰めで序数比較。
    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
