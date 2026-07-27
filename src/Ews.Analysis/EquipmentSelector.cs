namespace Ews.Analysis;

/// <summary>
/// 機器選定(直近上下位ファイル検索での候補比較)。
///
/// 【C原典】toku/sekkei/src/Fysk01.c
///   - <c>Fysk01_Data_Cmp(SHORT no, DOUBLE schi, DOUBLE dat1a[], DOUBLE dat1v,
///     struct FYDF812 dat1, DOUBLE dat2a[], DOUBLE dat2v, struct FYDF812 dat2)</c>(Fysk01.c:2668)
///     … 特殊予約語 THR(PC_1) / MG(PC_3) 用。今回候補(dat1)と前回候補(dat2)を比較し、
///       今回で入れ替えるべきか(1)を判定する。<c>Fysk01_Choki_Cmp1</c> を内部で使う。
///   - <c>Fysk01_Choki_Cmp1(DOUBLE sentchi, DOUBLE dt1[], DOUBLE dt2[])</c>(Fysk01.c:2734)
///     … 特殊予約語 THR / MG / XERY 用。基準値 sentchi が今回幅 dt1[0..1]・前回幅 dt2[0..1] の
///       どちらへより「均等に収まるか」で優劣を判定する。
///   - <c>Fysk01_Choki_Cmp2(DOUBLE sentchi, DOUBLE* dt1, DOUBLE* dt2)</c>(Fysk01.c:2779)
///     … 特殊予約語 THSW / TM 用。基準値 sentchi と各幅の中点の距離で優劣を判定する。
///
/// いずれもマスタ(ISAM)・記録列・物件に依存しない純粋数値関数で、機器マスタから複数候補が
/// ヒットした際に「よりよい候補」を選ぶ(=前回候補を今回候補で入れ替えるか)の判定に使う。
/// <c>Fysk01_Data_Cmp</c> は FYDF812 から定格値キー(<c>key.kteichi</c>[50])のみを参照するため、
/// 本移植では構造体全体でなく当該キー文字列を受け取る自己完結シグネチャとする。
///
/// 定数(【C原典】fyrt808.h): GOOD=0 / SYS_ERR=-1 / TOL=0.001 / PC_1=11(THR) / PC_3=13(MG)。
/// </summary>
public static class EquipmentSelector
{
    /// <summary>【C原典】GOOD(fyrt808.h:31)。良好(入れ替えない)。</summary>
    private const short Good = 0;

    /// <summary>【C原典】SYS_ERR(fyrt808.h:35)。システムエラー(不正な幅・範囲外)。</summary>
    private const short SysErr = -1;

    /// <summary>【C原典】TOL(fyrt808.h:25)。実数比較の許容誤差。</summary>
    private const double Tol = 0.001;

    /// <summary>【C原典】PC_1(fyrt808.h:38)。特殊予約語 THR の処理番号。</summary>
    public const short Pc1Thr = 11;

    /// <summary>【C原典】PC_3(fyrt808.h:40)。特殊予約語 MG の処理番号。</summary>
    public const short Pc3Mg = 13;

    /// <summary>
    /// 条件に合ったデータ(候補)を選ぶ(特殊予約語 THR/MG 用)。
    /// 【C原典】Fysk01_Data_Cmp(Fysk01.c:2668)。
    /// 今回候補(dat1)を前回候補(dat2)と比較し、今回で入れ替えるべきなら 1 を返す。
    /// MG(PC_3)は代表値 dat1v/dat2v の小さい方を優先し、同値かつ幅一致なら定格値キーの辞書順、
    /// さらに同じなら <see cref="ChokiCmp1"/> の均等度で決める。THR(PC_1)は代表値比較を行わず、
    /// 幅一致時にキー辞書順→均等度で決める。それ以外の処理番号は常に 0(入れ替えない)。
    /// </summary>
    /// <param name="no">処理番号(【C原典】no)。<see cref="Pc1Thr"/> または <see cref="Pc3Mg"/>。</param>
    /// <param name="schi">基準値(【C原典】schi、通常はトリップ電流 epaat)。</param>
    /// <param name="dat1a">今回候補の幅値[下端, 上端](【C原典】dat1a)。</param>
    /// <param name="dat1v">今回候補の代表値(【C原典】dat1v)。</param>
    /// <param name="dat1Kteichi">今回候補の定格値キー(【C原典】dat1.key.kteichi[50])。</param>
    /// <param name="dat2a">前回候補の幅値[下端, 上端](【C原典】dat2a)。</param>
    /// <param name="dat2v">前回候補の代表値(【C原典】dat2v)。</param>
    /// <param name="dat2Kteichi">前回候補の定格値キー(【C原典】dat2.key.kteichi[50])。</param>
    /// <returns>1:今回候補で入れ替える / 0:入れ替えない。</returns>
    public static short DataCmp(
        short no,
        double schi,
        double[] dat1a,
        double dat1v,
        string dat1Kteichi,
        double[] dat2a,
        double dat2v,
        string dat2Kteichi)
    {
        ArgumentNullException.ThrowIfNull(dat1a);
        ArgumentNullException.ThrowIfNull(dat2a);
        ArgumentNullException.ThrowIfNull(dat1Kteichi);
        ArgumentNullException.ThrowIfNull(dat2Kteichi);

        short k = 0;

        if (no == Pc3Mg)
        {
            // MG: 代表値の小さい方を優先。
            if (dat1v < dat2v)
            {
                k = 1;
            }
            else if (Math.Abs(dat1v - dat2v) < Tol)
            {
                if (Math.Abs(dat1a[0] - dat2a[0]) < Tol && Math.Abs(dat1a[1] - dat2a[1]) < Tol)
                {
                    if (string.CompareOrdinal(dat1Kteichi, dat2Kteichi) < 0)
                    {
                        k = 1;
                    }
                    else if (ChokiCmp1(schi, dat1a, dat2a) == 1)
                    {
                        k = 1;
                    }
                }
            }
        }
        else if (no == Pc1Thr)
        {
            // THR: 代表値比較なし。幅一致時にキー辞書順→均等度。
            if (Math.Abs(dat1a[0] - dat2a[0]) < Tol && Math.Abs(dat1a[1] - dat2a[1]) < Tol)
            {
                if (string.CompareOrdinal(dat1Kteichi, dat2Kteichi) < 0)
                {
                    k = 1;
                }
                else if (ChokiCmp1(schi, dat1a, dat2a) == 1)
                {
                    k = 1;
                }
            }
        }

        return k;
    }

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
