namespace Ews.Analysis;

/// <summary>
/// 対象オプション機器判定用データ(オプション機器番号)を保持する。
/// 【C原典】Fysk01_SET_INV_OPNO(toku/sekkei/src/Fysk01.c:6259)と、その設定先である
/// ファイルスコープのグローバル変数 <c>inv_opno</c>。
///
/// C 原典では <c>Fysk01_SET_INV_OPNO</c> が <c>inv_opno = opno</c> を実行して以後の
/// <c>Fysk01_Make_Koukiki_INV_OP</c>(=<see cref="InverterOptionComponentBuilder"/>)が
/// この値を参照し、ラインノイズフィルタ(opno==3)の手配数量を切り替える。
/// </summary>
public static class InverterOptionState
{
    /// <summary>ラインノイズフィルタを表すオプション機器番号。【C原典】inv_opno == 3。</summary>
    public const int LineNoiseFilter = 3;

    /// <summary>現在のオプション機器番号。【C原典】グローバル変数 inv_opno。</summary>
    public static int Current { get; private set; }

    /// <summary>
    /// オプション機器番号を設定する。【C原典】Fysk01_SET_INV_OPNO(opno) の <c>inv_opno = opno</c>。
    /// </summary>
    /// <param name="optionNumber">オプション機器番号。【C原典】SHORT opno。</param>
    public static void Set(int optionNumber) => Current = optionNumber;
}
