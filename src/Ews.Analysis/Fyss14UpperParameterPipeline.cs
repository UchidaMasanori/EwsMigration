using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// 主回路上流パラメータ生成の統括(周辺設定段＋自動生成 f/r ループ＋後処理群)。
/// 【C原典】toku/sekkei/src/Fyss14.c <c>Fyss14_Make_UpperParm</c>(341)。
///
/// C 原典の <c>for(f=r=0;;)</c> ループを忠実に再現する。1 巡目で周辺設定段
/// (Parm_Set_27 / Main_Rank_Set / Main_Rank_Update / Kiki_Equal_Bangou_Set /
/// Keiki_Kairo_Check / Parm_Set_MGSH / Make_UpperParm / Fyss40_Compo_DenryuuParm)を
/// 実行し、続いて NT/VT/PLTR 自動生成(<see cref="Fyss14AutoGenerator.GenerateAutoCircuits"/>)を試みる。
/// いずれかが生成されれば(=C の f!=0)もう 1 巡だけ周辺設定段を再実行して(2 巡目冒頭の
/// <c>if(f) break;</c> 相当で)ループを抜ける。何も生成されなければ 1 巡で終わる(最大 2 巡)。
/// ループ後に座標最適化(OptimZahyo)/切り換えタイプ設定(CS_MCDT_12_21_SET)/機器タイプ設定
/// (Type_Set)/計器回路要素リセット(KeikiKairo_Bangou_Reset)/MC 負荷容量リセット
/// (PropMcFukaReset)を実行する。
///
/// 各段はすべて既移植のクラスへ委譲する。外部境界入力(回路内容記述・予約語マスタ・周波数・
/// 製作仕様区分・工場グループ)は引数注入する。
/// </summary>
public static class Fyss14UpperParameterPipeline
{
    /// <summary>
    /// 主回路上流パラメータ生成を統括実行する。【C原典】Fyss14_Make_UpperParm(Fyss14.c:341)。
    /// </summary>
    /// <param name="mains">主回路レコード列(FYRT800 配列相当)。破壊的に更新する。【C原典】*Pmaina(件数 *Pmainc)。</param>
    /// <param name="descriptions">回路内容記述エリア(=Fysk11_FYDF805_KkGet)。Parm_Set_27/Parm_Set_MGSH で使用。</param>
    /// <param name="reservedWords">予約語マスタ(YOYAKU_TBL)。Kiki_Equal_Bangou_Set の汎用付与で使用。</param>
    /// <param name="frequency">回路周波数(Hz)。【C原典】Helutzu(HZ1=50/HZ2=60)。</param>
    /// <param name="manufacturingSpecKind">製作仕様区分(bukken1-&gt;com.kyo.sshiykbn)。既定 null。</param>
    /// <param name="facilityGroup">工場グループ(FyGetFacGrp)。PLTR 生成判定で使用。既定 0。</param>
    /// <returns>
    /// 最終レコード列・収集した設計エラー(FY-632E 等)・致命エラー(【C原典】ret!=0 の早期 return)を持つ結果。
    /// </returns>
    public static UpperParameterPipelineResult Run(
        IReadOnlyList<MainCircuitResult> mains,
        CircuitDescriptionArea descriptions,
        IReadOnlyList<ReservedWordMaster> reservedWords,
        int frequency,
        string? manufacturingSpecKind = null,
        int facilityGroup = 0)
    {
        ArgumentNullException.ThrowIfNull(mains);
        ArgumentNullException.ThrowIfNull(descriptions);
        ArgumentNullException.ThrowIfNull(reservedWords);

        // 【C原典】memset(&Kbangoua,...) はループ前に 1 回だが、Main_Rank_Set(3747)冒頭でも
        //   毎回 memset される。C# の SystemRankAssigner.Assign は毎回 CircuitNumberGenerator を
        //   new するため後者に忠実で、外側の 1 回 memset は subsumed される。

        IReadOnlyList<MainCircuitResult> current = mains;
        IReadOnlyList<CircuitParseError> designErrors = Array.Empty<CircuitParseError>();
        bool generated = false;

        // 【C原典】for(f=r=0;;){ ... }
        while (true)
        {
            // 【C原典】Parm_Set_27(特殊予約語区分 27A/27B/27C)。
            SpecialReservedKindSetter.Set27Kind(current, descriptions);

            // 【C原典】Main_Rank_Set(系統座標付与)。
            SystemRankAssigner.Assign(current);

            // 【C原典】Main_Rank_Update(グループ親データ追番の再設定)。
            GroupParentSequenceResetter.Reset(current);

            // 【C原典】Kiki_Equal_Bangou_Set(同一機器認識番号セッテイ)。
            EquipmentIdentityNumberAssigner.Assign(current, reservedWords);

            // 【C原典】ret = Keiki_Kairo_Check(...); if(ret!=0) return(ret);
            CircuitParseError? orderError = MeterCircuitOrderChecker.Check(current);
            if (orderError is not null)
            {
                return new UpperParameterPipelineResult(current, Array.Empty<CircuitParseError>(), orderError);
            }

            // 【C原典】Parm_Set_MGSH(シャッター回路 MGSH の区分設定)。
            SpecialReservedKindSetter.SetMgshKind(current, descriptions);

            // 【C原典】ret = Make_UpperParm(...): 上流パラメータ設定(FY-632E 等は erra へ収集)。
            designErrors = UpperParameterBuilder.GenerateUpperParameters(current, frequency, manufacturingSpecKind);

            // 【C原典】Fyss40_Compo_DenryuuParm(電気パラメータのクリア・移動[0]->[1][2])。
            CompositeElectricalParameterExpander.Expand(current);

            // 【C原典】if(f) break;(前巡で生成が起きていれば 2 巡目のここで抜ける)
            if (generated)
            {
                break;
            }

            // 【C原典】Pre_NT_Make→Mainfile_NT_Make / Pre_VT_Make→Mainfile_VT_Make /
            //   Pre_PLTR_Make→Mainfile_PLTR_Make を 1 巡分実行し、生成有無(=f)を得る。
            AutoGenerationSweep sweep =
                Fyss14AutoGenerator.GenerateAutoCircuits(current, manufacturingSpecKind, facilityGroup);
            current = sweep.Records;
            generated = sweep.Generated;

            // 【C原典】if(!f) break;(何も生成されなければ 1 巡で終了)
            if (!generated)
            {
                break;
            }

            // 【C原典】*Perrc=0;(次巡で再計算されるため収集済みエラーを破棄する)。
            //   C# では次巡の GenerateUpperParameters が新しいリストを返すので designErrors は上書きされる。
            designErrors = Array.Empty<CircuitParseError>();
        }

        // 【C原典】OptimZahyo(座標の最適化, 941226)。
        CoordinateOptimizer.Optimize(current);

        // 【C原典】ret = CS_MCDT_12_21_SET(...); if(ret!=0) return(ret);(950426/960404)
        CircuitParseError? switchError = SwitchTypeSetter.Set(current);
        if (switchError is not null)
        {
            return new UpperParameterPipelineResult(current, designErrors, switchError);
        }

        // 【C原典】Type_Set(機器タイプ設定, 941121)。
        EquipmentTypeSetter.Set(current, manufacturingSpecKind);

        // 【C原典】KeikiKairo_Bangou_Reset(計器回路要素リセット, 941130)。
        MeterCircuitElementResetter.Reset(current);

        // 【C原典】PropMcFukaReset(MC 負荷容量リセット, 改訂<31>)。
        McLoadCapacityResetter.Reset(current);

        return new UpperParameterPipelineResult(current, designErrors, null);
    }
}

/// <summary>
/// <see cref="Fyss14UpperParameterPipeline.Run"/> の結果。
/// 【C原典】Fyss14_Make_UpperParm の出力(*Pmaina / erra・*Perrc / ret)。
/// </summary>
/// <param name="Records">最終の主回路レコード列(NT/VT/PLTR 生成で件数が増え得る)。【C原典】*Pmaina。</param>
/// <param name="DesignErrors">収集した設計エラー(FY-632E 等)。【C原典】erra(件数 *Perrc)。致命エラー時は空。</param>
/// <param name="FatalError">
/// 致命エラー(【C原典】ret!=0 の早期 return: Keiki_Kairo_Check または CS_MCDT_12_21_SET)。
/// 正常時は null(=C の return(0))。
/// </param>
public sealed record UpperParameterPipelineResult(
    IReadOnlyList<MainCircuitResult> Records,
    IReadOnlyList<CircuitParseError> DesignErrors,
    CircuitParseError? FatalError);
