namespace Ews.Analysis;

/// <summary>
/// 機器サーチ結果フラグ。【C原典】Fysk01_Get_Errflg の出力(flg, epno)と戻り値(ret)。
/// </summary>
/// <param name="ShouldSearch">機器サーチするか。【C原典】ret==0(=サーチする)を true。</param>
/// <param name="Flag">エラーフラグ。【C原典】flg[2]("  "/"E1"/"E2"/"E3")。</param>
/// <param name="ParameterNumber">電気パラメータ番号 [1,2]。【C原典】*epno。</param>
public sealed record EquipmentSearchFlag(bool ShouldSearch, string Flag, int ParameterNumber);

/// <summary>
/// 直近上下位ファイル検索結果のエラー番号から機器サーチ結果フラグを作成する。
/// 【C原典】Fysk01_Get_Errflg(toku/sekkei/src/Fysk01.c:3970)。
///
/// エラー番号(eno)は 1-4:主複回路 / 5-6:制御回路 / 7-8:MP,SP回路,PT。
/// 偶数(E1/E2/E3 が立つ)側は機器サーチしない(ret=1)。
/// </summary>
public static class EquipmentSearchFlagResolver
{
    /// <summary>
    /// エラー番号から機器サーチ結果フラグを得る。
    /// 【C原典】Fysk01_Get_Errflg の switch(eno)。定義外の eno は ret=0(サーチする)。
    /// </summary>
    public static EquipmentSearchFlag Resolve(int errorNumber) =>
        errorNumber switch
        {
            1 => new(true, "  ", 1),
            2 => new(false, "E1", 1),
            3 => new(true, "  ", 2),
            4 => new(false, "E2", 2),
            5 => new(true, "  ", 2),
            6 => new(false, "E3", 2),
            7 => new(true, "  ", 1),
            8 => new(false, "E3", 1),
            _ => new(true, "  ", 0), // C原典は switch default 無し(flg/epno 不変・ret=0)
        };
}
