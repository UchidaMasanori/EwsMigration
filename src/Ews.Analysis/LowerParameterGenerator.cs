using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// 下流からのパラメータ生成(MAIN)。【C原典】<c>Fyss15_Make_LowerParm</c>(toku/sekkei/src/Fyss15.c)。
/// </summary>
public static class LowerParameterGenerator
{
    /// <summary>
    /// <see cref="MakeLowerParameters"/> の戻り値。C原典 <c>Fyss15_Make_LowerParm</c> は SHORT を返しつつ
    /// <c>*Pmaina</c>(主回路)と <c>*Pkouseia</c>(構成機器)を書き換えるため、その 3 つを 1 レコードにまとめる。
    /// </summary>
    /// <param name="ReturnCode">
    /// C原典の戻り値。0=正常、-1=CT 自動生成により主回路を再構築(呼出側は構成機器をクリアし再実行)、
    /// 1=負荷発生元セット異常または末端ブレーカ選定異常(ret!=0 かつ !=3)、2=使用相決定異常、
    /// 3=末端ブレーカ選定で ret==3(改訂&lt;5&gt;)。
    /// </param>
    /// <param name="Mains">処理後の主回路エリア。CT 自動生成時は再構築された新配列、それ以外は入力と同一。</param>
    /// <param name="ComponentsCleared">
    /// CT 自動生成が発生し構成機器(<c>*Pkouseic=0; *Pkouseia=NULL</c>)をクリアすべき場合 true。
    /// </param>
    public readonly record struct LowerParameterResult(
        int ReturnCode,
        IReadOnlyList<MainCircuitResult> Mains,
        bool ComponentsCleared);

    /// <summary>
    /// 下流からのパラメータ生成(MAIN)。末端区分・積み上げ区分・負荷発生元・機器選定・通電電流値・
    /// 使用相・電気パラメータ・メータ回路電流・CT 自動生成などを C原典の呼出順どおりに実行する。
    /// 【C原典】<c>Fyss15_Make_LowerParm</c>(toku/sekkei/src/Fyss15.c:132-390)。
    ///
    /// 未移植の外部依存(主回路機器サーチ=Fysk00、系統通電電流=SC_Keitou_Proc、自由文字有無判定、
    /// 親データ検索、エラー出力=Perrc/Perra など)は EwsMigration のインターフェイス非使用方針に従い
    /// 引数注入(デリゲート)で境界化する。
    /// </summary>
    /// <param name="mains">主回路エリア(FYRT800 配列相当)。【C原典】<c>*Pmaina</c>(件数 <c>*Pmainc</c>)。</param>
    /// <param name="panelCompositionKind">
    /// 品種構成区分。【C原典】<c>bukken2->com.mei.hycpskbn</c>。'3'=特注、'7'=ブロックコンポ。
    /// Fyss3R(プラグイン結線/主幹チェック)と Fyss3D(使用相決定)の分岐に使用。
    /// </param>
    /// <param name="branchArrayDesignationKind">
    /// 分岐配列指定有無区分。【C原典】<c>bukken2</c> 由来。Fyss3C(分岐並び換え)は '2' のときのみ処理する。
    /// </param>
    /// <param name="autoKick">自動起動区分。【C原典】<c>bukken1->auto_kick</c>(改訂&lt;8&gt;)。'W'=WinEATS。</param>
    /// <param name="zoneCode">地区コード。【C原典】<c>getenv("ZONECD")</c>(改訂&lt;6&gt;&lt;8&gt;)。</param>
    /// <param name="manufacturingSpecKind">
    /// 製作仕様区分。【C原典】Fyss3G/Fyss31 の製作仕様。先頭 2 文字が "01" なら河村標準(seisakusiyou=1)。
    /// </param>
    /// <param name="reservedWords">予約語マスタ(Fyss3C 用)。</param>
    /// <param name="components">構成機器エリア(Fyss3C 用)。【C原典】<c>*Pkouseia</c>。</param>
    /// <param name="parameterSettingTable">電気パラメータ設定型テーブル(Fyss3G 用)。</param>
    /// <param name="wireSizeTable">電線サイズ設定テーブル(Fyss3G 用)。</param>
    /// <param name="ratedCurrent2Table">定格電流 2 設定テーブル(Fyss3G 用)。</param>
    /// <param name="ratedCurrent1Table">定格電流 1 設定テーブル(Fyss3G 用)。</param>
    /// <param name="majorClassResolver">予約語→機器大分類の解決(Fyss33 用)。</param>
    /// <param name="equipmentSearch">主回路機器サーチ(Fyss3B=Fysk00 境界)。戻り値 ret(0=正常,3=特殊,他=異常)。</param>
    /// <param name="hasNothingInFreeText">自由文字に何も無いか(Fyss3R プラグイン結線境界)。</param>
    /// <param name="findParent">親データ追番→主回路データ検索(Fyss3R 主幹チェック境界)。</param>
    /// <param name="processSystemCircuit">系統(ＳＣ)通電電流算出(Fyss31 境界)。null なら SC 分岐を処理しない。</param>
    /// <param name="checkSystemReservedWord">系統予約語判定(Fyss36 境界)。</param>
    /// <param name="accumulateSystemCurrent">系統通電電流積算(Fyss36 境界)。</param>
    /// <param name="reportError">エラー出力(【C原典】<c>Perrc</c>/<c>Perra</c>)。</param>
    /// <param name="reportDesignError">使用相決定の設計エラー通知(Fyss3D 境界)。</param>
    /// <param name="reportDiagnostic">診断ログ(【C原典】<c>FyHcErrFunc</c>。Fyss37 積算異常時など)。</param>
    public static LowerParameterResult MakeLowerParameters(
        IReadOnlyList<MainCircuitResult> mains,
        char panelCompositionKind,
        char branchArrayDesignationKind,
        char autoKick,
        string zoneCode,
        string manufacturingSpecKind,
        IReadOnlyList<ReservedWordMaster> reservedWords,
        IReadOnlyList<ComponentEquipment> components,
        IReadOnlyList<ParameterSettingType> parameterSettingTable,
        IReadOnlyList<WireSizeSetting> wireSizeTable,
        IReadOnlyList<RatedCurrent2Setting> ratedCurrent2Table,
        IReadOnlyList<RatedCurrent1Setting> ratedCurrent1Table,
        Func<string, char> majorClassResolver,
        Func<IReadOnlyList<MainCircuitResult>, int> equipmentSearch,
        Func<MainCircuitResult, bool> hasNothingInFreeText,
        Func<string, MainCircuitResult?> findParent,
        Action<int>? processSystemCircuit = null,
        Func<int, (int Ret, int Flag)>? checkSystemReservedWord = null,
        Action<int, int, double>? accumulateSystemCurrent = null,
        Action<CircuitParseError>? reportError = null,
        Action<int>? reportDesignError = null,
        Action<string>? reportDiagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(zoneCode);
        ArgumentNullException.ThrowIfNull(manufacturingSpecKind);
        ArgumentNullException.ThrowIfNull(reservedWords);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(parameterSettingTable);
        ArgumentNullException.ThrowIfNull(wireSizeTable);
        ArgumentNullException.ThrowIfNull(ratedCurrent2Table);
        ArgumentNullException.ThrowIfNull(ratedCurrent1Table);
        ArgumentNullException.ThrowIfNull(majorClassResolver);
        ArgumentNullException.ThrowIfNull(equipmentSearch);
        ArgumentNullException.ThrowIfNull(hasNothingInFreeText);
        ArgumentNullException.ThrowIfNull(findParent);

        // 【C原典 改訂<3>】製作仕様区分の先頭 2 文字が "01" なら河村標準(seisakusiyou=1)。
        int seisakusiyou = manufacturingSpecKind.StartsWith("01", StringComparison.Ordinal) ? 1 : 0;

        // 末端区分のセット。【C原典】Fyss30_MattanKubun_Set。
        TerminalKindSetter.SetTerminalKind(mains);

        // ＳＣ／ＮＴの上流積み上げ区分セット。【C原典】Fyss32_SC_NT_Tumiage_Set。
        UpstreamStackingKindSetter.SetUpstreamStackingKind(mains);

        // 末端回路行種先頭機器フラグセット。【C原典】Fyss34_MattanGyouSento_Set。
        LeadingEquipmentFlagSetter.SetLeadingEquipmentFlag(mains);

        // 負荷発生元のセット。【C原典】ret=Fyss31_FukaHassei_Set; if(ret!=0) return(ret)。
        CircuitParseError? loadSourceError = LoadSourceSelector.SetLoadSource(mains, seisakusiyou, processSystemCircuit);
        if (loadSourceError is not null)
        {
            reportError?.Invoke(loadSourceError);
            return new LowerParameterResult(1, mains, false);
        }

        // 機器選定区分セット。【C原典】Fyss33_KikiSentei_Set(1994.7.30)。
        EquipmentSelectionKindSetter.SetEquipmentSelectionKind(mains, majorClassResolver);

        // 末端回路の通電電流値算出。【C原典】Fyss36_MattanKairo_Iset。
        AccumulationAreaSetter.SetTerminalCircuitCurrent(mains, checkSystemReservedWord, accumulateSystemCurrent, reportDiagnostic);

        // 電気パラメータ 2 のみセット。【C原典】Fyss3G_Denryuu_Parm_Set(...,1,'M')(1994.8.10)。
        CurrentParameterDispatcher.DispatchCurrentParameters(
            manufacturingSpecKind, mains.Count, mains, 1, 'M',
            parameterSettingTable, wireSizeTable, ratedCurrent2Table, ratedCurrent1Table, zoneCode);

        // 特注盤対応 プラグインブレーカの結線処理。【C原典 改訂<3><6>】hycpskbn '3'/'7' または地区コード。
        if (panelCompositionKind == '3' || panelCompositionKind == '7')
        {
            PlugInBreakerConnector.SetConnection(mains, hasNothingInFreeText);
        }
        else if (zoneCode == "33333" || zoneCode == "33334" || zoneCode == "33335")
        {
            PlugInBreakerConnector.SetConnection(mains, hasNothingInFreeText);
        }

        // 末端回路ブレーカの機器選定。【C原典】ret=Fyss3B_Breaker_Sentei; ret==3→return(3); ret!=0→return(1)。
        int breakerRet = TerminalBreakerSelector.SelectBreakers(mains, equipmentSearch);
        if (breakerRet == 3)
        {
            return new LowerParameterResult(3, mains, false);
        }
        if (breakerRet != 0)
        {
            return new LowerParameterResult(1, mains, false);
        }

        // 分岐配列指定無し時の並び換え。【C原典】Fyss3C_Bunki_Sort(bukken2,...,Pkousei)(950407)。
        BranchArraySorter.SortBranchArray(branchArrayDesignationKind, mains, reservedWords, components);

        // 使用相の決定。【C原典 改訂<3>】if(0!=Fyss3D_PH_Kettei(bukken2,...)) return(2)。
        CircuitParseError? phaseError = PhaseAssigner.AssignPhases(mains, panelCompositionKind, reportDesignError);
        if (phaseError is not null)
        {
            reportError?.Invoke(phaseError);
            return new LowerParameterResult(2, mains, false);
        }

        // ＳＣの特殊処理。【C原典】Fyss39_SC_Proc。
        ScSpecialProcessor.ProcessSc(mains);

        // 負荷発生元の変更処理。【C原典】Fyss3F_Fuka_Change(1994.7.30)。
        LoadSourceChanger.ChangeLoadSource(mains);

        // 1-2 型 MCDT/CSDT の処理。【C原典】Fyss3E_12_MCDT_CSDT。
        Process12McdtCsdt(mains);

        // 2-1 型 MCDT/CSDT の処理。【C原典】Fyss3I_21_MCDT_CSDT。
        Process21McdtCsdt(mains);

        // 通電電流値積算サブルーチン。【C原典】予約語 'P' の各データ追番で Fyss37_I_Set_Sub(1994.7.30)。
        for (int i = 0; i < mains.Count; i++)
        {
            if (Matches(mains[i].Data.ReservedWord, "P       ", 8))
            {
                int oiban = EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3);
                if (!TerminalCurrentIntegrator.IntegrateCurrent(mains, oiban))
                {
                    reportDiagnostic?.Invoke("Fyss15_Make_LowerParm(): Fyss37_I_Set_Sub failed.");
                }
            }
        }

        // ＮＴの特殊処理。【C原典】Fyss38_NT_Proc。
        NtSpecialProcessor.ProcessNt(mains);

        // ＳＣ／ＮＴの上流積算処理。【C原典】Fyss3A_SC_NT_Sekisan。
        ScNtUpstreamAccumulator.AccumulateScNt(mains);

        // メータ回路の通電電流値算出。【C原典】Fyss3H_Keiki_Iset。
        SetMeterCircuitCurrent(mains);

        // 電気パラメータのセット。【C原典】Fyss3G_Denryuu_Parm_Set(...,2,'M') と (...,2,'K')。
        CurrentParameterDispatcher.DispatchCurrentParameters(
            manufacturingSpecKind, mains.Count, mains, 2, 'M',
            parameterSettingTable, wireSizeTable, ratedCurrent2Table, ratedCurrent1Table, zoneCode);
        CurrentParameterDispatcher.DispatchCurrentParameters(
            manufacturingSpecKind, mains.Count, mains, 2, 'K',
            parameterSettingTable, wireSizeTable, ratedCurrent2Table, ratedCurrent1Table, zoneCode);

        // CT 自動生成。【C原典】r=Pre_CT_Make; if(r!=0){ 構成機器クリア; Mainfile_CT_Make; return(-1); }。
        IReadOnlyList<CtAutoGenerator.CtInfo> ctList = CtAutoGenerator.PrepareCtCreation(mains);
        if (ctList.Count > 0)
        {
            IReadOnlyList<MainCircuitResult> rebuilt = CtAutoGenerator.InsertCtIntoMainCircuit(mains, ctList);
            return new LowerParameterResult(-1, rebuilt, true);
        }

        // ＮＴに直接つながる MCB1P の使用相調整。【C原典】Fyss15_MCB1P_NT(...,'N')。
        AdjustMcb1PhaseForNt(mains, 'N');

        // 特注盤対応 プラグインブレーカの主幹チェック処理。【C原典 改訂<3><8>】。
        if (panelCompositionKind == '3' || panelCompositionKind == '7')
        {
            // クレスポ地区かつ簡易作図(auto_kick=='W')なら対象外。改訂<8>
            if (zoneCode != "33333" && zoneCode != "33334" && zoneCode != "33335")
            {
                CircuitParseError? mainChkError = PlugInBreakerConnector.CheckMainBreaker(mains, findParent);
                if (mainChkError is not null)
                {
                    reportError?.Invoke(mainChkError);
                }
            }
            else if (autoKick != 'W')
            {
                CircuitParseError? mainChkError = PlugInBreakerConnector.CheckMainBreaker(mains, findParent);
                if (mainChkError is not null)
                {
                    reportError?.Invoke(mainChkError);
                }
            }
        }

        return new LowerParameterResult(0, mains, false);
    }

    /// <summary>
    /// ＮＴに直接つながる MCB1P/RMCB1P の使用相に N 相を追加する。
    /// 【C原典】<c>Fyss15_MCB1P_NT</c>(Fyss15.c:404, 950531)。下流探索は移植済みの
    /// <see cref="DownstreamSelector.SelectDownstream"/>(=Fyss35_Select_Karyu_Sub)を再利用。
    /// </summary>
    /// <param name="mains">主回路エリア(FYRT800 配列相当)。</param>
    /// <param name="phase">使用相 2 文字目に設定する相文字。【C原典】呼出側は 'N'。</param>
    public static void AdjustMcb1PhaseForNt(IReadOnlyList<MainCircuitResult> mains, char phase)
    {
        ArgumentNullException.ThrowIfNull(mains);

        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData di = mains[i].Data;

            if (Matches(di.ReservedWord, "MCB     ", 8) &&
                Matches(di.ElectricalParameterSlots[0].P, "001", 3))
            {
                IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, i + 1);
                if (downstream is null)
                {
                    continue; // 下流抽出エラー(ret != 0)
                }

                if (downstream.Count == 0)
                {
                    di.UsedPhase = SetPhaseChar(di.UsedPhase, 1, phase); // N 相を追加
                }
            }

            // 1996.01.08: RMCB も MCB と同様に処理する。
            if (Matches(di.ReservedWord, "RMCB    ", 8) &&
                Matches(di.ElectricalParameterSlots[0].P, "001", 3))
            {
                IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, i + 1);
                if (downstream is null)
                {
                    continue; // 下流抽出エラー(ret != 0)
                }

                if (downstream.Count == 0)
                {
                    di.UsedPhase = SetPhaseChar(di.UsedPhase, 1, phase); // N 相を追加
                }
            }
        }
    }

    /// <summary>
    /// 1-2 型 MCDT/CSDT の処理。下流の負荷を算出し、同一機器認識番号のペアで通電電流値が
    /// 小さい方のテーブル要素に上流積み上げ区分をセットする。対象要素とその下流の通電電流値・
    /// 積算エリアはクリアする。
    /// 【C原典】<c>Fyss3E_12_MCDT_CSDT</c>(toku/sekkei/src/Fyss3E.c, 940727)。通電電流値積算は
    /// <see cref="TerminalCurrentIntegrator.IntegrateCurrent"/>(=Fyss37_I_Set_Sub)、下流抽出は
    /// <see cref="DownstreamSelector.SelectDownstream"/>(=Fyss35_Select_Karyu_Sub)を再利用。
    /// </summary>
    /// <param name="mains">主回路エリア(FYRT800 配列相当)。</param>
    public static void Process12McdtCsdt(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        const int MaxNo = 100; // 【C原典】MAX_NO=100。datano/noflag の固定長。
        int[] datano = new int[MaxNo];
        bool[] noflag = new bool[MaxNo];

        // 回路要素'1' で予約語 'MCDT'/'CSDT' かつ切り換えタイプ'1'(1-2型)を取得し、通電電流値を積算する。
        int num = 0;
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.CircuitElement != '1')
            {
                continue;
            }

            if (!Matches(d.ReservedWord, "MCDT", 4) && !Matches(d.ReservedWord, "CSDT", 4))
            {
                continue;
            }

            if (d.SwitchType == '1')
            {
                int no1 = EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3);
                datano[num] = no1;
                num++;
                TerminalCurrentIntegrator.IntegrateCurrent(mains, no1);
            }
        }

        // 同一機器認識番号が同じテーブル要素同士で、通電電流値の小さい方に上流積み上げ区分をセットする。
        for (int i = 0; i < num; i++)
        {
            if (noflag[i])
            {
                continue;
            }

            int no1 = datano[i];
            int kiki1 = EquipmentParameterFormatter.Stoi(mains[no1 - 1].Data.IdentityNumber, 2);

            int no2 = no1;
            int j = 0;
            for (; j < num; j++)
            {
                no2 = datano[j];
                if (no1 == no2)
                {
                    continue;
                }

                int kiki2 = EquipmentParameterFormatter.Stoi(mains[no2 - 1].Data.IdentityNumber, 2);
                if (kiki1 == kiki2)
                {
                    break;
                }
            }

            noflag[i] = true;
            noflag[j] = true; // 【C原典】break 未成立時は j==num(未使用領域)を立てる。UB を忠実再現。

            double tden1 = EquipmentParameterFormatter.Stof(mains[no1 - 1].Data.EnergizingCurrent, 8);
            double tden2 = EquipmentParameterFormatter.Stof(mains[no2 - 1].Data.EnergizingCurrent, 8);

            if (tden1 > tden2)
            {
                mains[no2 - 1].Data.StackKind = '1';
            }
            else
            {
                mains[no1 - 1].Data.StackKind = '1';
            }
        }

        // 下流テーブル要素の通電電流値・積算エリアをクリアする。
        for (int i = 0; i < num; i++)
        {
            int no1 = datano[i];
            ClearAccumulation(mains[no1 - 1]);

            // 【C原典】ret を無視して knum を使用。null(ret!=0)は knum==0 と等価でループしない。
            IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, no1);
            if (downstream is null)
            {
                continue;
            }

            foreach (int no2 in downstream)
            {
                // 負荷発生元区分 == '1' の時は打ち切る。
                if (mains[no2 - 1].Data.LoadSourceKind == '1')
                {
                    break;
                }

                mains[no2 - 1].Data.EnergizingCurrent = "00000.00";
                ClearAccumulation(mains[no2 - 1]);
            }
        }
    }

    /// <summary>
    /// 2-1 型 MCDT/CSDT の処理。下流の負荷を算出し、同一機器認識番号の相手側テーブル要素へ
    /// 通電電流値・機器選定区分・積算エリアをコピーする。対象要素の通電電流値・積算エリアはクリアする。
    /// 機器選定区分が異なるときは親データ追番を辿って伝播する。
    /// 【C原典】<c>Fyss3I_21_MCDT_CSDT</c>(toku/sekkei/src/Fyss3I.c, 950428)。通電電流値積算は
    /// <see cref="TerminalCurrentIntegrator.IntegrateCurrent"/>(=Fyss37_I_Set_Sub)を再利用。
    /// </summary>
    /// <param name="mains">主回路エリア(FYRT800 配列相当)。</param>
    public static void Process21McdtCsdt(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        const int MaxNo = 100; // 【C原典】MAX_NO=100。datano の固定長。
        int[] datano = new int[MaxNo];

        // 回路要素'1'・予約語MCDT/CSDT・末端区分!='1'・切り換えタイプ'2'(2-1型)を取得し、通電電流値を積算する。
        int num = 0;
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.CircuitElement != '1')
            {
                continue;
            }

            if (!Matches(d.ReservedWord, "MCDT", 4) && !Matches(d.ReservedWord, "CSDT", 4))
            {
                continue;
            }

            if (d.TerminalKind == '1')
            {
                continue;
            }

            if (d.SwitchType == '2')
            {
                int no = EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3);
                datano[num] = no;
                num++;
                TerminalCurrentIntegrator.IntegrateCurrent(mains, no);
            }
        }

        // 同一機器認識番号が同じテーブル要素へ通電電流値・機器選定区分・積算エリアをコピーする。
        for (int i = 0; i < num; i++)
        {
            int no1 = datano[i] - 1;

            for (int j = 0; j < mains.Count; j++)
            {
                if (no1 == j)
                {
                    continue;
                }

                if (!Matches(mains[j].Data.IdentityNumber, mains[no1].Data.IdentityNumber, 2))
                {
                    continue;
                }

                mains[j].Data.EnergizingCurrent = mains[no1].Data.EnergizingCurrent;
                mains[j].Data.LoadSourceKind = '1'; // 負荷発生元

                // 機器選定区分が異なるとき、親データ追番を辿って no1 の区分を伝播する。
                if (mains[j].Work.EquipmentSelectionKind != mains[no1].Work.EquipmentSelectionKind)
                {
                    string work = mains[j].Data.ParentSequenceNumber;
                    mains[j].Work.EquipmentSelectionKind = mains[no1].Work.EquipmentSelectionKind;
                    while (true)
                    {
                        int k = 0;
                        for (; k < mains.Count; k++)
                        {
                            if (Matches(mains[k].SequenceNumber, work, 3))
                            {
                                break;
                            }
                        }

                        if (k >= mains.Count)
                        {
                            break;
                        }

                        mains[k].Work.EquipmentSelectionKind = mains[no1].Work.EquipmentSelectionKind;
                        if (Matches(mains[k].Data.ParentSequenceNumber, "000", 3))
                        {
                            break;
                        }

                        work = mains[k].Data.ParentSequenceNumber;
                    }
                }

                CopyAccumulation(mains[j], mains[no1]);
            }

            ClearAccumulation(mains[no1]);
            mains[no1].Data.EnergizingCurrent = "00000.00";
        }
    }

    /// <summary>
    /// 計器回路(CT/ZCT)の通電電流値をセットする。回路要素'2'の CT は同一機器認識番号の回路要素'1'CT の
    /// 通電電流値を、回路要素≠'1'の ZCT は親データ追番の通電電流値を、それぞれ自身と下流の全要素にセットする。
    /// 【C原典】<c>Fyss3H_Keiki_Iset</c>(toku/sekkei/src/Fyss3H.c, 940719)。下流抽出は
    /// <see cref="DownstreamSelector.SelectDownstream"/>(=Fyss35_Select_Karyu_Sub)を再利用。
    /// </summary>
    /// <param name="mains">主回路エリア(FYRT800 配列相当)。</param>
    public static void SetMeterCircuitCurrent(IReadOnlyList<MainCircuitResult> mains)
    {
        ArgumentNullException.ThrowIfNull(mains);

        // 回路要素'2'・予約語'CT' の通電電流値をセットする。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.CircuitElement == '2' && Matches(d.ReservedWord, "CT      ", 8))
            {
                int no = EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3);
                int dno = EquipmentParameterFormatter.Stoi(d.IdentityNumber, 2);
                if (dno != 0)
                {
                    SetCtCurrent(mains, no, dno);
                }
            }
        }

        // 回路要素≠'1'・予約語'ZCT' の通電電流値をセットする。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            if (d.CircuitElement != '1' && Matches(d.ReservedWord, "ZCT     ", 8))
            {
                int no = EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3);
                int oyano = EquipmentParameterFormatter.Stoi(d.ParentSequenceNumber, 3);
                if (oyano != 0)
                {
                    SetZctCurrent(mains, no, oyano);
                }
            }
        }
    }

    // 【C原典】Fyss3H_Set_Den1(no, dno, num, syu)。同一機器認識番号の回路要素'1'CT の通電電流値を取得しセット。
    private static void SetCtCurrent(IReadOnlyList<MainCircuitResult> mains, int no, int dno)
    {
        string tsuden = "00000000"; // 【C原典】未初期化(UB)。設計上 CT'1' は存在する前提。
        for (int i = 0; i < mains.Count; i++)
        {
            MainCircuitData d = mains[i].Data;
            int doukkno = EquipmentParameterFormatter.Stoi(d.IdentityNumber, 2);
            if (dno != doukkno || doukkno == 0)
            {
                continue;
            }

            if (d.CircuitElement == '1' && Matches(d.ReservedWord, "CT      ", 8))
            {
                tsuden = Fix8(d.EnergizingCurrent);
                break;
            }
        }

        SetCurrentByDataNumber(mains, no, tsuden);
        SetDownstreamCurrent(mains, tsuden, no);
    }

    // 【C原典】Fyss3H_Set_Den2(no, oya, num, syu)。親データ追番の通電電流値を取得しセット。
    private static void SetZctCurrent(IReadOnlyList<MainCircuitResult> mains, int no, int oya)
    {
        string tsuden = "00000000"; // 【C原典】未初期化(UB)。設計上親要素は存在する前提。
        for (int i = 0; i < mains.Count; i++)
        {
            if (oya != EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3))
            {
                continue;
            }

            tsuden = Fix8(mains[i].Data.EnergizingCurrent);
            break;
        }

        SetCurrentByDataNumber(mains, no, tsuden);
        SetDownstreamCurrent(mains, tsuden, no);
    }

    // 【C原典】Fyss3H_Set_Karu(tu, no, num, syu)。指定データ追番の下流全要素に通電電流値をセット。
    private static void SetDownstreamCurrent(IReadOnlyList<MainCircuitResult> mains, string tsuden, int no)
    {
        IReadOnlyList<int>? downstream = DownstreamSelector.SelectDownstream(mains, no);
        if (downstream is null)
        {
            return;
        }

        foreach (int kdatano in downstream)
        {
            SetCurrentByDataNumber(mains, kdatano, tsuden);
        }
    }

    // データ追番 no に一致する最初の要素の通電電流値に tsuden をセットする。
    private static void SetCurrentByDataNumber(IReadOnlyList<MainCircuitResult> mains, int no, string tsuden)
    {
        for (int i = 0; i < mains.Count; i++)
        {
            if (EquipmentParameterFormatter.Stoi(mains[i].SequenceNumber, 3) == no)
            {
                mains[i].Data.EnergizingCurrent = tsuden;
                break;
            }
        }
    }

    // memcpy(dest, denryu, 8) 相当。通電電流値の先頭 8 桁を取り出す。
    private static string Fix8(string s) => (s ?? string.Empty).PadRight(8)[..8];

    // 積算エリア(6 スロット)の全機器種別値をクリアする。【C原典】sk_area[m].?_area = 0.0。
    private static void ClearAccumulation(MainCircuitResult record)
    {
        foreach (AccumulationArea a in record.Work.AccumulationSlots)
        {
            a.A = 0.0;
            a.B = 0.0;
            a.C = 0.0;
            a.D = 0.0;
            a.E = 0.0;
            a.M = 0.0;
            a.S = 0.0;
        }
    }

    // 積算エリア(6 スロット)を src から dest へコピーする。【C原典】dest.sk_area[m] = src.sk_area[m]。
    private static void CopyAccumulation(MainCircuitResult dest, MainCircuitResult src)
    {
        for (int m = 0; m < dest.Work.AccumulationSlots.Length; m++)
        {
            AccumulationArea s = src.Work.AccumulationSlots[m];
            AccumulationArea d = dest.Work.AccumulationSlots[m];
            d.A = s.A;
            d.B = s.B;
            d.C = s.C;
            d.D = s.D;
            d.E = s.E;
            d.M = s.M;
            d.S = s.S;
        }
    }

    // strncmp(a, b, width) == 0 相当。
    private static bool Matches(string value, string expected, int width) =>
        (value ?? string.Empty).PadRight(width)[..width] == expected.PadRight(width)[..width];

    // 4 桁固定の使用相の index 番目を c に差し替える(他桁は保持)。
    private static string SetPhaseChar(string phase, int index, char c)
    {
        char[] arr = (phase ?? string.Empty).PadRight(4)[..4].ToCharArray();
        arr[index] = c;
        return new string(arr);
    }
}
