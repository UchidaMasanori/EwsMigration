using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ＮＴ/ＶＴ/ＰＬＴＲ自動生成の結線(f/r ループの生成コア)。
/// 【C原典】toku/sekkei/src/Fyss14.c <c>Fyss14_Make_UpperParm</c>(341)の生成段。
///
/// C 原典の <c>for(f=r=0;;)</c> ループは、周辺設定段(Parm_Set_27 / Main_Rank_Set /
/// Main_Rank_Update / Kiki_Equal_Bangou_Set / Keiki_Kairo_Check / Parm_Set_MGSH /
/// Make_UpperParm / Fyss40_Compo_DenryuuParm)を実行後、
/// Pre_NT_Make→Mainfile_NT_Make / Pre_VT_Make→Mainfile_VT_Make /
/// Pre_PLTR_Make→Mainfile_PLTR_Make を順に呼び、いずれかで生成が起きれば f を進めて
/// もう一巡だけ回して(2 巡目冒頭の <c>if(f) break;</c> で抜ける)終わる。
/// 本クラスは既移植の 6 メソッドを結線した「1 巡分の生成スイープ」を移植する。
/// 周辺設定段と外側 <c>for</c> ループ本体は未移植関数に依存するため後続で組み立てる。
/// </summary>
public static class Fyss14AutoGenerator
{
    /// <summary>
    /// 主回路に対して NT→VT→PLTR を 1 巡分自動生成し、拡張後リストと生成有無(=C の f)を返す。
    /// 【C原典】Fyss14_Make_UpperParm(Fyss14.c:401-419)の生成段。
    ///
    /// C 原典と同じ順序・同じ f 加算規則で結線する:
    /// NT は判定結果が非空なら f を 1 進めて挿入、VT は <c>r==2</c>(既存 VT で全件抑止)でも
    /// f を 1 進め非 0 なら更に f を進めて挿入(r==2 時は挿入 0 件でも複写のため呼ぶ)、
    /// PLTR は判定結果が非空なら f を 1 進めて挿入する。各段の出力を次段の入力へ渡す。
    /// </summary>
    /// <param name="mains">周辺設定段適用済みの主回路レコード列。【C原典】*Pmaina(件数 *Pmainc)。</param>
    /// <param name="manufacturingSpecKind">製作仕様区分(bukken1->com.kyo.sshiykbn)。PLTR 判定で使用。既定 null。</param>
    /// <param name="facilityGroup">工場グループ(FyGetFacGrp)。PLTR 判定で使用。既定 0。</param>
    /// <returns>自動生成後の主回路レコード列と、いずれかの生成が起きたか(=C の f!=0)。</returns>
    public static AutoGenerationSweep GenerateAutoCircuits(
        IReadOnlyList<MainCircuitResult> mains,
        string? manufacturingSpecKind = null,
        int facilityGroup = 0)
    {
        ArgumentNullException.ThrowIfNull(mains);

        IReadOnlyList<MainCircuitResult> current = mains;
        int f = 0;

        // 【C原典】r=Pre_NT_Make(...); if(r!=0){ f++; Mainfile_NT_Make(...); }
        IReadOnlyList<NtInsertion> ntPlan = NtCircuitGenerator.PrepareNtInsertions(current);
        if (ntPlan.Count != 0)
        {
            f++;
            current = NtCircuitGenerator.InsertNtRecords(current, ntPlan);
        }

        // 【C原典】r=Pre_VT_Make(...); if(r==2) f++; if(r!=0){ f++; Mainfile_VT_Make(...); }
        VtPreparation vt = VtCircuitGenerator.PrepareVtInsertions(current);
        if (vt.Status == 2)
        {
            f++;   // 950515: 既存 VT で全件抑止でも f を進める。
        }

        if (vt.Status != 0)
        {
            f++;
            current = VtCircuitGenerator.InsertVtRecords(current, vt.Insertions);
        }

        // 【C原典】r=Pre_PLTR_Make(...); if(r!=0){ f++; Mainfile_PLTR_Make(...); }
        IReadOnlyList<PltrInsertion> pltrPlan =
            PltrCircuitGenerator.PreparePltrInsertions(current, manufacturingSpecKind, facilityGroup);
        if (pltrPlan.Count != 0)
        {
            f++;
            current = PltrCircuitGenerator.InsertPltrRecords(current, pltrPlan);
        }

        return new AutoGenerationSweep(current, f != 0);
    }
}

/// <summary>
/// 自動生成スイープ 1 巡分の結果。【C原典】Fyss14_Make_UpperParm の生成段出力(*Pmaina, f)。
/// </summary>
/// <param name="Records">自動生成後の主回路レコード列。【C原典】*Pmaina。</param>
/// <param name="Generated">いずれかの生成(NT/VT/PLTR)が起きたか。【C原典】f!=0(外側ループ継続判定)。</param>
public sealed record AutoGenerationSweep(IReadOnlyList<MainCircuitResult> Records, bool Generated);
