namespace Ews.Analysis;

/// <summary>
/// 機器選定(直近上下位ファイル検索での候補比較)。
///
/// 【C原典】toku/sekkei/src/Fysk01.c
///   - <c>Fysk01_Choki_Cmp1(DOUBLE sentchi, DOUBLE dt1[], DOUBLE dt2[])</c>(Fysk01.c:2734)
///     … 特殊予約語 THR / MG / XERY 用。基準値 sentchi が今回幅 dt1[0..1]・前回幅 dt2[0..1] の
///       どちらへより「均等に収まるか」で優劣を判定する。
///   - <c>Fysk01_Choki_Cmp2(DOUBLE sentchi, DOUBLE* dt1, DOUBLE* dt2)</c>(Fysk01.c:2779)
///     … 特殊予約語 THSW / TM 用。基準値 sentchi と各幅の中点の距離で優劣を判定する。
///
/// いずれもマスタ(ISAM)・記録列・物件に依存しない純粋数値関数で、機器マスタから複数候補が
/// ヒットした際に「よりよい候補」を選ぶ(=前回候補を今回候補で入れ替えるか)の判定に使う。
///
/// 定数(【C原典】fyrt808.h): GOOD=0 / SYS_ERR=-1 / TOL=0.001。
/// </summary>
public static class EquipmentSelector
{
    /// <summary>【C原典】GOOD(fyrt808.h:31)。良好(入れ替えない)。</summary>
    private const short Good = 0;

    /// <summary>【C原典】SYS_ERR(fyrt808.h:35)。システムエラー(不正な幅・範囲外)。</summary>
    private const short SysErr = -1;

    /// <summary>
    /// 比較データ 1(今回)・2(前回)より、よりよいデータを選ぶ(特殊予約語 THR/MG/XERY 用)。
    /// 【C原典】Fysk01_Choki_Cmp1(Fysk01.c:2734)。
    /// 基準値 sentchi の各幅内での正規化位置(下端寄り比・上端寄り比)の差の絶対値を求め、
    /// 今回幅(dt1)の方が小さい(=より中央に収まる)なら入れ替え(1)を返す。
    /// </summary>
    /// <param name="sentchi">基準値(【C原典】sentchi)。</param>
    /// <param name="dt1">今回比較データ幅値[下端, 上端](【C原典】dt1[2])。</param>
    /// <param name="dt2">前回比較データ幅値[下端, 上端](【C原典】dt2[2])。</param>
    /// <returns>1:データ入れ替えをする / 0:入れ替えない(GOOD) / -1:システムエラー(SYS_ERR)。</returns>
    public static short ChokiCmp1(double sentchi, double[] dt1, double[] dt2)
    {
        ArgumentNullException.ThrowIfNull(dt1);
        ArgumentNullException.ThrowIfNull(dt2);

        if (dt1[0] >= dt1[1])
        {
            return SysErr;
        }

        if (dt2[0] >= dt2[1])
        {
            return SysErr;
        }

        if (sentchi < dt1[0] || sentchi > dt1[1])
        {
            return SysErr;
        }

        if (sentchi < dt2[0] || sentchi > dt2[1])
        {
            return SysErr;
        }

        double wk1 = (sentchi - dt1[0]) / (dt1[1] - dt1[0]);
        double wk2 = (dt1[1] - sentchi) / (dt1[1] - dt1[0]);
        double wk3 = Math.Abs(wk1 - wk2);

        wk1 = (sentchi - dt2[0]) / (dt2[1] - dt2[0]);
        wk2 = (dt2[1] - sentchi) / (dt2[1] - dt2[0]);
        double wk4 = Math.Abs(wk1 - wk2);

        return wk3 < wk4 ? (short)1 : (short)0;
    }

    /// <summary>
    /// 比較データ 1(今回)・2(前回)より、よりよいデータを選ぶ(特殊予約語 THSW/TM 用)。
    /// 【C原典】Fysk01_Choki_Cmp2(Fysk01.c:2779)。
    /// 各幅の中点と基準値 sentchi の距離を求め、今回幅(dt1)の方が近いなら入れ替え(1)を返す。
    /// </summary>
    /// <param name="sentchi">基準値(【C原典】sentchi)。</param>
    /// <param name="dt1">今回比較データ幅値[下端, 上端](【C原典】dt1)。</param>
    /// <param name="dt2">前回比較データ幅値[下端, 上端](【C原典】dt2)。</param>
    /// <returns>1:データ入れ替えをする / 0:入れ替えない(GOOD) / -1:システムエラー(SYS_ERR)。</returns>
    public static short ChokiCmp2(double sentchi, double[] dt1, double[] dt2)
    {
        ArgumentNullException.ThrowIfNull(dt1);
        ArgumentNullException.ThrowIfNull(dt2);

        short ret = Good;

        if (dt1[0] >= dt1[1])
        {
            return SysErr;
        }

        if (dt2[0] >= dt2[1])
        {
            return SysErr;
        }

        if (sentchi < dt1[0] || sentchi > dt1[1])
        {
            return SysErr;
        }

        if (sentchi < dt2[0] || sentchi > dt2[1])
        {
            return SysErr;
        }

        double wk1 = ((dt1[1] - dt1[0]) / 2.0) + dt1[0];
        double wk2 = Math.Abs(wk1 - sentchi);
        double wk3 = ((dt2[1] - dt2[0]) / 2.0) + dt2[0];
        double wk4 = Math.Abs(wk3 - sentchi);

        if (wk2 < wk4)
        {
            ret = 1;
        }

        return ret;
    }
}
