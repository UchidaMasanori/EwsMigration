using System.Globalization;
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
    ///       CSDT/SSW/TSW/TS/FL/LSW/DSW/VS/AS/VM/LA/L/MCFR/MCSD/MCFRSD/MGFR/MGSD/MGFRSD/
    ///       DCSIR/DCNI/TSU/SSWU/PBSU/COSU/2COSU/OLU/CON/NHMB/CR。
    ///
    /// 未収録(後続増分・記録列/物件/未移植リーフ依存):
    ///   ・回路電気値 kpa* も再設定する RTR/PLTR(=<see cref="UpperParameterBuilder.ApplyExceptionCircuitParameters"/>)。
    ///   ・記録列参照 VT/TR。
    ///   ・物件(FYDF801)依存 VT/TR/VM。
    /// 記録列参照(親/兄弟)は list+index を受ける <see cref="SetParam_ep2(IReadOnlyList{MainCircuitResult},int)"/>
    /// で DCPW(親V2→V1複写+A2算出)・ELR(直前ZCT判定+同一ysno VC伝播)・LGR(+K数決定/エラー返却)・
    /// PLTR(親/RTR親回路電圧→V1)・MC(2次側検出/epap2Pで極数epap/子機エレメント修正)・
    /// TB(記述行/系統走査で端子台極数epap+電圧２)・
    /// WH(周波数チェック+回路要素別 V1/V2 を公称電圧変換で決定)・
    /// WL/GL/RL/OL/BL(製作仕様区分で径サイズ+直前F・TRで電圧上書き)を収録済。
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
                //   MC 数を数える等)・SetParam_ep2_epap2P に依存するため、単一レコード版では決定できない
                //   (極数決定は list+index 版 SetMc で収録)。かつ ep[2].epap は最終 FYDF806 で機器選定が
                //   実極数に上書きするため golden 非検証。単一レコード版では必ず呼ばれる V2/AC/BC のみ設定する。
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

            case "L":
                // 【C原典】case y_L: epaph2/epawr2 を相線式から設定し、リミッターは常に SP 枠扱い(spkvn='1')。
                ep2.Ph2[0] = data.CircuitPhaseCount.ToString();
                ep2.Ph2[1] = "0";
                ep2.Wr2[0] = data.CircuitWireType.ToString();
                ep2.Wr2[1] = "0";
                data.AttachedParameter.SpFutureMountKind = '1';
                break;

            case "MCFR":
                // 【C原典】case y_MCFR: SetParam_ep2_MC_V2/AC/BC。MC と同じ(極数なし)。
                SetMcVoltage2(data);
                SetMcContactA(data);
                SetMcContactB(data);
                break;

            case "MCSD":
            case "MCFRSD":
                // 【C原典】case y_MCSD / y_MCFRSD: SetParam_ep2_MC_V2 のみ。
                SetMcVoltage2(data);
                break;

            case "MGFR":
                // 【C原典】case y_MGFR: SetParam_ep2_MG_E/V2/AC/BC。
                SetMgElement(data);
                SetMgVoltage2(data);
                SetMgContactA(data);
                SetMgContactB(data);
                break;

            case "MGSD":
            case "MGFRSD":
                // 【C原典】case y_MGSD / y_MGFRSD: SetParam_ep2_MG_E/V2。
                SetMgElement(data);
                SetMgVoltage2(data);
                break;

            case "DCSIR":
            case "DCNI":
                // 【C原典】case y_DCSIR / y_DCNI: SetParam_ep2_MCB_V2 後、電圧2区分を直流 'D' に上書き。
                SetMcbVoltage2(data);
                ep2.V2Kbn = 'D';
                break;

            case "TSU":
            case "SSWU":
            case "PBSU":
            case "COSU":
            case "2COSU":
            case "OLU":
                // 【C原典】case y_TSU/SSWU/PBSU/COSU/2COSU/OLU: SetParam_ep2_TS_V2/VC。
                SetTsVoltage2(data);
                SetTsControlVoltage(data);
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

            case "NHMB":
                // 【C原典】case y_NHMB: MCB_P + MCB_V2 の後、負荷容量(Ｗ)入力があれば AT を W/V2 で算出、
                //   無く負荷電流２(A2)入力があれば ep[0].AT へ A2 を整形設定する自己完結ケース。
                SetMcbPole(data);
                SetMcbVoltage2(data);
                {
                    ElectricalParameters ep0 = data.ElectricalParameterSlots[0];
                    double load = EquipmentParameterFormatter.Stof(data.AttachedParameter.LoadCapacity, 7);
                    double w1 = EquipmentParameterFormatter.Stof(ep0.W1, 10);
                    if (w1 != 0.0 || load != 0.0)
                    {
                        double work1 = 0.0;
                        if (load != 0.0)
                        {
                            work1 = load;
                        }

                        if (w1 != 0.0)
                        {
                            work1 = w1;
                        }

                        double v20 = EquipmentParameterFormatter.Stof(ep0.V2[0], 8);
                        double work2 = v20 != 0.0
                            ? v20
                            : EquipmentParameterFormatter.Stof(ep2.V2[0], 8);
                        ep2.At = Format9(work1 / work2);
                    }
                    else if (EquipmentParameterFormatter.Stof(ep0.A2, 9) != 0.0)
                    {
                        // 【C原典】ここは ep[0].epaat へ設定する(ep[2] ではない)。
                        ep0.At = Format9(EquipmentParameterFormatter.Stof(ep0.A2, 9));
                    }
                }

                break;

            case "CR":
                // 【C原典】case y_CR(改訂<35>): 特殊予約語区分が 27A/27B/27C('3'/'4'/'5')のとき
                //   MCB_V2 + 制御電圧(VC)+ c接点数(CC=02)+ タイプ[2]=NC + 極数3桁目=2 を設定。
                if (data.SpecialReservedWordKind is '3' or '4' or '5')
                {
                    SetMcbVoltage2(data);
                    ep2.Vc = data.CircuitVoltage[0];
                    ep2.VcKbn = data.CircuitVoltageKind;
                    data.ElectricalParameterSlots[0].Cc = "02";
                    ep2.Cc = "02";
                    data.DataType[2] = "NC     ";
                    ep2.P = SetCharAt(ep2.P, 2, '2');
                }

                break;

                // その他予約語は上記 TODO(記録列/物件/未移植リーフ依存)のため未処理。
        }
    }

    /// <summary>
    /// 記録列(親レコード・同一予約語指定番号の兄弟)参照が必要な予約語を含む ep[2] 設定。
    /// 【C原典】Fyss14.c の SetParam_ep2 ディスパッチャ(maina/index を受ける版)。
    /// 現状で収録するのは DCPW(親の V2 を V1 へ複写+負荷容量から A2 算出)・
    /// ELR(直前 ZCT 判定+同一 ysno への VC 伝播)・LGR(ELR に加え K 数決定)・
    /// PLTR(親/RTR 親の回路電圧から 1 次側電圧 V1 を決定)・
    /// MC(2 次側機器検出・epap2P・同一 ysno の MC 数で極数 epap を決定)・
    /// TB(記述行/系統走査で端子台極数 epap と電圧２を決定)。
    /// 他の予約語は単一レコード版へ委譲する。
    /// 戻り値: 設計エラー(LGR の K 数が 0 または 6 以上)なら <see cref="CircuitParseError"/>、正常時 null。
    /// 【C原典】ret==2 → 呼び元が FY-632E を Error_Proc に渡す。
    /// </summary>
    /// <param name="maina">主回路エリア。【C原典】maina[]。</param>
    /// <param name="index">対象レコードの添字。【C原典】index。</param>
    /// <param name="manufacturingSpecKind">
    /// 製作仕様区分。【C原典】bukken1-&gt;com.kyo.sshiykbn。WL/GL/RL/OL/BL の径サイズ判定で
    /// 先頭 2 文字 "01"/"02" なら "025.0"、それ以外は "030.0" とする。物件情報を引数注入する。
    /// null または該当ケース以外では未使用。
    /// </param>
    public static CircuitParseError? SetParam_ep2(IReadOnlyList<MainCircuitResult> maina, int index, string? manufacturingSpecKind = null)
    {
        ArgumentNullException.ThrowIfNull(maina);
        MainCircuitData data = maina[index].Data;

        switch (data.ReservedWord)
        {
            case "DCPW":
                SetDcpw(maina, index);
                return null;

            case "ELR":
                SetElr(maina, index);
                return null;

            case "LGR":
                return SetLgr(maina, index);

            case "PLTR":
                SetPltr(maina, index);
                return null;

            case "RTR":
                SetRtr(maina, index);
                return null;

            case "MC":
                SetMc(maina, index);
                return null;

            case "TB":
                SetTb(maina, index);
                return null;

            case "WH":
                SetWh(maina, index);
                return null;

            case "WL":
            case "GL":
            case "RL":
            case "OL":
            case "BL":
                SetLampSize(maina, index, manufacturingSpecKind);
                return null;

            default:
                SetParam_ep2(data);
                return null;
        }
    }

    /// <summary>
    /// DCPW(直流電源)の ep[2] 設定。【C原典】case y_DCPW。MCB_V2 の後、親レコードの
    /// ep[2].V2[0] を自分の ep[2].V1[0] へ複写(DCPW_V1)し、負荷容量(Ｗ)入力があれば
    /// A2=W/V2 を算出して ep[2].A2 へ設定、最後に V2 区分を直流 'D' とする。
    /// </summary>
    private static void SetDcpw(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        ElectricalParameters ep0 = data.ElectricalParameterSlots[0];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        SetMcbVoltage2(data);

        // 【C原典】SetParam_ep2_DCPW_V1: 親=maina[index-(jibunno-oyano)] の ep[2].V2[0] を ep[2].V1[0] へ。
        int jibunno = AtoiC(maina[index].SequenceNumber);
        int oyano = AtoiC(data.ParentSequenceNumber);
        int parentIndex = index - (jibunno - oyano);
        if (parentIndex >= 0 && parentIndex < maina.Count)
        {
            ep2.V1[0] = maina[parentIndex].Data.ElectricalParameterSlots[2].V2[0];
        }

        // 【C原典】負荷容量(Ｗ)入力(ep[0].W1 または fp.LoadCapacity)があれば A2=W/V2 を算出。
        double load = EquipmentParameterFormatter.Stof(data.AttachedParameter.LoadCapacity, 7);
        double w1 = EquipmentParameterFormatter.Stof(ep0.W1, 10);
        if (w1 != 0.0 || load != 0.0)
        {
            double work1 = 0.0;
            if (load != 0.0)
            {
                work1 = load;
            }

            if (w1 != 0.0)
            {
                work1 = w1;
            }

            double v20 = EquipmentParameterFormatter.Stof(ep0.V2[0], 8);
            double work2 = v20 != 0.0
                ? v20
                : EquipmentParameterFormatter.Stof(ep2.V2[0], 8);

            // 【C原典】改訂 1996.08.06: ep[0] でなく ep[2].epaa2 へ設定するのが正しい。
            ep2.A2 = Format9(work1 / work2);
        }

        ep2.V2Kbn = 'D';
    }

    /// <summary>
    /// ELR(漏電継電器)の ep[2] VC 設定。【C原典】case y_ELR。直前(index-1)が ZCT でなければ
    /// 直前要素の回路電圧・区分を VC に設定し、直前が ZCT で同一予約語指定番号(ysno)を持つ
    /// 他の ELR にも同じ VC を伝播する。
    /// </summary>
    private static void SetElr(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        if (index > 0 && maina[index - 1].Data.ReservedWord != "ZCT")
        {
            MainCircuitData prev = maina[index - 1].Data;
            ep2.Vc = prev.CircuitVoltage[0];
            ep2.VcKbn = prev.CircuitVoltageKind;

            // 【C原典】for(i=1;i<Pmainc;i++): i=0 は直前参照不可のため対象外(忠実再現)。
            for (int i = 1; i < maina.Count; i++)
            {
                MainCircuitData m = maina[i].Data;
                if (i != index
                    && maina[i - 1].Data.ReservedWord == "ZCT"
                    && m.ReservedWord == "ELR"
                    && m.DesignationNumber == data.DesignationNumber)
                {
                    ElectricalParameters mep2 = m.ElectricalParameterSlots[2];
                    mep2.Vc = ep2.Vc;
                    mep2.VcKbn = ep2.VcKbn;
                }
            }
        }
    }

    /// <summary>
    /// LGR(地絡継電器)の ep[2] VC・K 設定。【C原典】case y_LGR。ELR と同じ VC 設定・伝播に加え、
    /// 同一予約語指定番号(ysno)を持つ自分以外の LGR 数 j で K を決める(1→"001"/2→"002"/3～5→"005")。
    /// j が 0 または 6 以上は設計エラーとして FY-632E(記述行/桁)を返す(【C原典】return(2))。
    /// </summary>
    private static CircuitParseError? SetLgr(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        if (index > 0 && maina[index - 1].Data.ReservedWord != "ZCT")
        {
            MainCircuitData prev = maina[index - 1].Data;
            ep2.VcKbn = prev.CircuitVoltageKind;
            ep2.Vc = prev.CircuitVoltage[0];

            int j = 0;

            // 【C原典】for(i=1;i<Pmainc;i++): i=0 は直前参照不可のため対象外(忠実再現)。
            for (int i = 1; i < maina.Count; i++)
            {
                MainCircuitData m = maina[i].Data;
                if (i != index
                    && maina[i - 1].Data.ReservedWord == "ZCT"
                    && m.ReservedWord == "LGR"
                    && m.DesignationNumber == data.DesignationNumber)
                {
                    j++;
                    ElectricalParameters mep2 = m.ElectricalParameterSlots[2];
                    mep2.Vc = ep2.Vc;
                    mep2.VcKbn = ep2.VcKbn;
                }
            }

            if (j == 1)
            {
                ep2.K = "001";
            }
            else if (j == 2)
            {
                ep2.K = "002";
            }
            else if (j is >= 3 and <= 5)
            {
                ep2.K = "005";
            }
            else
            {
                // 【C原典】return(2): 呼び元が記述行/桁で FY-632E を出力する。
                return new CircuitParseError(
                    "FY-632E",
                    EquipmentParameterFormatter.Stoi(data.DescriptionRow, 3),
                    EquipmentParameterFormatter.Stoi(data.DescriptionColumn, 3),
                    "FYMEE80");
            }
        }

        return null;
    }

    /// <summary>
    /// PLTR(パイロットランプ用変圧器)の ep[2] V1(1 次側電圧)設定。
    /// 【C原典】case y_PLTR → SetParam_ep2_RTR_V1。親レコード(親が RTR なら更にその親)の
    /// 回路電圧 kv0 に応じて 1 次側電圧 V1[0] を決め(kv0&gt;105→"200"/以下→"100"、
    /// PLTR かつ kv0&gt;=380→"400")、VC 区分に計器 1 次側電圧区分(kpakv1kb)を設定する。
    /// </summary>
    private static void SetPltr(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        // 【C原典】i=自分の追番, j=親追番。親=maina[index-(i-j)]。
        int i = AtoiC(maina[index].SequenceNumber);
        int j = AtoiC(data.ParentSequenceNumber);

        // 【C原典】改訂<39>: 親機器が RTR なら、その RTR の親機器を対象にする。
        int tmp = 0;
        int parentIndex = index - (i - j);
        if (parentIndex >= 0 && parentIndex < maina.Count
            && maina[parentIndex].Data.ReservedWord == "RTR")
        {
            tmp = i - j;
            i = AtoiC(maina[parentIndex].SequenceNumber);
            j = AtoiC(maina[parentIndex].Data.ParentSequenceNumber);
        }

        // 【C原典】kv0=対象要素(Smaina-tmp-(i-j))の回路電圧 kpav[0]。
        int kvIndex = index - tmp - (i - j);
        if (kvIndex >= 0 && kvIndex < maina.Count)
        {
            int kv0 = AtoiC(maina[kvIndex].Data.CircuitVoltage[0]);

            // 【C原典】epav1[0] はレコード初期化の "000000.0"(Fyss17/Fyss40 の零値照合と一致)前提で
            // 4 桁目に kv0>105 なら"200"、以下なら"100"を複写する。
            ep2.V1[0] = ReplaceSegment("000000.0", 3, kv0 > 105 ? "200" : "100");

            // 【C原典】95.03.20 add: kv0>=380 かつ PLTR は"400"。
            if (kv0 >= 380 && data.ReservedWord == "PLTR")
            {
                ep2.V1[0] = ReplaceSegment(ep2.V1[0], 3, "400");
            }
        }

        // 【C原典】epavckbn=kpakv1kb。
        ep2.VcKbn = data.MeterPrimaryVoltageKind;
    }

    /// <summary>
    /// RTR(計器用変流器付トランス)の ep[2] 設定。【C原典】Parm_Set_RTR(引数 Helutzu/pprmp/newpprmp 未使用)。
    /// ep[0].epav2 から定格電圧を取り出して自身の回路電圧に据え、回路相数・線式・極数・区分・周波数を親から複写する。
    /// V1 は RTR_V1(=<see cref="SetPltr"/>)、V2 は TR_V2(=<see cref="SetTrV2"/>)で決定する。
    /// </summary>
    private static void SetRtr(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        ElectricalParameters ep0 = data.ElectricalParameterSlots[0];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        // 【C原典】ep[0].epav2[k] の先頭6文字を atoi し 3 桁で自身の回路電圧に据える。
        string[] kvs =
        [
            Format3(AtoiC(ep0.V2[0].Length >= 6 ? ep0.V2[0][..6] : ep0.V2[0])),
            Format3(AtoiC(ep0.V2[1].Length >= 6 ? ep0.V2[1][..6] : ep0.V2[1])),
            Format3(AtoiC(ep0.V2[2].Length >= 6 ? ep0.V2[2][..6] : ep0.V2[2])),
        ];

        // 【C原典】親データ p=&maina[atoi(oyatno)-1]。回路情報を親から複写する。
        int pIdx = AtoiC(data.ParentSequenceNumber) - 1;
        if (pIdx >= 0 && pIdx < maina.Count)
        {
            MainCircuitData p = maina[pIdx].Data;
            data.CircuitPhaseCount = p.CircuitPhaseCount;
            data.CircuitWireType = p.CircuitWireType;
            data.CircuitVoltage[0] = kvs[0];
            data.CircuitVoltage[1] = kvs[1];
            data.CircuitVoltage[2] = kvs[2];
            data.CircuitVoltageKind = p.CircuitVoltageKind;
            data.CircuitPoleCount = p.CircuitPoleCount;
            data.CircuitFrequency = p.CircuitFrequency;
        }

        // 【C原典】SetParam_ep2_RTR_V1(=SetPltr) と SetParam_ep2_TR_V2 を呼ぶ。
        SetPltr(maina, index);
        SetTrV2(maina, index);
    }

    /// <summary>
    /// TR/RTR の 2 次側電圧(epav2)を設定する。【C原典】SetParam_ep2_TR_V2。
    /// ep[0].epaph2[1] で 1/2 電源トランスを判別し、2 電源時は同一 TR(予約語/入線番号/同一機器認識番号一致)の
    /// 前後関係で電圧格納先スロットを振り分ける。
    /// </summary>
    private static void SetTrV2(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        ElectricalParameters ep0 = data.ElectricalParameterSlots[0];

        // 【C原典】epav2[1]/[2] はレコード初期化の "000000.0" 前提でオフセット3へ複写する。
        ep2.V2[1] = "000000.0";
        ep2.V2[2] = "000000.0";

        // 【C原典】m: ep[0].epaph2[1]=='0' で 1 電源、他は 2 電源トランス。
        int m = ep0.Ph2[1] == "0" ? 1 : 2;

        int k = 0;
        if (m == 2)
        {
            // 【C原典】同一 TR(予約語/入線番号 nyuseno/同一機器認識番号 doukkno 一致)を探す。
            int t = 0;
            for (; t < maina.Count; t++)
            {
                if (t == index)
                {
                    continue;
                }

                MainCircuitData c = maina[t].Data;
                if (c.ReservedWord == data.ReservedWord
                    && c.IncomingNumber == data.IncomingNumber
                    && c.IdentityNumber == data.IdentityNumber)
                {
                    break;
                }
            }

            // 【C原典】同一 TR が自分より後方(未検出含む)なら k=0、前方なら k=1。
            k = t > index ? 0 : 1;
        }

        // 【C原典】V2 格納。epav2[x][3] に kpav を 3 桁複写する。
        if (m == 2 && k == 1)
        {
            ep2.V2[1] = ReplaceSegment(ep2.V2[1], 3, data.CircuitVoltage[0]);
            ep2.V2[2] = ReplaceSegment(ep2.V2[2], 3, data.CircuitVoltage[1]);
        }
        if (m == 2 && k == 0)
        {
            ep2.V2[0] = ReplaceSegment(ep2.V2[0], 3, data.CircuitVoltage[0]);
        }
        if (m == 1)
        {
            ep2.V2[0] = ReplaceSegment(ep2.V2[0], 3, data.CircuitVoltage[0]);
            ep2.V2[1] = ReplaceSegment(ep2.V2[1], 3, data.CircuitVoltage[1]);
            ep2.V2[2] = ReplaceSegment(ep2.V2[2], 3, data.CircuitVoltage[2]);
        }

        ep2.V2Kbn = data.CircuitVoltageKind;
    }
    /// 行種コードが TM/SM/M 系なら epap2P(2P自動選定)→未設定時 SetMcbPole(系統種別で極数)→
    /// 子機エレメント修正 PropMcChildElement、それ以外は 2 次側機器の有無で分岐する。
    /// 最後に V2/AC/BC を設定する。
    /// </summary>
    private static void SetMc(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        // 【C原典】memcmp(gyocd,"TM "/"SM "/"M  ",3): 行種コードが変圧器系。
        string gyo = data.LineTypeCode.TrimEnd();
        if (gyo is "TM" or "SM" or "M")
        {
            // 【C原典】極数の2P自動選定。設定されなければ系統種別で極数設定。
            if (SetEpap2P(maina, index) == 0)
            {
                SetMcbPole(data);
            }

            // 【C原典】改訂<6> MCの子機のエレメント数修正。
            PropMcChildElement(maina, index);
        }
        else
        {
            SetMcSecondarySideEpap(maina, index);
        }

        SetMcVoltage2(data);
        SetMcContactA(data);
        SetMcContactB(data);
    }

    /// <summary>
    /// MC の極数を条件が揃えば 2P("002")に自動選定する。【C原典】SetParam_ep2_epap2P(改訂&lt;17&gt;)。
    /// 戻り値: 0=2P設定なし / -1=2P設定済み。極数入力済み・タイプが SF/未指定以外・回路相数!='1' なら何もしない。
    /// 同一系統に TM 行があれば行種 M/SM を、無ければ SM を 2P にする(MC は 3P より 2P の使用頻度が高いため)。
    /// </summary>
    private static int SetEpap2P(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData mc = maina[index].Data;

        // 【C原典】極数入力チェック(入力ありなら終了)。
        if (mc.ElectricalParameterSlots[0].P != "000")
        {
            return 0;
        }

        // 【C原典】タイプ指定無し時 SF 自動選定。SF/空白以外は対象外。
        string type0 = mc.DataType[0].TrimEnd();
        if (type0 is not ("SF" or ""))
        {
            return 0;
        }

        // 【C原典】電源相数(1P2W/1P3W)チェック。
        if (mc.CircuitPhaseCount != '1')
        {
            return 0;
        }

        string kno = mc.SystemNumber;

        // 【C原典】同一系統に TM 行があるか。
        bool tmAri = false;
        for (int i = 0; i < maina.Count; i++)
        {
            if (maina[i].Data.SystemNumber != kno)
            {
                continue;
            }
            if (maina[i].Data.LineTypeCode.StartsWith("TM", StringComparison.Ordinal))
            {
                tmAri = true;
                break;
            }
        }

        string gyo = mc.LineTypeCode;
        if (tmAri)
        {
            // 【C原典】TM 行あり: 行種 M または SM を 2P。
            if (gyo.StartsWith("M", StringComparison.Ordinal)
                || gyo.StartsWith("SM", StringComparison.Ordinal))
            {
                mc.ElectricalParameterSlots[2].P = "002";
                return -1;
            }
        }
        else
        {
            // 【C原典】TM 行なし: 行種 SM を 2P。
            if (gyo.StartsWith("SM", StringComparison.Ordinal))
            {
                mc.ElectricalParameterSlots[2].P = "002";
                return -1;
            }
        }

        return 0;
    }

    /// <summary>
    /// MC の子機(SB/MCB/ELB)のエレメント数(ep[2].E)を負荷電圧(fpalv[0])で修正する。
    /// 【C原典】PropMcChildElement(改訂&lt;6&gt;/&lt;8&gt;/&lt;9&gt;/&lt;10&gt;/&lt;16&gt;)。
    /// 負荷電圧 200→'2'、100→'1'、000(指定なし)→親の回路電圧/極数で判定(210かつ非3P→'2'、105または3P→'1')。
    /// また改訂&lt;10&gt;で子機が 3P(ep[0].P=="003")の場合は N 相素通しとして線式・極数を '3' にする。
    /// </summary>
    private static void PropMcChildElement(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData oya = maina[index].Data;
        string oyaDatano = maina[index].SequenceNumber;

        for (int i = 0; i < maina.Count; i++)
        {
            MainCircuitData child = maina[i].Data;

            // 【C原典】親の追番 == 子の親追番。
            if (child.ParentSequenceNumber != oyaDatano)
            {
                continue;
            }

            // 【C原典】改訂<16> SB/MCB/ELB を対象。
            if (child.ReservedWord is "SB" or "MCB" or "ELB")
            {
                string fpalv0 = child.AttachedParameter.LoadVoltage[0];
                ElectricalParameters cep2 = child.ElectricalParameterSlots[2];

                if (fpalv0.StartsWith("200", StringComparison.Ordinal))
                {
                    cep2.E = "2";
                }
                else if (fpalv0.StartsWith("100", StringComparison.Ordinal))
                {
                    // 【C原典】改訂<8>。
                    cep2.E = "1";
                }
                else if (fpalv0.StartsWith("000", StringComparison.Ordinal))
                {
                    // 【C原典】改訂<8>/<9> 負荷電圧指定なし: 親の回路電圧/極数で判定。
                    if (oya.CircuitVoltage[0].StartsWith("210", StringComparison.Ordinal)
                        && !oya.ElectricalParameterSlots[2].P.StartsWith("003", StringComparison.Ordinal))
                    {
                        cep2.E = "2";
                    }
                    else if (oya.CircuitVoltage[0].StartsWith("105", StringComparison.Ordinal)
                        || oya.ElectricalParameterSlots[2].P.StartsWith("003", StringComparison.Ordinal))
                    {
                        // 【C原典】改訂<9> 電圧指定が無い時の MC3P 時は 1E 選定。
                        cep2.E = "1";
                    }
                }
            }

            // 【C原典】改訂<10> MC の子機が3P(ep[0].P=="003")の時は N 相素通し。
            if (child.ElectricalParameterSlots[0].P.StartsWith("003", StringComparison.Ordinal))
            {
                child.CircuitWireType = '3';
                child.CircuitPoleCount = '3';
            }
        }
    }

    /// <summary>
    /// MC の 2 次側機器の有無で極数(epap)を決める。【C原典】case y_MC の非変圧器系分岐。
    /// 自分の追番を親に持つ要素(または同一機器認識番号の兄弟の 2 次側)があれば「2 次側あり」。
    /// INVBP(tokkbn=='7')は "003" 固定、2 次側なしは同一 ysno の MC 数、
    /// 2 次側ありは共用時の MC 数集計で極数を決める。
    /// </summary>
    private static void SetMcSecondarySideEpap(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        string datano = maina[index].SequenceNumber;

        // 【C原典】2 次側に機器が接続されるか(自分の datano を親に持つ要素があるか)。
        bool kiki2ari = false;
        for (int i = 0; i < maina.Count; i++)
        {
            if (maina[i].Data.ParentSequenceNumber == datano)
            {
                kiki2ari = true;
                break;
            }
        }

        // 【C原典】950519: 2 次側が無くても同一機器認識番号(doukkno)の他機器の 2 次側を調べる。
        if (!kiki2ari && data.IdentityNumber != "00")
        {
            for (int i = 0; i < maina.Count && !kiki2ari; i++)
            {
                if (i == index)
                {
                    continue;
                }
                MainCircuitData m = maina[i].Data;
                if (m.IdentityNumber == data.IdentityNumber && m.ReservedWord == data.ReservedWord)
                {
                    for (int j = 0; j < maina.Count; j++)
                    {
                        if (maina[j].Data.ParentSequenceNumber == maina[i].SequenceNumber)
                        {
                            kiki2ari = true;
                            break;
                        }
                    }
                }
            }
        }

        if (data.SpecialReservedWordKind == '7')
        {
            // 【C原典】改訂<37> INVBP の M は 3 固定。
            ep2.P = "003";
        }
        else if (!kiki2ari)
        {
            // 【C原典】2 次側に機器がない。
            if (data.DesignationNumber == "00"
                || data.ElectricalParameterSlots[0].P != "000"
                || data.DesignationSuffix != ' ')
            {
                SetMcbPole(data);
            }
            else
            {
                int icnt = 0;
                for (int i = 0; i < maina.Count; i++)
                {
                    MainCircuitData m = maina[i].Data;
                    if (m.ReservedWord != "MC")
                    {
                        continue;
                    }
                    if (m.DesignationNumber == data.DesignationNumber)
                    {
                        icnt++;
                        if (string.CompareOrdinal(m.CircuitVoltage[0], "105") > 0)
                        {
                            icnt++;
                        }
                    }
                }
                if (icnt > 1)
                {
                    ep2.P = icnt.ToString("D3", CultureInfo.InvariantCulture);
                }
                else
                {
                    SetMcbPole(data);
                }
            }
        }
        else
        {
            // 【C原典】2 次側に機器がある。
            if (data.DesignationSuffix == ' ' && data.DesignationNumber != "00")
            {
                // 【C原典】共用する場合: 105 超を 2 極分として集計。
                int icnt100 = 0;
                int icnt200 = 0;
                for (int i = 0; i < maina.Count; i++)
                {
                    MainCircuitData m = maina[i].Data;
                    if (m.ReservedWord != "MC")
                    {
                        continue;
                    }
                    if (m.DesignationNumber == data.DesignationNumber)
                    {
                        if (string.CompareOrdinal(m.CircuitVoltage[0], "105") > 0)
                        {
                            icnt200++;
                        }
                        else
                        {
                            icnt100++;
                        }
                    }
                }
                int val = icnt100 + icnt200 * 2 <= 3 ? icnt100 + icnt200 * 2 : icnt100 + icnt200;
                ep2.P = val.ToString("D3", CultureInfo.InvariantCulture);
            }
            else
            {
                // 【C原典】共用しない場合。
                SetMcPole(data);
            }
        }
    }

    /// <summary>
    /// TB(端子台)の ep[2] 極数(epap)・電圧２を設定する。【C原典】case y_TB。
    /// 記述行(同一 gyo)や系統(kno)を走査して以下の優先順で極数 3 桁目を決める:
    ///   (1)直列トリップ(MCSD/MGSD/MCFRSD/MGFRSD 兄弟)あり→'6'、
    ///   (2)シャッター回路(MGSH=MG+特殊区分'1'/'2')→電源相線で 6/5/4、
    ///   (3)27A/27B/27C(CR+特殊区分'3'/'4'/'5')→2/2/3 かつ自 TB を特殊区分'6'、
    ///   (4)MC 回路 N 相共用(コメント TBKY)→1P/2P、
    ///   (5)上記以外→回路相数・線式で 2/3/3/4。
    /// 最後に回路電圧最大値を V2[0] に格納し、V2 区分を回路電圧区分とする。
    /// </summary>
    private static void SetTb(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        if (TbHasSeriesTrip(maina, index))
        {
            // 【C原典】1996.09.03: MC*SD 等の直列トリップは 6P。
            ep2.P = SetCharAt(ep2.P, 2, '6');
        }
        else if (TbFindShutterMg(maina, index, out int mgIndex, out int pIndex))
        {
            // 【C原典】改訂<33> シャッター回路の端子台極数。
            MainCircuitData p = maina[pIndex].Data;
            char mgTok = maina[mgIndex].Data.SpecialReservedWordKind;
            if (mgTok == '1')
            {
                // MGSH+(3P)
                if (p.CircuitPhaseCount == '3' && p.CircuitWireType == '3')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '6');
                }
                if (p.CircuitPhaseCount == '1' && p.CircuitWireType == '3')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '5');
                }
            }
            else if (mgTok == '2')
            {
                // MGSH+(2P)
                if (p.CircuitPhaseCount == '3' && p.CircuitWireType == '3')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '5');
                }
                if (p.CircuitPhaseCount == '1' && p.CircuitWireType == '3')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '4');
                }
            }
        }
        else if (TbFind27(maina, index, out int cr27Index))
        {
            // 【C原典】改訂<35> 27A/27B は 2P、27C は 3P。自 TB を特殊区分'6'(27*,TB)に。
            char tok = maina[cr27Index].Data.SpecialReservedWordKind;
            if (tok is '3' or '4')
            {
                ep2.P = SetCharAt(ep2.P, 2, '2');
            }
            else if (tok == '5')
            {
                ep2.P = SetCharAt(ep2.P, 2, '3');
            }

            data.SpecialReservedWordKind = '6';
        }
        else
        {
            int ret = TbCheckMcNShare(maina, index);
            if (ret != 0)
            {
                // 【C原典】改訂<36> MC 回路 N 相共用: 共用側 1P・自身 TBKY は 2P。
                if (ret == 1)
                {
                    ep2.P = SetCharAt(ep2.P, 2, '1');
                }
                else if (ret == 2)
                {
                    ep2.P = SetCharAt(ep2.P, 2, '2');
                }
            }
            else
            {
                // 【C原典】950621 基本ケース: 回路相数・線式で極数。
                if (data.CircuitPhaseCount == '1' && data.CircuitWireType == '2')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '2');
                }
                if (data.CircuitPhaseCount == '1' && data.CircuitWireType == '3')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '3');
                }
                if (data.CircuitPhaseCount == '3' && data.CircuitWireType == '3')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '3');
                }
                if (data.CircuitPhaseCount == '3' && data.CircuitWireType == '4')
                {
                    ep2.P = SetCharAt(ep2.P, 2, '4');
                }
            }
        }

        // 【C原典】V2: 回路電圧最大値を epav2[0] のオフセット 3 から 3 桁格納。
        int n = MaxVoltageIndex(data.CircuitVoltage);
        ep2.V2[0] = ReplaceSegment(ep2.V2[0], 3, data.CircuitVoltage[n]);
        ep2.V2[1] = "000000.0";
        ep2.V2[2] = "000000.0";
        ep2.V2Kbn = data.CircuitVoltageKind;
    }

    /// <summary>
    /// TB と同一記述行に直列トリップ(MCSD/MGSD/MCFRSD/MGFRSD)兄弟があるか。
    /// 【C原典】Parm_Set_TB_Chk(1996.09.03, 戻り 0=あり)。index-1 から記述行が変わるまで遡る。
    /// </summary>
    private static bool TbHasSeriesTrip(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        string gyo = maina[index].Data.DescriptionRow;
        for (int i = index - 1; i > 0; i--)
        {
            MainCircuitData m = maina[i].Data;
            if (m.DescriptionRow != gyo)
            {
                break;
            }
            if (m.ReservedWord is "MCSD" or "MGSD" or "MCFRSD" or "MGFRSD")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// TB と同一記述行に MGSH(MG+特殊区分'1'/'2')があるか。あれば同一系統の電源も探す。
    /// 【C原典】Parm_Set_TB_Chk2(改訂<33>, 戻り 0=あり)。
    /// </summary>
    private static bool TbFindShutterMg(IReadOnlyList<MainCircuitResult> maina, int index, out int mgIndex, out int pIndex)
    {
        mgIndex = 0;
        pIndex = 0;
        bool found = false;

        string gyo = maina[index].Data.DescriptionRow;
        for (int i = index - 1; i > 0; i--)
        {
            MainCircuitData m = maina[i].Data;
            if (m.DescriptionRow != gyo)
            {
                break;
            }
            if (m.ReservedWord == "MG")
            {
                // 【C原典】MG を見つけたら特殊区分に関わらず走査終了。
                if (m.SpecialReservedWordKind is '1' or '2')
                {
                    found = true;
                    mgIndex = i;
                }
                break;
            }
        }

        if (found)
        {
            string kno = maina[index].Data.SystemNumber;
            for (int i = 0; i < maina.Count; i++)
            {
                MainCircuitData m = maina[i].Data;
                if (m.ReservedWord == "P" && m.SystemNumber == kno)
                {
                    pIndex = i;
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// TB と同一記述行に 27A/27B/27C(CR+特殊区分'3'/'4'/'5')があるか。
    /// 【C原典】Parm_Set_TB_Chk3(改訂<35>, 戻り 0=あり)。
    /// </summary>
    private static bool TbFind27(IReadOnlyList<MainCircuitResult> maina, int index, out int cr27Index)
    {
        cr27Index = 0;
        string gyo = maina[index].Data.DescriptionRow;
        for (int i = index - 1; i > 0; i--)
        {
            MainCircuitData m = maina[i].Data;
            if (m.DescriptionRow != gyo)
            {
                break;
            }
            if (m.ReservedWord == "CR" && m.SpecialReservedWordKind is '3' or '4' or '5')
            {
                cr27Index = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// MC 回路 N 相共用の TB 判定。【C原典】Parm_Set_TB_Chk4(改訂<36>)。
    /// 自身のコメントが TBKY(自身は先頭 3 文字比較=C原典の strncmp3 を踏襲)なら 2、
    /// 同一記述行の他機器コメントが TBKY(4 文字)なら 1、いずれも無ければ 0。
    /// </summary>
    private static int TbCheckMcNShare(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;

        // 【C原典】strncmp(fpacm1,"TBKY",3): 自身は先頭3文字("TBK")比較。
        if (data.AttachedParameter.Comment.StartsWith("TBK", StringComparison.Ordinal))
        {
            return 2;
        }

        string gyo = data.DescriptionRow;
        for (int i = 0; i < maina.Count; i++)
        {
            MainCircuitData m = maina[i].Data;
            if (m.DescriptionRow != gyo)
            {
                continue;
            }
            if (m.AttachedParameter.Comment.StartsWith("TBKY", StringComparison.Ordinal))
            {
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// WH(電力量計)の ep[2] 設定。【C原典】Parm_Set_WH(引数 Hz/pprmp/newpprmp は未使用)。
    /// 周波数不整合(ep[0].Hz が "00" 以外かつ回路周波数と相違)なら設定せず抜ける。
    /// PH2/WR2 を回路相数・線式から設定し、回路要素(kiryoso)='3'(VT無)/'4'(VT付)で V1/V2 を決める。
    /// VT 付は上方の VT の回路電圧を、VT 無は自身の回路電圧を公称電圧変換し 0 でない最小値を採る。
    /// </summary>
    private static void SetWh(IReadOnlyList<MainCircuitResult> maina, int index)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];
        ElectricalParameters ep0 = data.ElectricalParameterSlots[0];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        // 【C原典】周波数チェック: ep[0].epahz!="00" かつ kpahz と相違なら r=1 で抜ける(設定せず)。
        if (ep0.Hz != "00" && ep0.Hz != data.CircuitFrequency)
        {
            return;
        }

        // 【C原典】PH2/WR2。
        ep2.Ph2[0] = data.CircuitPhaseCount.ToString();
        ep2.Ph2[1] = "0";
        ep2.Wr2[0] = data.CircuitWireType.ToString();
        ep2.Wr2[1] = "0";

        // 【C原典】V1。
        if (data.CircuitElement == '3')
        {
            ep2.V1[0] = "000000.0";
            ep2.V1[1] = "000000.0";
            ep2.V1[2] = "000000.0";
        }
        if (data.CircuitElement == '4')
        {
            // 【C原典】上方の VT を探す(kiryoso=='4'=VT付は VT が必ず存在する前提)。
            int vt = -1;
            for (int i = index - 1; i >= 0; i--)
            {
                if (maina[i].Data.ReservedWord == "VT")
                {
                    vt = i;
                    break;
                }
            }

            if (vt >= 0)
            {
                short[] v = ToVoltageArray(maina[vt].Data.CircuitVoltage);
                VoltageInheritance.RightAlignVoltage(v);
                VoltageInheritance.ConvertVoltage(v, v);
                int n = v[0] < v[1] && v[0] != 0 ? 0 : 1;
                n = v[n] < v[1] && v[n] != 0 ? n : 2;
                // 【C原典】epav1[0] はレコード初期化の "000000.0" 前提で 4 桁目に memcpy する。
                ep2.V1[0] = ReplaceSegment("000000.0", 3, Format3(v[n]));
                ep2.V1[1] = "000000.0";
                ep2.V1[2] = "000000.0";
            }
        }

        // 【C原典】V2。
        if (data.CircuitElement == '3')
        {
            short[] v = ToVoltageArray(data.CircuitVoltage);
            VoltageInheritance.RightAlignVoltage(v);
            VoltageInheritance.ConvertVoltage(v, v);
            int n = v[0] < v[1] && v[0] != 0 ? 0 : 1;
            n = v[n] < v[1] && v[n] != 0 ? n : 2;
            ep2.V2[0] = ReplaceSegment(ep2.V2[0], 3, Format3(v[n]));
            ep2.V2[1] = "000000.0";
            ep2.V2[2] = "000000.0";
        }
        if (data.CircuitElement == '4')
        {
            ep2.V2[0] = "000110.0";
            ep2.V2[1] = "000000.0";
            ep2.V2[2] = "000000.0";
        }

        ep2.V2Kbn = data.CircuitVoltageKind;
        ep2.Hz = data.CircuitFrequency;
    }

    /// <summary>回路電圧 3 スロット(各 3 桁)を atoi して short 配列にする。【C原典】v[i]=atoi(kpav[i])。</summary>
    private static short[] ToVoltageArray(string[] voltage) =>
        [(short)AtoiC(voltage[0]), (short)AtoiC(voltage[1]), (short)AtoiC(voltage[2])];

    /// <summary>C の <c>sprintf(buf,"%.3d",v); memcpy(dst,buf,3)</c> を再現し、先頭 3 文字を返す。</summary>
    private static string Format3(int value)
    {
        string s = value.ToString("D3", CultureInfo.InvariantCulture);
        return s.Length > 3 ? s[..3] : s;
    }

    /// <summary>
    /// WL/GL/RL/OL/BL(表示灯)の ep[2] 設定。【C原典】case y_WL/y_GL/y_RL/y_OL/y_BL。
    /// MCB_V2 の後、製作仕様区分(sshiykbn)が "01"/"02" なら径サイズ 025.0、それ以外は 030.0。
    /// 直前レコードが F かつ datatype[0]=="TR" なら回路電圧を 5(005/005.5V)に上書きする。
    /// </summary>
    private static void SetLampSize(IReadOnlyList<MainCircuitResult> maina, int index, string? manufacturingSpecKind)
    {
        MainCircuitData data = maina[index].Data;
        ElectricalParameters ep2 = data.ElectricalParameterSlots[2];

        // 【C原典】ディスパッチャ先頭の部分初期化。
        ep2.P = "000";
        ep2.V2[0] = "000000.0";

        SetMcbVoltage2(data);

        // 【C原典】改訂<13>: 支給品仕様区分 "01"/"02" は径サイズ 025.0、他は 030.0。
        string spec = manufacturingSpecKind ?? string.Empty;
        ep2.Ksize = spec.StartsWith("01", StringComparison.Ordinal) || spec.StartsWith("02", StringComparison.Ordinal)
            ? "025.0"
            : "030.0";

        // 【C原典】直前が F(ヒューズ)かつ datatype[0]=="TR" なら 5.5V 系に上書き。
        if (index > 0)
        {
            MainCircuitData prev = maina[index - 1].Data;
            if (prev.ReservedWord == "F" && prev.DataType[0].TrimEnd() == "TR")
            {
                data.CircuitVoltage[0] = "005";
                data.CircuitVoltage[1] = "000";
                data.CircuitVoltage[2] = "000";
                ep2.V2[0] = "000005.5";
            }
        }
    }

    /// <summary>
    /// C の <c>sprintf(buf,"%09.3f",v); memcpy(dst,buf,9)</c> を再現し、先頭 9 文字を返す。
    /// 整数部が 6 桁を超えて 9 文字を上回った場合は先頭 9 文字に切り詰める(memcpy 9 と同じ)。
    /// </summary>
    private static string Format9(double value)
    {
        string s = EquipmentParameterFormatter.SprintfF("%09.3f", value);
        return s.Length > 9 ? s[..9] : s;
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
