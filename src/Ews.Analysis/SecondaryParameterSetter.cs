using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 上流パラメータ生成における ep[2](システム側生成値)の設定。
/// 【C原典】toku/sekkei/src/Fyss14.c の SetParam_ep2_* 群。
///
/// 回路電気値(<c>dt.kpa*</c>=<see cref="MainCircuitData"/> の CircuitPhaseCount/CircuitWireType/
/// CircuitPoleCount/CircuitVoltage[3]/CircuitVoltageKind)から、予約語別に ep[2]
/// (3 スロット目の電気パラメータ=システム側の生成値)を決定する決定的処理を移植する。
/// Make_UpperParm(主回路上流パラメータ生成)から呼び出される。
///
/// 本クラスは単一レコード(<see cref="MainCircuitData"/>)内で完結する決定的セッタのみを収録する。
/// 次の関数は依存が未モデル化のため段階移植の後続とする:
///   ・SetParam_ep2_RTR_V1 … 親機器相対参照(datano/oyatno でレコードを遡る)・PLTR 依存。
///   ・SetParam_ep2_MC_AC / SetParam_ep2_MC_BC … INVBP(tokkbn=='7')・fp.fpalw2 依存(改訂&lt;37&gt;)。
///   ・SetParam_ep2_TR_V2 / SetParam_ep2_DCPW_V1 / SetParam_ep2_epap2P … 追加引数・改訂依存。
///   ・SetParam_ep2(ディスパッチャ) … bukken(FYDF801)・MCPRMS 依存。
/// </summary>
public static class SecondaryParameterSetter
{
    /// <summary>
    /// MCB 用 極数(Ｐ)の設定。【C原典】SetParam_ep2_MCB_P(Fyss14.c:2350)。
    /// 回路極数が '1' なら ep[2] の極数3桁目を '2'、それ以外は回路極数そのものとする。
    /// </summary>
    public static void SetMcbPole(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        char p = data.CircuitPoleCount == '1' ? '2' : data.CircuitPoleCount;
        ep2.P = SetCharAt(ep2.P, 2, p);
    }

    /// <summary>
    /// MCB 用 エレメント数(Ｅ)の設定。【C原典】SetParam_ep2_MCB_E(Fyss14.c)。
    /// ep[0] の AF/AT が "99999.999"(=定格なし)なら '0'、
    /// それ以外は回路相数・線式・極数の組合せでエレメント数を決定する。
    /// </summary>
    public static void SetMcbElement(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // epae がこの時点で '\0' のものは '0' に置き換える。【C原典】for(i=0;i<3;i++)。
        for (int i = 0; i < 3; i++)
        {
            ElectricalParameters ep = data.ElectricalParameterSlots[i];
            if (ep.E.Length == 0 || ep.E == "\0")
            {
                ep.E = "0";
            }
        }

        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        char ph = data.CircuitPhaseCount;
        char wr = data.CircuitWireType;
        char p = data.CircuitPoleCount;

        // 【C原典】memcmp(ep[0].epaat,"99999.999",9)==0 → epae='0'。
        if (data.ElectricalParameterSlots[0].At == "99999.999")
        {
            ep2.E = "0";
        }
        else if (ph == '1' && wr == '2' && p == '1')
        {
            ep2.E = "1";
        }
        else if (ph == '1' && wr == '2')
        {
            ep2.E = "2";
        }
        else if (ph == '1' && wr == '3')
        {
            ep2.E = "2";
        }
        else if (ph == '3' && wr == '3')
        {
            ep2.E = "3";
        }
        else if (ph == '3' && wr == '4')
        {
            ep2.E = "3";
        }
        else if (ph == '0' && wr == '0')
        {
            ep2.E = "2";
        }
    }

    /// <summary>
    /// MCB 用 電圧２(Ｖ２)・AC/DC 区分の設定。【C原典】SetParam_ep2_MCB_V2(Fyss14.c)。
    /// 回路電圧 3 スロットのうち最大値を ep[2] の電圧２[0] の 4 桁目以降 3 桁へ格納し、
    /// 残り 2 スロットを "000000.0"、AC/DC 区分を回路電圧区分とする。
    /// </summary>
    public static void SetMcbVoltage2(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        int n = MaxVoltageIndex(data.CircuitVoltage);
        // 【C原典】memcpy(&ep[2].epav2[0][3], kpav[n], 3)。
        ep2.V2[0] = ReplaceSegment(ep2.V2[0], 3, data.CircuitVoltage[n]);
        ep2.V2[1] = "000000.0";
        ep2.V2[2] = "000000.0";
        ep2.V2Kbn = data.CircuitVoltageKind;
    }

    /// <summary>
    /// MC 用 極数(Ｐ)の設定。【C原典】SetParam_ep2_MC_P(Fyss14.c, 950518)。
    /// 回路電圧[0] が 105 超なら 3 桁目 '2'、以下なら '1'。
    /// </summary>
    public static void SetMcPole(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        int kv0 = AtoiC(data.CircuitVoltage[0]);
        ep2.P = SetCharAt(ep2.P, 2, kv0 > 105 ? '2' : '1');
    }

    /// <summary>MC 用 電圧２の設定。【C原典】SetParam_ep2_MC_V2 = SetParam_ep2_MCB_V2。</summary>
    public static void SetMcVoltage2(MainCircuitData data) => SetMcbVoltage2(data);

    /// <summary>
    /// MC 用 ａ接点数(ＡＣ)の設定。【C原典】SetParam_ep2_MC_AC(改訂&lt;37&gt;)。
    /// INVBP の MC(dt.tokkbn=='7')は負荷容量(fp.fpalw2)が 2.2KW 以下なら "01"、超なら "02"、
    /// それ以外(非 INVBP)は "00"。特注区分 tokkbn が未モデルのため非 INVBP 経路 "00" を採る
    /// (INVBP の負荷容量分岐は tokkbn/fp.fpalw2 導入時の後続増分)。
    /// </summary>
    public static void SetMcContactA(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Ac = "00";
    }

    /// <summary>
    /// MC 用 ｂ接点数(ＢＣ)の設定。【C原典】SetParam_ep2_MC_BC(改訂&lt;37&gt;)。
    /// INVBP の MC(dt.tokkbn=='7')は負荷容量(fp.fpalw2)が 2.2KW 以下なら "01"、超なら "02"、
    /// それ以外(非 INVBP)は "00"。特注区分 tokkbn が未モデルのため非 INVBP 経路 "00" を採る。
    /// </summary>
    public static void SetMcContactB(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Bc = "00";
    }

    /// <summary>
    /// VM(電圧計)用 電圧１(Ｖ１)・電圧２(Ｖ２)・AC/DC 区分の設定。【C原典】SetParam_ep2 case y_VM。
    /// 回路要素(kiryoso)が '3'(計器用回路・VT無)/'4'(計器用回路・VT付)で分岐する。
    ///   ・V1: kiryoso=='3' は "000000.0"。kiryoso=='4' は計器１次電圧(kpakv1)が 220 以下で
    ///     "000300.0"、超で "000600.0"。V1[1]/V1[2] は "000000.0"。
    ///   ・V2: kiryoso=='3' は回路電圧最大値が 105 以下なら(datatype[1]=="VS" のとき"000300.0"改訂&lt;25&gt;・
    ///     他は"000150.0")、220 以下なら "000300.0"、それ超は初期値"000000.0"のまま。
    ///     kiryoso=='4' は "000150.0"。V2[1]/V2[2] は "000000.0"、V2区分は回路電圧区分(kpavkbn)。
    /// </summary>
    public static void SetVoltmeterParameters(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        // ---- V1(1 次側電圧) ----
        if (data.CircuitElement == '3')
        {
            ep2.V1[0] = "000000.0";
        }
        else if (data.CircuitElement == '4')
        {
            // 【C原典】memcpy(buf,kpakv1,3);buf[3]='\0';atoi(buf)。
            int kv1 = AtoiC(data.MeterPrimaryVoltage);
            ep2.V1[0] = kv1 <= 220 ? "000300.0" : "000600.0";
        }

        ep2.V1[1] = "000000.0";
        ep2.V1[2] = "000000.0";

        // ---- V2(2 次側電圧) ----
        if (data.CircuitElement == '3')
        {
            int n = MaxVoltageIndex(data.CircuitVoltage);
            int v = AtoiC(data.CircuitVoltage[n]);
            if (v <= 105)
            {
                // 【C原典】改訂<25>: strncmp(datatype[1],"VS ",3)==0 で "000300.0"、他は "000150.0"。
                bool isVs = data.DataType.Length > 1 && data.DataType[1].TrimEnd() == "VS";
                ep2.V2[0] = isVs ? "000300.0" : "000150.0";
            }
            else if (v <= 220)
            {
                ep2.V2[0] = "000300.0";
            }

            // v>220 のときは初期値 "000000.0" のまま。
        }
        else if (data.CircuitElement == '4')
        {
            ep2.V2[0] = "000150.0";
        }

        ep2.V2[1] = "000000.0";
        ep2.V2[2] = "000000.0";
        ep2.V2Kbn = data.CircuitVoltageKind;
    }

    /// <summary>MG 用 エレメント数(Ｅ)の設定。【C原典】SetParam_ep2_MG_E。常に '2'。</summary>
    public static void SetMgElement(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].E = "2";
    }

    /// <summary>MG 用 電圧２の設定。【C原典】SetParam_ep2_MG_V2 = SetParam_ep2_MCB_V2。</summary>
    public static void SetMgVoltage2(MainCircuitData data) => SetMcbVoltage2(data);

    /// <summary>MG 用 ａ接点数(ＡＣ)の設定。【C原典】SetParam_ep2_MG_AC。常に "00"。</summary>
    public static void SetMgContactA(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Ac = "00";
    }

    /// <summary>MG 用 ｂ接点数(ＢＣ)の設定。【C原典】SetParam_ep2_MG_BC。常に "00"。</summary>
    public static void SetMgContactB(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Bc = "00";
    }

    /// <summary>TS 用 電圧２の設定。【C原典】SetParam_ep2_TS_V2 = SetParam_ep2_MCB_V2(941130)。</summary>
    public static void SetTsVoltage2(MainCircuitData data) => SetMcbVoltage2(data);

    /// <summary>
    /// TS 用 制御電圧(ＶＣ)・AC/DC 区分の設定。【C原典】SetParam_ep2_TS_VC(941130)。
    /// 回路電圧 3 スロットの最大値を ep[2] の制御電圧(3 桁)へ格納し、AC/DC 区分を回路電圧区分とする。
    /// </summary>
    public static void SetTsControlVoltage(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        int n = MaxVoltageIndex(data.CircuitVoltage);
        ep2.Vc = data.CircuitVoltage[n];
        ep2.VcKbn = data.CircuitVoltageKind;
    }

    /// <summary>TS 用 ａ接点数(ＡＣ)の設定。【C原典】SetParam_ep2_TS_AC(941130)。常に "00"。</summary>
    public static void SetTsContactA(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Ac = "00";
    }

    /// <summary>TS 用 ｂ接点数(ＢＣ)の設定。【C原典】SetParam_ep2_TS_BC(941130)。常に "00"。</summary>
    public static void SetTsContactB(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Bc = "00";
    }

    /// <summary>TS 用 ｃ接点数(ＣＣ)の設定。【C原典】SetParam_ep2_TS_CC(941130)。常に "01"。</summary>
    public static void SetTsContactC(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.ElectricalParameterSlots[2].Cc = "01";
    }

    /// <summary>
    /// ep[2](システム側生成値)を予約語別に生成するディスパッチャ。
    /// 【C原典】SetParam_ep2(Fyss14.c:2872)の予約語分岐のうち、単一レコードで完結し
    /// 移植済みリーフのみで表せる自己完結ケースを収録する。
    /// 冒頭で部分設定部位(ep[2].epap/epav2[0])を初期化してから分岐する。
    /// 収録: MCB/ELB/MMCB/ELMB/RMCB系/MC/SB/THR/MG/SC/NT/RRY/MCDT/F/CP/LGT/HM/ZCT/HPSB/HSB/CKS/
    ///       CSDT/SSW/TSW/TS/FL/LSW/DSW/VS/AS/VM/LA/CON。
    ///
    /// 未収録(後続増分・記録列/物件/未移植リーフ依存):
    ///   ・回路電気値 kpa* も再設定する RTR/WL/PLTR(=<see cref="UpperParameterBuilder.ApplyExceptionCircuitParameters"/>)。
    ///   ・MC の極数 epap(2次側検出=全レコード配列走査依存。V2/AC/BC は収録済)。
    ///   ・記録列参照 WH/VT/TR/TB/LGR/ELR。
    ///   ・物件(FYDF801)依存 VT/TR/WH/VM。未移植リーフ DCPW/NHMB(計算)。
    ///
    /// 【注意】ep[2].epap/epae は暫定値で、最終 FYDF806 は後段の機器選定が選定機器の実極数・
    /// 実エレメントで上書きする(電圧 V2 は不変)。詳細は GoldenEp2ComparisonTests のクラス doc。
    /// </summary>
    /// <param name="data">ep[0]/fp/回路電気値 kpa* が設定済みの主回路データ。ep[2] を破壊的に更新する。</param>
    public static void SetParam_ep2(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        // 【C原典】部分設定する部位の初期化: memcpy(ep[2].epap,"000",3); memcpy(ep[2].epav2[0],"000000.0",8)。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        switch (data.ReservedWord)
        {
            case "MCB":
            case "ELB":
            case "MMCB":
            case "ELMB":
            case "RMCB":
            case "RELB":
            case "RMMCB":
            case "RELMB":
                SetMcbPole(data);
                SetMcbElement(data);
                SetMcbVoltage2(data);
                break;

            case "MC":
                // 【C原典】case y_MC。epap(極数)は 2 次側検出(全レコード配列 maina の走査で同一 ysno の
                //   MC 数を数える等)・SetParam_ep2_epap2P・PropMcChildElement に依存し、単一レコードの
                //   ディスパッチャでは決定できない。かつ ep[2].epap は最終 FYDF806 で機器選定が実極数に
                //   上書きするため golden 非検証。C のディスパッチャ末尾で必ず呼ばれる V2/AC/BC のみ設定する。
                SetMcVoltage2(data);
                SetMcContactA(data);
                SetMcContactB(data);
                break;

            case "SB":
                // 【C原典】epap[2]='2'; epae=(kpap=='1'?'1':'2'); MCB_V2。
                ep2.P = SetCharAt(ep2.P, 2, '2');
                ep2.E = data.CircuitPoleCount == '1' ? "1" : "2";
                SetMcbVoltage2(data);
                break;

            case "THR":
                // 【C原典】epae='2'; MCB_V2。
                ep2.E = "2";
                SetMcbVoltage2(data);
                break;

            case "MG":
                SetMcbPole(data);
                SetMgElement(data);
                SetMgVoltage2(data);
                SetMgContactA(data);
                SetMgContactB(data);
                break;

            case "SC":
                // 【C原典】epaph2[0]=kpaph; epaph2[1]='0'; MCB_V2; epahz=kpahz。
                ep2.Ph2[0] = data.CircuitPhaseCount.ToString();
                ep2.Ph2[1] = "0";
                SetMcbVoltage2(data);
                ep2.Hz = data.CircuitFrequency;
                break;

            case "NT":
                SetMcbVoltage2(data);
                break;

            case "RRY":
                // 【C原典】epap[2]=kpap; MCB_V2。
                ep2.P = SetCharAt(ep2.P, 2, data.CircuitPoleCount);
                SetMcbVoltage2(data);
                break;

            case "MCDT":
                SetMcbPole(data);
                SetMcbVoltage2(data);
                break;

            case "F":
                SetMcbVoltage2(data);
                break;

            case "CP":
                // 【C原典】epap[2]='2'; MCB_V2。
                ep2.P = SetCharAt(ep2.P, 2, '2');
                SetMcbVoltage2(data);
                break;

            case "LGT":
                SetMcbPole(data);
                break;

            case "HM":
                SetMcbVoltage2(data);
                ep2.Hz = data.CircuitFrequency;
                break;

            case "ZCT":
                SetMcbVoltage2(data);
                break;

            case "HPSB":
            case "HSB":
                // 【C原典】case y_HPSB / y_HSB: SetParam_ep2_MCB_P + SetParam_ep2_MCB_V2。
                SetMcbPole(data);
                SetMcbVoltage2(data);
                break;

            case "CKS":
                SetMcbPole(data);
                SetMcbElement(data);
                SetMcbVoltage2(data);
                break;

            case "CSDT":
                SetMcbPole(data);
                SetMcbVoltage2(data);
                break;

            case "SSW":
            case "TSW":
                SetMcbPole(data);
                SetMcbVoltage2(data);
                break;

            case "TS":
                // 【C原典】case y_TS: SetParam_ep2_TS_V2/VC/AC/BC/CC(941130)。
                //   すべて回路電圧 kpav・区分 kpavkbn のみに依存する自己完結ケース。
                //   V2=最大回路電圧、VC=最大回路電圧(3桁)、AC="00"、BC="00"、CC="01"。
                SetTsVoltage2(data);
                SetTsControlVoltage(data);
                SetTsContactA(data);
                SetTsContactB(data);
                SetTsContactC(data);
                break;

            case "FL":
            case "LSW":
            case "DSW":
                SetMcbVoltage2(data);
                break;

            case "VS":
            case "AS":
                // 【C原典】epaph2[0]=kpaph; epaph2[1]='0'; epawr2[0]=kpawr; epawr2[1]='0'。
                ep2.Ph2[0] = data.CircuitPhaseCount.ToString();
                ep2.Ph2[1] = "0";
                ep2.Wr2[0] = data.CircuitWireType.ToString();
                ep2.Wr2[1] = "0";
                break;

            case "VM":
                SetVoltmeterParameters(data);
                break;

            case "LA":
                // 【C原典】epaph2/epawr2 設定 +(datatype[0]!="CT" のとき)epaqty + MCB_V2。
                ep2.Ph2[0] = data.CircuitPhaseCount.ToString();
                ep2.Ph2[1] = "0";
                ep2.Wr2[0] = data.CircuitWireType.ToString();
                ep2.Wr2[1] = "0";
                if (!(data.DataType.Length > 0 && data.DataType[0].TrimEnd() == "CT"))
                {
                    if (data.CircuitPhaseCount == '1' && data.CircuitWireType == '2')
                    {
                        ep2.Qty = '3';
                    }
                    else if (data.CircuitPhaseCount == '1' && data.CircuitWireType == '3')
                    {
                        ep2.Qty = '4';
                    }
                    else if (data.CircuitPhaseCount == '3')
                    {
                        ep2.Qty = '6';
                    }
                }

                SetMcbVoltage2(data);
                break;

            case "CON":
                // 【C原典】epap[2] を相線式から、V2 を回路電圧最大値から設定。
                if (data.CircuitPhaseCount == '1' && data.CircuitWireType == '2')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '2');
                }
                else if (data.CircuitPhaseCount == '1' && data.CircuitWireType == '3')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '3');
                }
                else if (data.CircuitPhaseCount == '3' && data.CircuitWireType == '3')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '3');
                }
                else if (data.CircuitPhaseCount == '3' && data.CircuitWireType == '4')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '4');
                }

                SetMcbVoltage2(data);
                break;

                // その他予約語は上記 TODO(記録列/物件/未移植リーフ依存)のため未処理。
        }
    }

    /// <summary>
    /// 回路電圧 3 スロットのうち最大値のインデックスを返す。
    /// 【C原典】n=((memcmp(kpav[0],kpav[1],3)&gt;0)?0:1); n=((memcmp(kpav[n],kpav[2],3)&gt;0)?n:2);。
    /// memcmp は固定 3 バイト比較のため、等長文字列の序数比較で忠実に再現する。
    /// </summary>
    private static int MaxVoltageIndex(string[] voltage)
    {
        int n = string.CompareOrdinal(voltage[0], voltage[1]) > 0 ? 0 : 1;
        n = string.CompareOrdinal(voltage[n], voltage[2]) > 0 ? n : 2;
        return n;
    }

    /// <summary>固定長文字列の指定インデックスに 1 文字を上書きする(幅は保持)。</summary>
    private static string SetCharAt(string s, int index, char c)
    {
        char[] arr = (s.Length > index ? s : s.PadRight(index + 1, '0')).ToCharArray();
        arr[index] = c;
        return new string(arr);
    }

    /// <summary>固定長文字列の指定開始位置へ部分文字列を上書きする(幅は保持)。【C原典】memcpy(&amp;dst[start],src,len)。</summary>
    private static string ReplaceSegment(string s, int start, string segment)
    {
        int required = start + segment.Length;
        char[] arr = (s.Length >= required ? s : s.PadRight(required, '0')).ToCharArray();
        for (int i = 0; i < segment.Length; i++)
        {
            arr[start + i] = segment[i];
        }
        return new string(arr);
    }

    /// <summary>先頭空白スキップ+符号+数字部のみ解釈する C の atoi 相当。【C原典】atoi。</summary>
    private static int AtoiC(string s)
    {
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
