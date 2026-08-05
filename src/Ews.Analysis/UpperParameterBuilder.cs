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
    /// 本統括では kpa* 生成の中核(Kairo_Init_Take / Find_Parent / Kairo_Parm_Set / Kairo_End_Set)に加え、
    /// ep[2]生成 + 例外要素(TR/RTR/WH/VT)の回路情報再設定を担うディスパッチャ
    /// (<see cref="SecondaryParameterSetter.SetParam_ep2(IReadOnlyList{MainCircuitResult}, int, string?, int)"/>)を配線する。
    /// 以下は後続増分(TODO):
    ///   ・SetParam_Kubun 戻り値 2(不整合)の FY-898 エラー報告
    ///   ・NT/VT/PLTR の自動生成ループ(Pre_*_Make / Mainfile_*_Make)
    /// </summary>
    /// <param name="records">主回路レコード列(FYRT800 配列相当)。破壊的に kpa*・ep[2] を更新する。</param>
    /// <param name="frequency">回路周波数(Hz)。【C原典】Helutzu(HZ1=50/HZ2=60)。</param>
    /// <param name="manufacturingSpecKind">
    /// 製作仕様区分。【C原典】bukken1-&gt;com.kyo.sshiykbn。表示灯(WL/GL/RL/OL/BL)の径サイズ判定に
    /// ディスパッチャへ引数注入する。null 時は該当ケースで未使用。
    /// </param>
    /// <returns>
    /// SetParam_ep2 が返した設計エラー(【C原典】ret==2 → FY-632E)の一覧。エラーが無ければ空。
    /// </returns>
    public static IReadOnlyList<CircuitParseError> GenerateUpperParameters(
        IReadOnlyList<MainCircuitResult> records, int frequency, string? manufacturingSpecKind = null)
    {
        ArgumentNullException.ThrowIfNull(records);

        // 【C原典】pprmp=&pprma; newpprmp=&newpprma; いずれもループ間で再利用される単一領域。
        var parentParam = new MainCircuitParameter();
        var ownParam = new MainCircuitParameter();
        var errors = new List<CircuitParseError>();

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
                // 【C原典】子の負荷電圧 200V を親(自機器)へ反映(改訂<21>)。
                PropagateLoadVoltageFromChild(records, i);

                // 【C原典】Find_Parent の戻り値は無視される。見つからない場合 parentParam は
                //   前回値のまま Kairo_Parm_Set に渡される(C の pprma 再利用に忠実)。
                FindParent(records, i, parentParam);
                CircuitParameterResolver.SetCircuitParameter(
                    (short)frequency, parentParam, ownParam, records, i, records.Count - i);
                SetCircuitInfo(data, ownParam, frequency);

                // 【C原典】SetParam_ep2: 例外要素(TR/RTR/WH/VT)の回路電気値(kpa*)を再設定し、
                //   続いて ep[2](システム側生成値)を予約語別に生成する。戻り値 2(設計エラー)は
                //   FY-632E としてエラー一覧へ収集する(C は Error_Proc へ渡す)。
                //   ※ep[2].epap/epae は回路極数 kpap からの暗定値だが、最終 FYDF806 の ep[2] は
                //     後段の機器選定(eparm_set 相当)が選定機器の実極数・実エレメントで上書きする。
                CircuitParseError? ep2Error =
                    SecondaryParameterSetter.SetParam_ep2(records, i, manufacturingSpecKind, frequency);
                if (ep2Error is not null)
                {
                    errors.Add(ep2Error);
                }

                // 【C原典】SetParam_Kubun: 回路電気値から負荷種別(fp.fpalw1)を確定・検証する。
                //   戻り値 2(不整合)の FY-898 エラー報告はエラー基盤の導入時に配線(TODO)。
                SetLoadClassification(data);
            }
        }

        // 【C原典】末尾の MC 共用ループ: MC が共用する時に極数を設定するため SetParam_ep2 を再実行する
        //   (C は dmyHelutzu/dmypprmp のダミー引数で呼ぶ。MC は Hz/製作仕様を参照しない)。
        for (int i = 0; i < records.Count; i++)
        {
            MainCircuitData data = records[i].Data;
            if (data.SystemKind == '1' && data.ReservedWord == "MC")
            {
                CircuitParseError? mcError = SecondaryParameterSetter.SetParam_ep2(records, i);
                if (mcError is not null)
                {
                    errors.Add(mcError);
                }
            }
        }

        return errors;
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

    /// <summary>
    /// 例外要素の回路電気値(kpa*)を再設定する。
    /// 【C原典】SetParam_ep2(Fyss14.c:2872)のうち<b>回路電気値を再設定する</b>分岐のうち RTR を移植:
    ///   ・RTR … Parm_Set_RTR(相/線式/極数/AC・DC/周波数は親から、電圧は自身の ep[0] 定格から)。
    /// 以下は後続増分(TODO):
    ///   ・WL/GL/RL/OL/BL の 005V 再設定 … 主に Pre_PLTR_Make(Fyss14.c:5075, PLTR生成)の後段パスが
    ///     物件施策区分(sshiykbn)・盤種類(epabn)・datatype 伝播に基づいて行う
    ///     (SetParam_ep2 の case y_WL は前段 F+datatype[0]="TR" 時のみ発火し実データでは稀)。
    ///   ・PLTR(y_PLTR)の下流 005V 再設定、その他予約語の ep[2] 生成。
    /// </summary>
    /// <param name="records">主回路レコード列。破壊的に kpa* を更新する。</param>
    /// <param name="index">対象機器のインデックス。</param>
    public static void ApplyExceptionCircuitParameters(IReadOnlyList<MainCircuitResult> records, int index)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitData dt = records[index].Data;

        switch (dt.ReservedWord)
        {
            case "RTR":
                // 【C原典】case y_RTR: Parm_Set_RTR(Fyss14.c:859)。
                ApplyRtrCircuitParameters(records, index);
                break;

                // 【C原典】case y_WL/y_GL/y_RL/y_OL/y_BL(Fyss14.c:3455)の 005V 再設定と
                //   Pre_PLTR_Make(5075)の後段パスは 工場地区(FyGetZoneCD)/bukken/ep[2]/datatype 依存のため後続増分(TODO)。
        }
    }

    /// <summary>
    /// RTR(計器用変成器 2次)の回路電気値を再設定する。
    /// 【C原典】Parm_Set_RTR(Fyss14.c:859)の回路情報設定部。
    /// 相/線式/極数/AC・DC/周波数は親データ(oyatno 直接参照)から、電圧は自身の ep[0] 定格から取る。
    /// </summary>
    private static void ApplyRtrCircuitParameters(IReadOnlyList<MainCircuitResult> records, int index)
    {
        MainCircuitData dt = records[index].Data;
        ElectricalParameters ep0 = dt.ElectricalParameterSlots[0];

        // 【C原典】定格電圧: ep[0].epav2[i] を先頭6文字 atoi → "%.3d"。
        //   epav2 は "NNNNNN.N" 形式のため atoi は小数点で停止し整数部を得る。
        string v0 = SetI(AtoiC(ep0.V2[0]), 3, "%03d");
        string v1 = SetI(AtoiC(ep0.V2[1]), 3, "%03d");
        string v2 = SetI(AtoiC(ep0.V2[2]), 3, "%03d");

        // 【C原典】親データ p = maina[atoi(oyatno)-1](datano=index+1 の位置直接参照)。
        int parentIndex = AtoiC(dt.ParentSequenceNumber) - 1;
        if (parentIndex < 0 || parentIndex >= records.Count)
        {
            return; // 親不明時は再設定しない(C は配列外参照だが安全側に倒す)。
        }

        MainCircuitData parent = records[parentIndex].Data;

        // 【C原典】回路情報: 相/線式/AC・DC/極数/周波数は親から、電圧は自身の定格から。
        dt.CircuitPhaseCount = parent.CircuitPhaseCount;
        dt.CircuitWireType = parent.CircuitWireType;
        dt.CircuitVoltage[0] = v0;
        dt.CircuitVoltage[1] = v1;
        dt.CircuitVoltage[2] = v2;
        dt.CircuitVoltageKind = parent.CircuitVoltageKind;
        dt.CircuitPoleCount = parent.CircuitPoleCount;
        dt.CircuitFrequency = parent.CircuitFrequency;

        // 【C原典】SetParam_ep2_RTR_V1 / SetParam_ep2_TR_V2(ep[2]生成)は後続増分(TODO)。
    }

    /// <summary>
    /// 子機器の負荷電圧 200V を親(自機器)の付属パラメータへ反映する。
    /// 【C原典】PropFukaDenFromChild(Fyss14.c:6766, 改訂<21>)。
    /// 自機器が B(ブレーカ)行種の MCB/ELB/SB で負荷電圧未入力(≠200)のとき、
    /// 同一行種グループ内の子機器(oyatno==自 datano)に 200V があれば自機器へコピーする。
    /// (負荷電圧は Kairo_Parm_Set が参照するため、回路電気値確定の前に呼ぶ。)
    /// </summary>
    /// <param name="records">主回路レコード列。</param>
    /// <param name="index">自機器のインデックス。</param>
    public static void PropagateLoadVoltageFromChild(IReadOnlyList<MainCircuitResult> records, int index)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitData self = records[index].Data;

        // 【C原典】gyocd が "B " 以外なら対象外。
        if (!FieldEquals(self.LineTypeCode, "B ", 2))
        {
            return;
        }

        // 【C原典】予約語が MCB/ELB(先頭3) でも SB(先頭2) でもなければ対象外。
        string y = self.ReservedWord;
        if (!FieldEquals(y, "MCB", 3) && !FieldEquals(y, "ELB", 3) && !FieldEquals(y, "SB", 2))
        {
            return;
        }

        // 【C原典】自機器の負荷電圧が既に 200V なら何もしない。
        if (FieldEquals(self.AttachedParameter.LoadVoltage[0], "200", 3))
        {
            return;
        }

        string datano = records[index].SequenceNumber; // 【C原典】oyatno=自 datano
        string gyoglno = self.LineTypeGroupNumber;   // 行種グループ番号

        for (int i = index + 1; i < records.Count; i++)
        {
            MainCircuitData child = records[i].Data;

            // 【C原典】同じ行種グループでなければ打ち切り。
            if (!Match3(gyoglno, child.LineTypeGroupNumber))
            {
                break;
            }

            // 【C原典】子機器(oyatno==自 datano)でなければスキップ。
            if (!Match3(datano, child.ParentSequenceNumber))
            {
                continue;
            }

            // 【C原典】子の負荷電圧が 200V なら自機器へコピーして打ち切り。
            if (FieldEquals(child.AttachedParameter.LoadVoltage[0], "200", 3))
            {
                self.AttachedParameter.LoadVoltage[0] = child.AttachedParameter.LoadVoltage[0];
                break;
            }
        }
    }

    /// <summary>
    /// 機器負荷種別(fp.fpalw1)を回路電気値から確定・検証する。
    /// 【C原典】SetParam_Kubun(Fyss14.c:4116)。
    /// 負荷容量(fpalw2)がある機器について、負荷種別が空なら kpa* から H/PS/M を決定し、
    /// 負荷種別と負荷単位区分(fpalwkbn)・相線式の整合を検証する(不整合は 2)。
    /// </summary>
    /// <param name="data">付属パラメータと回路電気値を持つ主回路データ。</param>
    /// <returns>0:正常 / 2:負荷種別と回路の不整合(【C原典】FY-898)。</returns>
    public static int SetLoadClassification(MainCircuitData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        AttachedParameters fp = data.AttachedParameter;

        // 【C原典】負荷容量(fpalw2)が 0 なら対象外。
        if (AtoiC(fp.LoadCapacity) == 0)
        {
            return 0;
        }

        // 【C原典】負荷種別(fpalw1)が空(先頭2文字空白)なら kpa* から決定。
        //   C は Main_Area_Clear で fpalw1="  "(空白2)だが本ドメインは空文字で未設定を表すため両者を空扱い。
        if (string.IsNullOrWhiteSpace(fp.LoadKind))
        {
            if (data.CircuitPhaseCount == '1')
            {
                fp.LoadKind = "H";
            }
            else if (AtoiC(data.CircuitVoltage[0]) == 210
                && AtoiC(data.CircuitVoltage[1]) == 210
                && AtoiC(data.CircuitVoltage[2]) == 105)
            {
                fp.LoadKind = "PS";
            }
            else
            {
                fp.LoadKind = "M";
            }
        }

        // 【C原典】負荷種別ごとの整合検証。一致した予約語は(内部条件の真偽に関わらず)
        //   最終的に 0 を返す。どの種別にも一致しない(else)場合のみ 2。
        //   ※C の内部条件(fpalwkbn/相線式の一致)は早期 return(0) するのみで、偽でも
        //     関数末尾の return(0) に落ちるため、戻り値には影響しない。
        //   ※C は memcmp(fpalw1,"H ",2)(=H+空白)等で判定するが、本ドメインの LoadKind は
        //     空白詰めしない論理値のため trim 後の完全一致で同値判定する。
        string kind = fp.LoadKind.TrimEnd();
        if (kind is "M" or "H" or "S" or "HA" or "FL" or "NA" or "YA" or "YS" or "TR")
        {
            return 0;
        }

        // 【C原典】上記のいずれの負荷種別にも一致しない("PS" 等) → 不整合。
        return 2;
    }

    /// <summary>
    /// C の strncmp(s, prefix, n)==0 相当(先頭 n バイト一致。不足は '\0' 扱い)。
    /// </summary>
    private static bool FieldEquals(string s, string prefix, int n)
    {
        for (int i = 0; i < n; i++)
        {
            char a = i < s.Length ? s[i] : '\0';
            char b = i < prefix.Length ? prefix[i] : '\0';
            if (a != b)
            {
                return false;
            }
        }

        return true;
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
