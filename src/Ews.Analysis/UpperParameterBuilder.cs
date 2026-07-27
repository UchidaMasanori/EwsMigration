using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 主回路の上流パラメータ生成(回路電気値 kpa* の設定)。
/// 【C原典】toku/sekkei/src/Fyss14.c Make_UpperParm とその補助関数。
///
/// Make_UpperParm は P 系統内の各機器について、入線(P)は ep[0] から、
/// それ以外は親の回路情報＋自機器の変換(<see cref="CircuitParameterResolver.SetCircuitParameter"/>)
/// から主回路パラメータ(<see cref="MainCircuitParameter"/>)を求め、
/// 回路電気値(<c>dt.kpa*</c>)へ書き出す。
///
/// 本クラスは決定的な補助関数を段階移植する:
///   - <see cref="TakeIncomingParameter"/> … Kairo_Init_Take(入線 ep[0] → MCPRMS)。
///   - <see cref="SetCircuitInfo"/>          … Kairo_End_Set(MCPRMS → dt.kpa*)。
/// 親相対参照(Find_Parent)・ディスパッチャ(SetParam_ep2)・統括ループ(Make_UpperParm 本体)は
/// 機器リスト全体と索引に依存するため後続の増分で移植する。
/// </summary>
public static class UpperParameterBuilder
{
    /// <summary>周波数区分1(50Hz)。【C原典】#define HZ1 50。</summary>
    public const int Hz1 = 50;

    /// <summary>周波数区分2(60Hz)。【C原典】#define HZ2 60。</summary>
    public const int Hz2 = 60;

    /// <summary>
    /// 主回路の上流パラメータ生成(統括ループのうち回路電気値 kpa* 生成部)。
    /// 【C原典】Make_UpperParm(Fyss14.c:462)の P 系統ループ。
    /// 各機器について、入線(yoyaku=="P")は ep[0] から、それ以外は親の回路情報(<see cref="FindParent"/>)
    /// ＋自機器の変換(<see cref="CircuitParameterResolver.SetCircuitParameter"/>)で主回路パラメータを求め、
    /// 回路電気値(<c>dt.kpa*</c>)へ書き出す(<see cref="SetCircuitInfo"/>)。
    ///
    /// 本増分では kpa* 生成の中核(Kairo_Init_Take / Find_Parent / Kairo_Parm_Set / Kairo_End_Set)を
    /// 配線する。以下は後続増分(TODO):
    ///   ・PropFukaDenFromChild(改訂&lt;21&gt; 子の負荷電圧200V反映)
    ///   ・SetParam_ep2(ep[2]生成 + 例外要素の回路情報再設定)
    ///   ・SetParam_Kubun(回路電圧からの負荷種類確定)
    ///   ・末尾の MC 共用ループ(SetParam_ep2)
    /// </summary>
    /// <param name="records">主回路レコード列(FYRT800 配列相当)。破壊的に kpa* を更新する。</param>
    /// <param name="frequency">回路周波数(Hz)。【C原典】Helutzu(HZ1=50/HZ2=60)。</param>
    public static void GenerateUpperParameters(IReadOnlyList<MainCircuitResult> records, int frequency)
    {
        ArgumentNullException.ThrowIfNull(records);

        // 【C原典】pprmp=&pprma; newpprmp=&newpprma; いずれもループ間で再利用される単一領域。
        var parentParam = new MainCircuitParameter();
        var ownParam = new MainCircuitParameter();

        for (int i = 0; i < records.Count; i++)
        {
            MainCircuitData data = records[i].Data;

            // 【C原典】P 系統(ksyubetu=='1')のみ処理。
            if (data.SystemKind != '1')
            {
                continue;
            }

            if (data.ReservedWord == "P")
            {
                // 【C原典】入線: Kairo_Init_Take(ep[0]→MCPRMS)＋ Kairo_End_Set(→kpa*)。
                MainCircuitParameter incoming = TakeIncomingParameter(data);
                SetCircuitInfo(data, incoming, frequency);
            }
            else
            {
                // 【C原典】PropFukaDenFromChild(改訂<21>)は後続増分(TODO)。
                // 【C原典】Find_Parent の戻り値は無視される。見つからない場合 parentParam は
                //   前回値のまま Kairo_Parm_Set に渡される(C の pprma 再利用に忠実)。
                FindParent(records, i, parentParam);
                CircuitParameterResolver.SetCircuitParameter(
                    (short)frequency, parentParam, ownParam, records, i, records.Count - i);
                SetCircuitInfo(data, ownParam, frequency);

                // 【C原典】SetParam_ep2(ep[2]生成+例外要素のkpa*再設定)/SetParam_Kubun(負荷種類)は
                //   後続増分(TODO)。
            }
        }

        // 【C原典】末尾の MC 共用ループ(SetParam_ep2)は後続増分(TODO)。
    }

    /// <summary>
    /// 入線(P)の ep[0] から主回路パラメータ(相・線式・極数・電圧・AC/DC)を取り出す。

    /// 【C原典】Kairo_Init_Take(Fyss14.c:1198)。
    /// 電圧を右詰め(<see cref="VoltageInheritance.RightAlignVoltage"/>)し、極数を
    /// <see cref="CircuitElementResolver.ResolvePole"/> で確定、左詰めで 105V/単相2線の
    /// 特例(極数=1)を適用後、再度右詰めする。
    /// </summary>
    /// <param name="data">ep[0](入線の整形済電気パラメータ)を持つ主回路データ。</param>
    /// <returns>入線の主回路パラメータ。</returns>
    public static MainCircuitParameter TakeIncomingParameter(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ElectricalParameters ep = data.ElectricalParameterSlots[0];

        var prm = new MainCircuitParameter
        {
            Phase = (short)AtoiC(ep.Ph2[0]),    // 【C原典】atoi(ep->epaph2[0])
            WireType = (short)AtoiC(ep.Wr2[0]), // 【C原典】atoi(ep->epawr2[0])
            Pole = (short)AtoiC(ep.P),          // 【C原典】atoi(ep->epap)
            AcDcKind = ep.V2Kbn == 'A' ? (short)0 : (short)1, // 【C原典】epav2kbn=='A'→0 else 1
        };

        prm.Voltage[0] = (short)AtoiC(ep.V2[0]); // 【C原典】atoi(ep->epav2[0])
        prm.Voltage[1] = (short)AtoiC(ep.V2[1]);
        prm.Voltage[2] = (short)AtoiC(ep.V2[2]);

        VoltageInheritance.RightAlignVoltage(prm.Voltage); // 【C原典】Right_Volt
        CircuitElementResolver.ResolvePole(prm);           // 【C原典】Pole_Gen
        VoltageInheritance.LeftAlignVoltage(prm.Voltage);  // 【C原典】Left_Volt

        // 【C原典】105V・単相(ph=1)・2線(wr=2)は極数=1。
        if (prm.Voltage[0] == 105 && prm.Voltage[1] == 0 && prm.Voltage[2] == 0
            && prm.Phase == 1 && prm.WireType == 2)
        {
            prm.Pole = 1;
        }

        VoltageInheritance.RightAlignVoltage(prm.Voltage); // 【C原典】Right_Volt
        return prm;
    }

    /// <summary>
    /// 主回路パラメータ(MCPRMS)から回路電気値(<c>dt.kpa*</c>)を設定する。
    /// 【C原典】Kairo_End_Set(Fyss14.c:1324)。
    /// 電圧は左詰めしてから 3 桁で格納。AC/DC 区分・周波数(Hz)を設定し、DC のときは
    /// 相/線式/周波数を 0、極数を 2 に再設定する。
    /// </summary>
    /// <param name="data">回路電気値の書き込み先。</param>
    /// <param name="prm">設定元の主回路パラメータ。</param>
    /// <param name="frequency">回路周波数(Hz)。【C原典】Helutzu(HZ1=50/HZ2=60)。</param>
    public static void SetCircuitInfo(MainCircuitData data, MainCircuitParameter prm, int frequency)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(prm);

        // 【C原典】電圧を左詰めにする(v2 コピーへ)。
        short[] v2 = [prm.Voltage[0], prm.Voltage[1], prm.Voltage[2]];
        VoltageInheritance.LeftAlignVoltage(v2);

        // 【C原典】回路情報の設定(Set_I)。
        data.CircuitPhaseCount = SetI(prm.Phase, 1, "%01d")[0];
        data.CircuitWireType = SetI(prm.WireType, 1, "%01d")[0];
        data.CircuitFrequency = SetI(frequency, 2, "%02d");
        data.CircuitPoleCount = SetI(prm.Pole, 1, "%01d")[0];
        data.CircuitVoltageKind = prm.AcDcKind != 0 ? 'D' : 'A'; // 【C原典】vkbn?'D':'A'

        data.CircuitVoltage[0] = SetI(v2[0], 3, "%03d");
        data.CircuitVoltage[1] = SetI(v2[1], 3, "%03d");
        data.CircuitVoltage[2] = SetI(v2[2], 3, "%03d");

        // 【C原典】改訂<14>: VM の回路情報(親が Fuse の時)は前後機器(Smaina-1/-2)参照のため
        // 機器リストと索引が必要。Make_UpperParm 本体配線時に移植する(TODO)。

        // 【C原典】DC のとき相/線式/周波数を 0、極数を 2 に再設定する。
        if (prm.AcDcKind != 0)
        {
            data.CircuitPhaseCount = '0';
            data.CircuitWireType = '0';
            data.CircuitFrequency = "00";
            data.CircuitPoleCount = '2';
            // 電圧・AC/DC 区分は上記で設定済み。
        }
    }

    /// <summary>
    /// 親データ(親機器)を遡って検索し、その回路電気値(kpa*)を主回路パラメータへ取り出す。
    /// 【C原典】Find_Parent(Fyss14.c:1249)。
    /// 現機器の親データ追番(<c>dt.oyatno</c>)と一致するデータ追番(<c>datano</c>)を、
    /// 手前(<paramref name="index"/>-1 …0)へ遡って探す。見つかれば親の kpa* を
    /// <paramref name="output"/> へ設定し右詰めする。
    /// 予約語が VT/F 以外で回路要素が '4'(計器用回路 VT 付)の場合は電圧を 110/0/0 に上書きする。
    /// </summary>
    /// <param name="records">主回路レコード列(FYRT800 配列相当)。</param>
    /// <param name="index">現機器のインデックス。</param>
    /// <param name="output">親の回路情報の格納先(見つかったときのみ更新)。</param>
    /// <returns>親が見つかれば true、見つからなければ false。</returns>
    public static bool FindParent(IReadOnlyList<MainCircuitResult> records, int index, MainCircuitParameter output)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(output);

        MainCircuitResult current = records[index];
        string oyatno = current.Data.ParentSequenceNumber; // 【C原典】Smaina->dt.oyatno

        // 【C原典】for( j=1 ; j<=indx ; j++ )：手前へ遡る。
        for (int j = 1; j <= index; j++)
        {
            MainCircuitResult candidate = records[index - j];

            // 【C原典】memcmp(Smaina->dt.oyatno, (Smaina-j)->datano, 3)==0。
            if (!Match3(oyatno, candidate.SequenceNumber))
            {
                continue;
            }

            MainCircuitData p = candidate.Data;
            output.Phase = (short)AtoiC(p.CircuitPhaseCount.ToString());    // 【C原典】atoi(kpaph)
            output.WireType = (short)AtoiC(p.CircuitWireType.ToString());  // 【C原典】atoi(kpawr)
            output.Pole = (short)AtoiC(p.CircuitPoleCount.ToString());     // 【C原典】atoi(kpap)
            output.AcDcKind = p.CircuitVoltageKind == 'A' ? (short)0 : (short)1; // 【C原典】kpavkbn=='A'→0
            output.Voltage[0] = (short)AtoiC(p.CircuitVoltage[0]);         // 【C原典】atoi(kpav[0])
            output.Voltage[1] = (short)AtoiC(p.CircuitVoltage[1]);
            output.Voltage[2] = (short)AtoiC(p.CircuitVoltage[2]);
            VoltageInheritance.RightAlignVoltage(output.Voltage);          // 【C原典】Right_Volt

            // 【C原典】940830/950512: 予約語が VT でも F でもなく回路要素=='4' なら電圧110/0/0。
            if (current.Data.ReservedWord != "VT"
                && current.Data.ReservedWord != "F"
                && current.Data.CircuitElement == '4')
            {
                output.Voltage[0] = 110;
                output.Voltage[1] = 0;
                output.Voltage[2] = 0;
                VoltageInheritance.RightAlignVoltage(output.Voltage);
            }

            return true; // 【C原典】return(TRUE)
        }

        return false; // 【C原典】return(FALSE)
    }

    /// <summary>固定 3 バイト一致(先頭 3 文字)。【C原典】memcmp(a, b, 3)==0。</summary>
    private static bool Match3(string a, string b)
    {
        string a3 = a.Length >= 3 ? a[..3] : a.PadRight(3, '\0');
        string b3 = b.Length >= 3 ? b[..3] : b.PadRight(3, '\0');
        return string.CompareOrdinal(a3, b3) == 0;
    }

    /// <summary>
    /// SHORT 値を C の書式(%0Nd)で整形し先頭 <paramref name="length"/> 文字を返す。

    /// 【C原典】Set_I(Fyss14.c:1177): sprintf(buff, format, from); strncpy(to, buff, to_length)。
    /// </summary>
    private static string SetI(int value, int length, string cFormat)
    {
        string formatted = cFormat switch
        {
            "%02d" => value.ToString("D2"),
            "%03d" => value.ToString("D3"),
            _ => value.ToString(),   // "%01d"(最小幅1)
        };

        // strncpy: 先頭 length 文字。format 側が短い場合は '\0' 埋め。
        return formatted.Length >= length ? formatted[..length] : formatted.PadRight(length, '\0');
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
