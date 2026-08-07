namespace Ews.Analysis;

/// <summary>
/// 機器サーチ結果フラグ(<see cref="EquipmentSearchResultFlagResolver.Resolve"/>)の結果。
/// </summary>
/// <param name="Flag">エラーフラグ flg。"  "(エラー無)/"E1"/"E2"/"E3"。</param>
/// <param name="ParameterNumber">電気パラメータ番号 epno。1 または 2。</param>
/// <param name="SkipSearch">機器サーチ要否 ret。false:サーチする(0) / true:サーチしない(1)。</param>
public sealed record EquipmentSearchResultFlag(string Flag, short ParameterNumber, bool SkipSearch);

/// <summary>
/// 直近上下位ファイルを検索した結果のフラグ(エラーフラグ・電気パラメータ番号・サーチ要否)を作成する。
/// 【C原典】Fysk01_Get_Errflg(toku/sekkei/src/Fysk01.c:3928)。
///   エラー番号 eno(1-8, 1-4:主複回路 5-6:制御回路 7-8:MP,SP回路/PT)を
///   エラーフラグ flg・電気パラメータ番号 epno・サーチ要否 ret へ振り分ける。
///   偶数側(2/4/6/8)はエラー("E1"/"E2"/"E3"/"E3")で ret=1(サーチしない)。
/// </summary>
public static class EquipmentSearchResultFlagResolver
{
    /// <summary>
    /// エラー番号に対応するフラグを返す。範囲外(1-8以外)は <c>null</c>。
    /// 【C原典】eno が 1-8 以外の場合 flg/epno は不変・ret=0。呼出側の初期値を維持するため
    /// 本移植では <c>null</c> を返し、呼出側で ret=0(サーチする)相当として扱う。
    /// </summary>
    public static EquipmentSearchResultFlag? Resolve(short errorNumber) => errorNumber switch
    {
        1 => new EquipmentSearchResultFlag("  ", 1, false),
        2 => new EquipmentSearchResultFlag("E1", 1, true),
        3 => new EquipmentSearchResultFlag("  ", 2, false),
        4 => new EquipmentSearchResultFlag("E2", 2, true),
        5 => new EquipmentSearchResultFlag("  ", 2, false),
        6 => new EquipmentSearchResultFlag("E3", 2, true),
        7 => new EquipmentSearchResultFlag("  ", 1, false),
        8 => new EquipmentSearchResultFlag("E3", 1, true),
        _ => null,
    };
}
