using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 電流に関するパラメータのセット処理(ブレーカ系＋非ブレーカ系一部)。
/// 【C原典】Fyss3G_Set_MCB / Set_ELB / Set_MMCB / Set_ELMB / Set_THR / Set_MG / Set_WH /
///   Set_CON / Set_MCDT / Set_F / Set_ELR / Set_LGR / Set_TS / Set_SU / Set_SSW / Set_CKS / Set_L /
///   Set_TB / Set_TR / Set_RRY
///   および補助関数 Check_fyrt800 / Set_IM / PropSetELBKando(toku/sekkei/src/Fyss3G.c)、
///   Fysk0e_SetELBkando(toku/sekkei/src/Fysk0e.c)。
///
/// 主回路 1 データ(<see cref="MainCircuitResult"/>)の電気パラメータスロット
/// <c>ElectricalParameterSlots</c>[0..2](ep[0]=入力値, ep[1]=生成値, ep[2]=システム生成値)に対し、
/// 予約語別のトリップ電流(AT)/フレーム電流(AF)/感度電流(MA)/メーカー定格(AM)を設定する。
///
/// 【段階移植の範囲】
///   本クラスではブレーカ系 4 種(MCB系/ELB系/MMCB系/ELMB系)のセッタ、非ブレーカ系の
///   THR/MG/WH のセッタ、リーフセッタ群(CON/MCDT/F/ELR/LGR/TS/SU/SSW/CKS/L)、
///   および依存関数を伴うセッタ(TB は電線サイズ検索 <c>CurrentParameterTableSeeker.SeekWireSize</c>、
///   TR は下流抽出 <c>DownstreamSelector.SelectDownstream</c>、RRY は親遡行)、
///   その依存(Check_fyrt800/Set_IM/PropSetELBKando/Fysk0e_SetELBkando、
///   CNS Seek 群は <c>CurrentParameterTableSeeker</c>)を移植する。
///   ディスパッチャ Fyss3G_Denryuu_Parm_Set 本体、および MC/AM/CT 等の
///   残りの機器セッタは後続増分で移植する(Set_DCPW は C 原典が空関数のため移植省略)。
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

    /// <summary>
    /// 電流パラメータのセット処理(THR/2ERY/3ERY/4ERY 用)。【C原典】Fyss3G_Set_THR。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetThr(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】回路要素= 主回路(kiryoso=='1')のとき AT=通電電流値。
            if (dt.CircuitElement == '1')
            {
                ep[2].At = EnergizingCurrentToNine(dt.EnergizingCurrent);
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
            // 【C原典】ep[0].AT==0 かつ ep[0].W1!=0 のとき Set_IM で AT を算出。
            if (MatchesZero(ep[0].At, ZeroAt) && !MatchesZero(ep[0].W1, ZeroW1))
            {
                double denryu = ComputeInductionMotorCurrent(row, ep[1].W1, PhaseToLoadKind(dt.CircuitPhaseCount));
                ep[1].At = Format9(denryu);
            }

            // ---- 電気パラメータ２再設定処理(負荷発生区分) ----
            if (dt.LoadSourceKind == '1')
            {
                ep[2].At = Fix(ep[1].At, 9);
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(MG/MGFR/MGSD/MGFRSD 用)。【C原典】Fyss3G_Set_MG。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="ratedCurrent2Table">定格電流２設定一覧。【C原典】a2set_p。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetMg(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index,
        IReadOnlyList<RatedCurrent2Setting> ratedCurrent2Table, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(ratedCurrent2Table);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】AT=通電電流値。
            ep[2].At = EnergizingCurrentToNine(dt.EnergizingCurrent);

            // 【C原典】A2 = 通電電流値 × A2SET 係数。
            double denryu = EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);
            double kei = CurrentParameterTableSeeker.SeekRatedCurrent2Coefficient(records, index, ratedCurrent2Table);
            ep[2].A2 = Format9(denryu * kei);
        }

        // 【C原典】if(prm1!=0) return;
        if (parameter1SetRequired != 0)
        {
            return;
        }

        // ---- 電気パラメータ１設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】AT 設定(ep[1].AT==0 のとき)。
            if (MatchesZero(ep[1].At, ZeroAt))
            {
                if (!MatchesZero(ep[1].W1, ZeroW1))
                {
                    double denryu = ComputeInductionMotorCurrent(row, ep[1].W1, PhaseToLoadKind(dt.CircuitPhaseCount));
                    ep[1].At = Format9(denryu);
                }
                else
                {
                    // 【C原典】memcpy(ep[1].epaat, ep[2].epaat, 9)。
                    ep[1].At = Fix(ep[2].At, 9);
                }
            }

            // 【C原典】A2 設定(ep[1].A2==0 のとき)。
            if (MatchesZero(ep[1].A2, ZeroAt))
            {
                if (!MatchesZero(ep[1].W1, ZeroW1))
                {
                    double denryu = ComputeInductionMotorCurrent(row, ep[1].W1, PhaseToLoadKind(dt.CircuitPhaseCount));
                    ep[1].A2 = Format9(denryu);
                }
                else
                {
                    // 【C原典】memcpy(ep[1].epaa2, ep[2].epaa2, 9)。
                    ep[1].A2 = Fix(ep[2].A2, 9);
                }
            }

            // ---- 電気パラメータ２再設定処理(負荷発生区分) ----
            if (dt.LoadSourceKind == '1')
            {
                ep[2].At = Fix(ep[1].At, 9);
                ep[2].A2 = Fix(ep[1].A2, 9);
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(WH 用)。【C原典】Fyss3G_Set_WH。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="ratedCurrent1Table">定格電流１設定一覧。【C原典】a1set_p。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetWh(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index,
        IReadOnlyList<RatedCurrent1Setting> ratedCurrent1Table, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(ratedCurrent1Table);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // 【C原典】通電電流値を取得。
        double denryu = EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】A1 設定。回路要素= 主回路(kiryoso=='1')は初期化、計器用回路(CT付き)は A1SET 検索。
            ep[2].A1 = dt.CircuitElement == '1'
                ? ZeroAt
                : Format9(CurrentParameterTableSeeker.SeekRatedCurrent1(denryu, ratedCurrent1Table));

            // 【C原典】A2 設定。主回路は通電電流値で 30/120、計器用回路(CT付き)は 5 固定。
            if (dt.CircuitElement == '1')
            {
                // 【C原典】denryu<=40 は 30A、それ以外(<=150 も 150 超も)は 120A。
                ep[2].A2 = denryu <= 40.0 ? "00030.000" : "00120.000";
            }
            else
            {
                ep[2].A2 = "00005.000";
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
            // 【C原典】A1 設定。計器用回路(CT付き)かつ ep[1].A1!=0 のとき A1SET 再検索。
            if (dt.CircuitElement != '1' && !MatchesZero(ep[1].A1, ZeroAt))
            {
                ep[1].A1 = Format9(CurrentParameterTableSeeker.SeekRatedCurrent1(denryu, ratedCurrent1Table));
            }

            // 【C原典】A2 設定。主回路のみ ep[0].A2 の値で 30/120 を決定。
            if (dt.CircuitElement == '1')
            {
                double a2 = EquipmentParameterFormatter.Stof(ep[0].A2, 9);
                if (a2 <= 40.0)
                {
                    ep[1].A2 = "00030.000";
                }
                else if (a2 <= 150.0)
                {
                    ep[1].A2 = "00120.000";
                }
                // 【C原典】else 節はコメントアウト(a2>150 は据え置き)。
            }

            // ---- 電気パラメータ２再設定処理(負荷発生区分) ----
            if (dt.LoadSourceKind == '1')
            {
                ep[2].A1 = Fix(ep[1].A1, 9);
                ep[2].A2 = Fix(ep[1].A2, 9);
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(CON/ZCT 用)。【C原典】Fyss3G_Set_CON。
    /// A2 に通電電流値を設定するのみ(prm1 は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetCon(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】A2 = 通電電流値(denryu 8桁)。
            ep[2].A2 = EnergizingCurrentToNine(dt.EnergizingCurrent);
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(MCDT 用)。【C原典】Fyss3G_Set_MCDT。
    /// A2 = 通電電流値 * 1.25(prm1 は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetMcdt(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】A2 = 通電電流値 * 1.25。
            double denryu = EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);
            ep[2].A2 = Format9(denryu * 1.25);
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(F 用)。【C原典】Fyss3G_Set_F。
    /// A2 は通電電流値 3A 未満なら 3A、それ以外は通電電流値そのまま(prm1 は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetF(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】denryu<3.0 は 3A、それ以外は denryu を整形。
            double denryu = EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);
            ep[2].A2 = denryu < 3.0 ? "00003.000" : Format9(denryu);
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(ELR 用)。【C原典】Fyss3G_Set_ELR。
    /// ep[0] の感度電流(MA)が未設定のとき、通電電流値 100A 以下は 30mA、超は 200mA(prm1 は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetElr(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】ep[0].MA[0] が未設定のとき、通電電流値<=100 は 30mA、それ以外は 200mA。
            if (MatchesZero(ep[0].Ma[0], ZeroMa))
            {
                double denryu = EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);
                ep[2].Ma[0] = denryu <= 100.0 ? "0030" : "0200";
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(LGR 用)。【C原典】Fyss3G_Set_LGR。
    /// ep[0] の感度電流(MA)が未設定のとき 200mA を設定(prm1 は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetLgr(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】ep[0].MA[0] が未設定のとき 200mA を設定。
            if (MatchesZero(ep[0].Ma[0], ZeroMa))
            {
                ep[2].Ma[0] = "0200";
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(TS 用)。【C原典】Fyss3G_Set_TS。
    /// A2 = 15A 固定、prm1==0 のとき ep[1].A2 = ep[0].A2。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetTs(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】A2 = 15A 固定。
            ep[2].A2 = Format9(15.0);
        }

        // 【C原典】if(prm1!=0) return;
        if (parameter1SetRequired != 0)
        {
            return;
        }

        // ---- 電気パラメータ１設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】ep[1].A2 = ep[0].A2。
            ep[1].A2 = Fix(ep[0].A2, 9);
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(PBSU/COSU/2COSU/OLU 用)。【C原典】Fyss3G_Set_SU。
    /// A2 = 1.5A 固定、prm1==0 のとき ep[1].A2 = ep[0].A2。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetSu(
        int parameter1SetRequired, IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】A2 = 1.5A 固定。
            ep[2].A2 = Format9(1.5);
        }

        // 【C原典】if(prm1!=0) return;
        if (parameter1SetRequired != 0)
        {
            return;
        }

        // ---- 電気パラメータ１設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】ep[1].A2 = ep[0].A2。
            ep[1].A2 = Fix(ep[0].A2, 9);
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(SSW/LSW/DSW/TSW 用)。【C原典】Fyss3G_Set_SSW。
    /// A2 に通電電流値を設定するのみ(prm1 は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetSsw(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】A2 = 通電電流値(denryu 8桁)。
            ep[2].A2 = EnergizingCurrentToNine(dt.EnergizingCurrent);
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(CKS 用)。【C原典】Fyss3G_Set_CKS。
    /// prm2==0 のとき、設定電流(setteii)!=0 はその値、それ以外は通電電流値を整形して A2 に設定。
    /// </summary>
    /// <param name="parameter2SetRequired">パラメータ2設定フラグ 1:on 0:off。【C原典】prm2。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetCks(
        int parameter2SetRequired, IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag) && parameter2SetRequired == 0)
        {
            // 【C原典】setteii!=0 はその値、それ以外は通電電流値を整形して A2 に設定。
            double value = row.Work.SetCurrent != 0.0
                ? row.Work.SetCurrent
                : EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);
            ep[2].A2 = Format9(value);
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(L 用)。【C原典】Fyss3G_Set_L。
    /// A2 は通電電流値 40A 未満なら 30A、それ以外は 60A(prm1 は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetL(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】denryu<40 は 30A、それ以外は 60A。
            double denryu = EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);
            ep[2].A2 = denryu < 40.0 ? "00030.000" : "00060.000";
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(TB 用)。【C原典】Fyss3G_Set_TB。
    /// A2 に通電電流値を設定し、電線サイズ(SQ)を CnsSQsetSeek で決定する。
    /// 改訂&lt;9&gt;: 動力電源(fpalwkbn=='W')かつ三相(kpaph=='3')で負荷容量帯により通電電流値を補正。
    /// 改訂&lt;7&gt;: 通電電流値が 26.669～26.876 の帯なら 30.1 に補正。LGT は電線サイズ非設定で終了。
    /// </summary>
    /// <param name="parameter1SetRequired">パラメータ1設定フラグ 0:on 1:off。【C原典】prm1。</param>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="wireSizeTable">電線サイズ設定表。【C原典】sqset_p。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetTb(
        int parameter1SetRequired,
        IReadOnlyList<MainCircuitResult> records,
        int index,
        IReadOnlyList<WireSizeSetting> wireSizeTable,
        int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(wireSizeTable);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // 【C原典】通電電流値を取得(この double は電線サイズ検索用。A2 には生の denryu 文字列を使う)。
        double denryu = EquipmentParameterFormatter.Stof(dt.EnergizingCurrent, DenryuWidth);

        // 【C原典 改訂<9>】動力電源(fpalwkbn=='W')かつ三相(kpaph=='3')は負荷容量帯で通電電流値を補正。
        if (dt.AttachedParameter.LoadUnitKind == 'W' && dt.CircuitPhaseCount == '3')
        {
            int fuka = EquipmentParameterFormatter.Stoi(dt.AttachedParameter.LoadCapacity, 7);
            if (fuka > 2200 && fuka <= 5500)
            {
                denryu = 15.1;
            }
            else if (fuka > 5500 && fuka <= 11000)
            {
                denryu = 30.1;
            }
            else if (fuka > 11000)
            {
                denryu = 71.5;
            }
        }

        // 【C原典 改訂<7>】26.669～26.876 の帯は 30.1 に補正。
        if (denryu > 26.669 && denryu < 26.876)
        {
            denryu = 30.1;
        }

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】A2 = 通電電流値(生の denryu 8桁 + '0')。
            ep[2].A2 = EnergizingCurrentToNine(dt.EnergizingCurrent);

            // 【C原典】LGT は電線サイズを設定せず終了。
            if (MatchesSpace(dt.ReservedWord, "LGT     "))
            {
                return;
            }

            // 【C原典】ep[2].SQ が未設定("000.00")なら CnsSQsetSeek で電線サイズを決定。
            if (MatchesZero(ep[2].Sq, ZeroSq))
            {
                double sq = CurrentParameterTableSeeker.SeekWireSize(denryu, wireSizeTable);
                ep[2].Sq = Format6(sq);
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
            // 【C原典】ep[1].SQ が未設定("000.00")なら CnsSQsetSeek で電線サイズを決定。
            if (MatchesZero(ep[1].Sq, ZeroSq))
            {
                double sq = CurrentParameterTableSeeker.SeekWireSize(denryu, wireSizeTable);
                ep[1].Sq = Format6(sq);
            }

            // 【C原典】負荷発生区分 ahassei=='1' のとき ep[2].SQ = ep[1].SQ。
            if (dt.LoadSourceKind == '1')
            {
                ep[2].Sq = Fix(ep[1].Sq, 6);
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(TR 用)。【C原典】Fyss3G_Set_TR。
    /// タイプ[0] 未設定時は負荷容量(VA)&lt;=500 で "RO" を設定。ep[2].VA 未設定時は下流の
    /// 負荷容量(負荷種類 M)を積算して VA を決定する(prm1/Pmainc は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetTr(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- タイプ設定処理 ----
        // 【C原典】datatype[0] が未設定(空白7)なら負荷容量(VA)を取得し 500 以下で "RO" を設定。
        if (MatchesSpace(dt.DataType[0], "       "))
        {
            // 【C原典 バグ】条件は ep[0].VA を見るが、非ゼロ時に読むのは ep[1].VA(原典どおりの挙動)。
            string vaSource = MatchesZero(ep[0].Va, ZeroVa) ? ep[2].Va : ep[1].Va;
            double va0 = EquipmentParameterFormatter.Stof(vaSource, 10);
            if (va0 <= 500.0)
            {
                dt.DataType[0] = "RO     ";
            }
        }

        // ---- 電気パラメータ２設定処理 ----
        if (ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            // 【C原典】ep[2].VA が未設定("0000000.00")のときのみ算出。
            if (MatchesZero(ep[2].Va, ZeroVa))
            {
                // 【C原典】下流データ追番を抽出(Fyss35_Select_Karyu_Sub)。lw2 は決定性のため 0.0 初期化。
                double lw2 = 0.0;
                IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(records, index + 1);
                if (downstream is not null)
                {
                    int num = downstream.Count;
                    for (int lp = 0; lp < num; lp++)
                    {
                        // 【C原典】d_no = *dno + lp(下流追番は連続するため先頭 + lp)。
                        int dno = downstream[0] + lp;
                        MainCircuitData child = records[dno - 1].Data;

                        // 【C原典】負荷発生元区分が '1' の下流のみ対象。
                        if (child.LoadSourceKind == '1')
                        {
                            // 【C原典】負荷種類が "M " のとき自身(rt800[no])の fpalw2 を積算(原典どおり自身を参照)。
                            if (MatchesSpace(child.AttachedParameter.LoadKind, "M "))
                            {
                                lw2 += EquipmentParameterFormatter.Stof(dt.AttachedParameter.LoadCapacity, 7);
                            }
                            else
                            {
                                // 【C原典】ret = lp; break;(ret は後段の代入バグで上書きされる)。
                                break;
                            }
                        }
                    }
                }

                // 【C原典 バグ】if(ret = -1) は比較 == の誤記で常に真。よって常に va = lw2 * 1.5。
                //   else 節(denryu * kpav * 1.2)は到達しない死コードのため移植しない。
                double va = lw2 * 1.5;
                ep[2].Va = Fix(EquipmentParameterFormatter.SprintfF("%10.2f", va), 10);
            }
        }
    }

    /// <summary>
    /// 電流パラメータのセット処理(RRY 用)。【C原典】Fyss3G_Set_RRY。
    /// 改訂&lt;1&gt;: LACSL リモコン(datatype[1]=="LA")は ep[1].A2 を 16A 固定。
    /// それ以外は直列上位(親)を遡り、同一階層かつ AT 設定済みの親の AT を A2 に採り、
    /// 見つからなければ通電電流値を A2 に設定する(prm1 は未使用)。
    /// </summary>
    /// <param name="records">主回路エリア。【C原典】rt800[]。</param>
    /// <param name="index">処理対象データ追番。【C原典】no。</param>
    /// <param name="inputFlag">データデッドフラグ(1 or 2)。【C原典】inpflg。</param>
    public static void SetRry(IReadOnlyList<MainCircuitResult> records, int index, int inputFlag)
    {
        ArgumentNullException.ThrowIfNull(records);
        MainCircuitResult row = records[index];
        MainCircuitData dt = row.Data;
        ElectricalParameters[] ep = dt.ElectricalParameterSlots;

        // ---- 電気パラメータ２設定処理 ----
        if (!ShouldSet(inputFlag, row.Work.LeadingEquipmentFlag))
        {
            return;
        }

        // 【C原典 改訂<1>】LACSL リモコンは ep[1].A2 を 16A 強制設定して終了。
        if (MatchesSpace(dt.DataType[1], "LA "))
        {
            ep[1].A2 = "00016.000";
            return;
        }

        // 【C原典】直列上位(親)を遡り、同一階層かつ AT 設定済みの親の AT を A2 に採る。
        int lp = index + 1;
        while (true)
        {
            int parentNumber = EquipmentParameterFormatter.Stoi(records[lp - 1].Data.ParentSequenceNumber, 3);
            if (parentNumber < 1)
            {
                break;
            }

            MainCircuitData parent = records[parentNumber - 1].Data;
            if (string.CompareOrdinal(Fix(parent.HierarchyNumber, 3), Fix(dt.HierarchyNumber, 3)) == 0 &&
                !MatchesZero(parent.ElectricalParameterSlots[0].At, ZeroAt))
            {
                ep[2].A2 = Fix(parent.ElectricalParameterSlots[0].At, 9);
                break;
            }

            lp = parentNumber;
        }

        // 【C原典】親から採れなければ通電電流値を A2 に設定。
        if (MatchesZero(ep[2].A2, ZeroAt))
        {
            ep[2].A2 = EnergizingCurrentToNine(dt.EnergizingCurrent);
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

    /// <summary>電線サイズ(SQ)の初期(未設定)値。【C原典】"000.00"(6)。</summary>
    private const string ZeroSq = "000.00";

    /// <summary>負荷容量(VA)の初期(未設定)値。【C原典】"0000000.00"(10)。</summary>
    private const string ZeroVa = "0000000.00";

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

    /// <summary>C の <c>sprintf("%06.2lf", v)</c> + <c>memcpy(dest, work, 6)</c> 相当(先頭 6 桁, 電線サイズ用)。</summary>
    private static string Format6(double value) =>
        Fix(EquipmentParameterFormatter.SprintfF("%06.2f", value), 6);

    /// <summary>
    /// 通電電流値(denryu 8桁)を AT/A1/A2 用の 9 桁へ設定する。
    /// 【C原典】memcpy(dest,"00000.000",9); memcpy(dest,denryu,8);(先頭 8 桁を denryu で上書き、9 桁目は '0')。
    /// </summary>
    private static string EnergizingCurrentToNine(string? energizingCurrent) =>
        Fix(energizingCurrent, DenryuWidth) + "0";

    /// <summary>
    /// 回路相数を Set_IM の負荷種別フラグ(1:三相 2:単相)へ変換する。
    /// 【C原典】if(kpaph=='3') w1=1; if(kpaph=='1') w1=2;(いずれでもない場合 C では未初期化。決定性のため 0)。
    /// </summary>
    private static int PhaseToLoadKind(char circuitPhaseCount)
    {
        int w1 = 0;
        if (circuitPhaseCount == '3') { w1 = 1; }
        if (circuitPhaseCount == '1') { w1 = 2; }
        return w1;
    }

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
