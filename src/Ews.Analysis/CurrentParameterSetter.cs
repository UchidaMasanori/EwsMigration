using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 電流に関するパラメータのセット処理(ブレーカ系)。
/// 【C原典】Fyss3G_Set_MCB / Set_ELB / Set_MMCB / Set_ELMB
///   および補助関数 Check_fyrt800 / Set_IM / PropSetELBKando(toku/sekkei/src/Fyss3G.c)、
///   Fysk0e_SetELBkando(toku/sekkei/src/Fysk0e.c)。
///
/// 主回路 1 データ(<see cref="MainCircuitResult"/>)の電気パラメータスロット
/// <c>ElectricalParameterSlots</c>[0..2](ep[0]=入力値, ep[1]=生成値, ep[2]=システム生成値)に対し、
/// 予約語別のトリップ電流(AT)/フレーム電流(AF)/感度電流(MA)/メーカー定格(AM)を設定する。
///
/// 【段階移植の範囲】
///   本増分ではブレーカ系 4 種(MCB系/ELB系/MMCB系/ELMB系)のセッタと、その依存
///   (Check_fyrt800/Set_IM/PropSetELBKando/Fysk0e_SetELBkando)のみを移植する。
///   ディスパッチャ Fyss3G_Denryuu_Parm_Set 本体、Check_fyrt812、CNS Seek 群、
///   および MC/THR/MG/WH/AM/CT/TB/CON/TR 等の他機器セッタは後続増分で移植する。
///
/// 【C 原典のバグ再現】Set_MCB 内 <c>dwork == Stof(...)</c> は代入 <c>=</c> の誤記(<c>==</c>)で
///   実質 no-op のため、AM はその時点で <c>dwork</c> が保持する値から整形される。本移植は
///   この挙動を忠実に再現する(該当箇所にコメントを付す)。C では <c>dwork</c> 未初期化経路が
///   あるが、決定性のため 0.0 初期化とする。
/// </summary>
public static class CurrentParameterSetter
{
    /// <summary>電気パラメータ設定要否を判定する。【C原典】Fyss3G_Check_fyrt800。</summary>
    /// <param name="row">対象の主回路データ。【C原典】rt800[no]。</param>
    /// <param name="parameter1SetRequired">
    /// パラメータ1設定フラグ。0:設定要 1:設定不要。【C原典】*prm1。
    /// ep[0](入力値)がいずれも初期状態(未入力)なら 1、いずれかに入力があれば 0。
    /// </param>
    /// <param name="parameter2SetRequired">
    /// パラメータ2設定フラグ。0:設定要 1:設定不要。【C原典】*prm2。
    /// 設定要(prm1=0)かつ負荷発生元(ahassei=='1')のとき 0。
    /// </param>
    public static void ComputeParameterFlags(
        MainCircuitResult row, out int parameter1SetRequired, out int parameter2SetRequired)
    {
        ArgumentNullException.ThrowIfNull(row);

        // 【C原典】*prm1 = 1; *prm2 = 1;(初期化 -> 設定不要)。
        parameter1SetRequired = 1;
        parameter2SetRequired = 1;

        ElectricalParameters ep0 = row.Data.ElectricalParameterSlots[0];

        // 【C原典】ep[0] の AT/AF/A1/A2/MA[0..3]/W1 のいずれかが初期(0)状態でなければ設定要。
        if (!MatchesZero(ep0.At, ZeroAt) ||
            !MatchesZero(ep0.Af, ZeroAt) ||
            !MatchesZero(ep0.A1, ZeroAt) ||
            !MatchesZero(ep0.A2, ZeroAt) ||
            !MatchesZero(ep0.Ma[0], ZeroMa) ||
            !MatchesZero(ep0.Ma[1], ZeroMa) ||
            !MatchesZero(ep0.Ma[2], ZeroMa) ||
            !MatchesZero(ep0.Ma[3], ZeroMa) ||
            !MatchesZero(ep0.W1, ZeroW1))
        {
            // 【C原典】*prm1 = 0;(パラメータ1 設定要)。
            parameter1SetRequired = 0;

            // 【C原典】負荷発生区分 ahassei=='1' なら *prm2 = 0。
            if (row.Data.LoadSourceKind == '1')
            {
                parameter2SetRequired = 0;
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(MCB/RMCB/CP/SB/HPSB/HSB 用)。【C原典】Fyss3G_Set_MCB。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetMcb(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // 【C原典】DOUBLE dwork;(未初期化経路あり。決定性のため 0.0 とする)。
        double dwork = 0.0;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            string work;
            if (row.Work.SetCurrent != 0.0)
            {
                // 【C原典】sprintf(work, "%09.3lf", setteii)。
                work = Format9(row.Work.SetCurrent);
            }
            else
            {
                // 【C原典】dwork = atof(denryu); sprintf(work, "%09.3lf", dwork)。
                dwork = EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);
                work = Format9(dwork);
            }

            ep[2].At = work;                       // 【C原典】memcpy(ep[2].epaat, work, 9)。
            ep[2].Af = Fix(ep[2].At, 9);           // 【C原典】memcpy(ep[2].epaaf, ep[2].epaat, 9)。

            // ---- HPSB / HSB ----
            if (MatchesSpace(dt.ReservedWord, "HSB     ") || MatchesSpace(dt.ReservedWord, "HPSB    "))
            {
                if (MatchesSpace(dt.DataType[0], "AM     "))
                {
                    // 【C原典 bug】dwork == Stof(ep[2].epaaf, 9); は == 誤記で no-op(dwork 据え置き)。
                    ep[2].Am = Format3(dwork);     // 【C原典】memcpy(ep[2].epaam, work, 3)。
                }
            }
        }

        // 【C原典】if(prm1!=0) return;
        if (parameter1SetRequired != 0)
        {
            return;
        }

        // ---- 電気パラメータ１設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】AT 設定。
            if (MatchesZero(ep[0].At, ZeroAt))
            {
                ep[1].At = Fix(ep[0].Af, 9);
            }

            // 【C原典】AF 設定。
            if (MatchesZero(ep[0].Af, ZeroAt))
            {
                ep[1].Af = MatchesZero(ep[1].At, ZeroAt)
                    ? Fix(ep[2].Af, 9)
                    : Fix(ep[1].At, 9);
            }

            // ---- HPSB / HSB(No1220: ep[0].AM 未設定時のみ) ----
            if (MatchesSpace(dt.ReservedWord, "HSB     ") || MatchesSpace(dt.ReservedWord, "HPSB    "))
            {
                if (MatchesZero(ep[0].Am, ZeroAm) && MatchesSpace(dt.DataType[0], "AM     "))
                {
                    // 【C原典 bug】dwork == Stof(ep[1].epaaf, 9); は no-op(dwork 据え置き)。
                    ep[1].Am = Format3(dwork);
                }
            }

            // ---- 電気パラメータ２再設定処理(負荷発生区分) ----
            if (dt.LoadSourceKind == '1')
            {
                ep[2].At = Fix(ep[1].At, 9);
                ep[2].Af = Fix(ep[1].Af, 9);
                ep[2].Am = Fix(ep[1].Am, 3);
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(ELB/RELB 用)。【C原典】Fyss3G_Set_ELB。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetElb(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            string work = row.Work.SetCurrent != 0.0
                ? Format9(row.Work.SetCurrent)
                : Format9(EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth));

            ep[2].At = work;                       // 【C原典】memcpy(ep[2].epaat, work, 9)。
            ep[2].Af = Fix(ep[2].At, 9);           // 【C原典】memcpy(ep[2].epaaf, ep[2].epaat, 9)。

            // 【C原典】af = atof(ep[2].epaaf); PropSetELBKando(af, no, 2)。
            double af = EquipmentParameterFormatter.Stof(ep[2].Af, 9);
            SetElbSensitivity(af, records, index, 2);
        }

        if (parameter1SetRequired != 0)
        {
            return;
        }

        // ---- 電気パラメータ１設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】AT 設定。
            if (MatchesZero(ep[0].At, ZeroAt))
            {
                ep[1].At = MatchesZero(ep[0].Af, ZeroAt)
                    ? Fix(ep[2].At, 9)
                    : Fix(ep[0].Af, 9);
            }

            // 【C原典】AF 設定。
            if (MatchesZero(ep[0].Af, ZeroAt))
            {
                ep[1].Af = MatchesZero(ep[1].At, ZeroAt)
                    ? Fix(ep[2].Af, 9)
                    : Fix(ep[1].At, 9);
            }

            // 【C原典】af = atof(ep[1].epaaf); ep[0].epama 未設定なら PropSetELBKando(af, no, 1)。
            double af = EquipmentParameterFormatter.Stof(ep[1].Af, 9);
            if (MatchesZero(ep[0].Ma[0], ZeroMa))
            {
                SetElbSensitivity(af, records, index, 1);
            }

            // ---- 電気パラメータ２再設定処理(負荷発生区分) ----
            if (dt.LoadSourceKind == '1')
            {
                ep[2].At = Fix(ep[1].At, 9);
                ep[2].Af = Fix(ep[1].Af, 9);
                ep[2].Ma[0] = Fix(ep[1].Ma[0], 4);
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(MMCB/RMMCB/NHMB 用)。【C原典】Fyss3G_Set_MMCB。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetMmcb(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            string work = row.Work.SetCurrent != 0.0
                ? Format9(row.Work.SetCurrent)
                : Format9(EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth));

            ep[2].At = work;                       // 【C原典】memcpy(ep[2].epaat, work, 9)。
            ep[2].Af = Fix(ep[2].At, 9);           // 【C原典】memcpy(ep[2].epaaf, ep[2].epaat, 9)。
        }

        if (parameter1SetRequired != 0)
        {
            return;
        }

        // ---- 電気パラメータ１設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】AT 設定。
            if (MatchesZero(ep[0].At, ZeroAt))
            {
                if (!MatchesZero(ep[0].W1, ZeroW1))
                {
                    // 【C原典】w1=1; Set_IM(no, ep[1].epaw1, w1, &denryu); ep[1].epaat=sprintf(denryu)。
                    double denryu = ComputeInductionMotorCurrent(row, ep[1].W1, 1);
                    ep[1].At = Format9(denryu);
                }
                else if (MatchesZero(ep[0].Af, ZeroAt))
                {
                    ep[1].At = Fix(ep[2].At, 9);
                }
                else
                {
                    ep[1].At = MatchesZero(ep[2].At, ZeroAt)
                        ? Fix(ep[0].Af, 9)
                        : Fix(ep[2].At, 9);
                }
            }

            // 【C原典】yoyaku != "NHMB    " のとき AF 設定(ep[1].epaaf 基準)。
            if (!MatchesSpace(dt.ReservedWord, "NHMB    "))
            {
                if (MatchesZero(ep[1].Af, ZeroAt))
                {
                    ep[1].Af = MatchesZero(ep[1].At, ZeroAt)
                        ? Fix(ep[2].Af, 9)
                        : Fix(ep[1].At, 9);
                }
            }

            // ---- 電気パラメータ２再設定処理(負荷発生区分) ----
            if (dt.LoadSourceKind == '1')
            {
                ep[2].At = Fix(ep[1].At, 9);
                ep[2].Af = Fix(ep[1].Af, 9);
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(ELMB/RELMB 用)。【C原典】Fyss3G_Set_ELMB。
    /// MMCB(W1→Set_IM)と ELB(PropSetELBKando)を組み合わせた処理。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetElmb(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            string work = row.Work.SetCurrent != 0.0
                ? Format9(row.Work.SetCurrent)
                : Format9(EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth));

            ep[2].At = work;                       // 【C原典】memcpy(ep[2].epaat, work, 9)。
            ep[2].Af = Fix(ep[2].At, 9);           // 【C原典】memcpy(ep[2].epaaf, ep[2].epaat, 9)。

            // 【C原典】af = atof(ep[2].epaaf); PropSetELBKando(af, no, 2)。
            double af = EquipmentParameterFormatter.Stof(ep[2].Af, 9);
            SetElbSensitivity(af, records, index, 2);
        }

        if (parameter1SetRequired != 0)
        {
            return;
        }

        // ---- 電気パラメータ１設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】AT 設定。
            if (MatchesZero(ep[0].At, ZeroAt))
            {
                if (!MatchesZero(ep[0].W1, ZeroW1))
                {
                    // 【C原典】w1=1; Set_IM(no, ep[1].epaw1, w1, &denryu); ep[1].epaat=sprintf(denryu)。
                    double denryu = ComputeInductionMotorCurrent(row, ep[1].W1, 1);
                    ep[1].At = Format9(denryu);
                }
                else if (MatchesZero(ep[0].Af, ZeroAt))
                {
                    ep[1].At = Fix(ep[2].At, 9);
                }
                else
                {
                    ep[1].At = MatchesZero(ep[2].At, ZeroAt)
                        ? Fix(ep[0].Af, 9)
                        : Fix(ep[2].At, 9);
                }
            }

            // 【C原典】AF 設定(ep[0].epaaf 基準)。
            if (MatchesZero(ep[0].Af, ZeroAt))
            {
                ep[1].Af = MatchesZero(ep[1].At, ZeroAt)
                    ? Fix(ep[2].Af, 9)
                    : Fix(ep[1].At, 9);
            }

            // 【C原典】af = atof(ep[1].epaaf); ep[0].epama 未設定なら PropSetELBKando(af, no, 1)。
            double af = EquipmentParameterFormatter.Stof(ep[1].Af, 9);
            if (MatchesZero(ep[0].Ma[0], ZeroMa))
            {
                SetElbSensitivity(af, records, index, 1);
            }

            // ---- 電気パラメータ２再設定処理(負荷発生区分) ----
            if (dt.LoadSourceKind == '1')
            {
                ep[2].At = Fix(ep[1].At, 9);
                ep[2].Af = Fix(ep[1].Af, 9);
                ep[2].Ma[0] = Fix(ep[1].Ma[0], 4);
            }
        }
    }

    // ---------------------------------------------------------------------
    //  補助関数
    // ---------------------------------------------------------------------

    /// <summary>
    /// 負荷種類(ＬＷ＝)からの負荷電流値算出処理。【C原典】Fyss3G_Set_IM。
    /// </summary>
    /// <param name="row">対象の主回路データ。【C原典】rt800[no]。</param>
    /// <param name="loadCapacity">負荷容量(先頭 10 桁を atof)。【C原典】lw1。</param>
    /// <param name="loadKind">パラメータタイプ(1:電動機 2:ヒータ)。【C原典】fpalw1。</param>
    /// <returns>負荷電流値。【C原典】*denryu。</returns>
    private static double ComputeInductionMotorCurrent(MainCircuitResult row, string? loadCapacity, int loadKind)
    {
        // 【C原典】memcpy(work, lw1, 10); work[10]=0; w1 = atof(work)。
        double w1 = EquipmentParameterFormatter.Stof(loadCapacity, 10);
        // 【C原典】memcpy(work, kpav[0], 3); work[3]=0; kpav = atoi(work)。
        int kpav = EquipmentParameterFormatter.Stoi(row.Data.CircuitVoltage[0], 3);
        char kpaph = row.Data.CircuitPhaseCount;

        if (loadKind == 1)
        {
            // 【C原典】fpalw1=="M "(電動機)。
            if (kpaph == '3')
            {
                if (kpav <= 220)
                {
                    return w1 >= 1000.0
                        ? Math.Pow(w1 / 1000.0, 0.948) * 4.4
                        : Math.Pow(w1 / 1000.0, 0.945) * (6.0 - 1.6 * w1 / 1000.0);
                }

                return w1 >= 1500.0
                    ? Math.Pow(w1 / 1000.0, 0.948) * 2.26
                    : Math.Pow(w1 / 1000.0, 0.948) * (3.3 - 1.25 * w1 / 1000.0);
            }

            if (kpaph == '1' && kpav <= 105)
            {
                return Math.Pow(w1 / 1000.0, 0.71) * 18.3;
            }

            return Math.Pow(w1 / 1000.0, 0.71) * 9.1;
        }

        if (loadKind == 2)
        {
            // 【C原典】fpalw1=="H "(ヒータ)。
            return kpaph == '1'
                ? w1 / kpav
                : w1 / kpav / Math.Pow(3.0, 0.5);
        }

        // 【C原典】上記いずれにも該当しない場合 *denryu は未設定。決定性のため 0 を返す。
        return 0.0;
    }

    /// <summary>
    /// 漏電ブレーカの感度電流(ＭＡ)設定。親機器(P 行)の回路相数を取得して設定する。
    /// 【C原典】PropSetELBKando(Fyss3G.c)。
    /// </summary>
    /// <param name="af">フレーム電流。【C原典】af。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="slot">設定先の電気パラメータスロット番号(1 or 2)。【C原典】epno。</param>
    private static void SetElbSensitivity(double af, IReadOnlyList<MainCircuitResult> records, int index, int slot)
    {
        MainCircuitResult row = records[index];

        // 【C原典】Fysk0f_GetOyaP(rt800, oyatno, &oya)。
        MainCircuitResult? parent =
            ParentEquipmentLocator.FindParentPRow(records, row.Data.ParentSequenceNumber);

        // 【C原典】は親 P 行が常に存在する前提(oya->dt.kpaph)。本移植では親が無ければ
        //         設定不能として何もしない(NULL 参照回避)。
        if (parent is null)
        {
            return;
        }

        // 【C原典】Fysk0e_SetELBkando(af, oya->dt.kpaph, datatype, &ep[epno])。
        ApplyElbSensitivity(af, parent.Data.CircuitPhaseCount, row.Data.DataType, row.Data.ElectricalParameterSlots[slot]);
    }

    /// <summary>
    /// 漏電ブレーカの感度電流(ＭＡ[0])を回路相数・フレーム電流・データタイプから決定する。
    /// 【C原典】Fysk0e_SetELBkando(Fysk0e.c:39)。
    /// </summary>
    /// <param name="af">フレーム容量。【C原典】af。</param>
    /// <param name="parentPhase">親の回路相数。【C原典】kpaph。'3':動力 '1':電灯。</param>
    /// <param name="dataType">データタイプ(7 種)。type[1]=="EV " なら高感度形。【C原典】type[][7]。</param>
    /// <param name="ep">設定先の電気パラメータ。【C原典】ep。</param>
    private static void ApplyElbSensitivity(double af, char parentPhase, string[] dataType, ElectricalParameters ep)
    {
        bool isEv = MatchesSpace(dataType[1], "EV ");

        if (parentPhase == '3')
        {
            // 動力回路。
            if (af <= 60.0)
            {
                ep.Ma[0] = isEv ? "0015" : "0030";
            }
            else if (af <= 100.0)
            {
                ep.Ma[0] = "0100";
            }
            else
            {
                ep.Ma[0] = "0200";
            }
        }
        else if (parentPhase == '1')
        {
            // 電灯回路。
            if (af <= 100.0)
            {
                ep.Ma[0] = isEv ? "0015" : "0030";
            }
            else
            {
                ep.Ma[0] = "0200";
            }
        }
    }

    // ---------------------------------------------------------------------
    //  共通ヘルパ
    // ---------------------------------------------------------------------

    /// <summary>通電電流値(denryu)の桁数。【C原典】sizeof(dt.denryu)=8。</summary>
    private const int DenryuWidth = 8;

    /// <summary>AT/AF/A1/A2 の初期(未設定)値。【C原典】"00000.000"(9)。</summary>
    private const string ZeroAt = "00000.000";

    /// <summary>W1 の初期(未設定)値。【C原典】"0000000.00"(10)。</summary>
    private const string ZeroW1 = "0000000.00";

    /// <summary>MA の初期(未設定)値。【C原典】"0000"(4)。</summary>
    private const string ZeroMa = "0000";

    /// <summary>AM の初期(未設定)値。【C原典】"000"(3)。</summary>
    private const string ZeroAm = "000";

    /// <summary>
    /// 電気パラメータ設定要否(先頭機器フラグ×データデッドフラグ)を判定する。
    /// 【C原典】(inpflg==1 &amp;&amp; sentflg=='1') || (inpflg==2 &amp;&amp; sentflg!='1')。
    /// </summary>
    private static bool ShouldSet(int inputFlag, char leadingEquipmentFlag) =>
        (inputFlag == 1 && leadingEquipmentFlag == '1') ||
        (inputFlag == 2 && leadingEquipmentFlag != '1');

    /// <summary>C の <c>sprintf("%09.3lf", v)</c> + <c>memcpy(dest, work, 9)</c> 相当(先頭 9 桁)。</summary>
    private static string Format9(double value) =>
        Fix(EquipmentParameterFormatter.SprintfF("%09.3f", value), 9);

    /// <summary>C の <c>sprintf("%03.0lf", v)</c> + <c>memcpy(dest, work, 3)</c> 相当(先頭 3 桁)。</summary>
    private static string Format3(double value) =>
        Fix(EquipmentParameterFormatter.SprintfF("%03.0f", value), 3);

    /// <summary>固定長フィールドへの memcpy 相当。<paramref name="width"/> 桁に切詰/末尾 NUL 埋め。</summary>
    private static string Fix(string? value, int width)
    {
        string v = value ?? string.Empty;
        return v.Length >= width ? v[..width] : v.PadRight(width, '\0');
    }

    /// <summary>C の <c>memcmp(value, expected, len)==0</c> 相当(不足分は '\0' 埋め、数値系フィールド用)。</summary>
    private static bool MatchesZero(string? value, string expected)
    {
        string v = value ?? string.Empty;
        if (v.Length < expected.Length)
        {
            v = v.PadRight(expected.Length, '\0');
        }
        return string.CompareOrdinal(v, 0, expected, 0, expected.Length) == 0;
    }

    /// <summary>C の <c>memcmp(value, expected, len)==0</c> 相当(不足分は ' ' 埋め、文字系フィールド用)。</summary>
    private static bool MatchesSpace(string? value, string expected)
    {
        string v = value ?? string.Empty;
        if (v.Length < expected.Length)
        {
            v = v.PadRight(expected.Length, ' ');
        }
        return string.CompareOrdinal(v, 0, expected, 0, expected.Length) == 0;
    }
}
