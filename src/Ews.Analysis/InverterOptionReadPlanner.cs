namespace Ews.Analysis;

/// <summary>
/// <see cref="InverterOptionReadPlanner.Plan"/> の結果。機器マスタ読込に進むか、
/// パラメータ未指定/KW 未入力で打ち切るかの判定を表す。
/// </summary>
/// <param name="Status">判定結果。0:続行 / 1:KW 入力無しエラー / 2:パラメータ指定無し。</param>
/// <param name="InputKw">入力 KW 値(付属パラメータ fpalw2 から換算)。</param>
/// <param name="ConstantFileName">読み込む定格コンスタントファイル名。RN 時は null。</param>
/// <param name="UseRadioNoise">ラジオノイズフィルタ(RN)専用処理を使うか。</param>
public sealed record InverterOptionReadPlan(
    int Status,
    double InputKw,
    string? ConstantFileName,
    bool UseRadioNoise);

/// <summary>
/// INV オプション機器の機器マスタ検索前に、パラメータ指定有無・KW 入力有無を判定し、
/// 定格コンスタントファイル(またはラジオノイズフィルタ専用処理)へ振り分ける。
///
/// 【C原典】Fysk01_Kiki_Read_INV_OP(toku/sekkei/src/Fysk01.c:5762, 改訂&lt;38&gt;)の
///   機器マスタ読込前の前提判定部。opno==4(MC 機械連動子)はパラメータチェックを行わず、
///   それ以外は dtype[opno+2] 先頭2バイトが optype[opno] と一致しなければ 2(指定無し)。
///   付属パラメータ fpalw2 が TOL(0.001) 超なら inputKW=(fpalw2/10)/100.0、そうでなく
///   opno!=2(RN 以外)なら 1(KW 入力無し)。opno に応じ invAC/DC/LN/MC.cns へ振り分け、
///   opno==2 は kw 入力を持たないラジオノイズフィルタ専用処理を使う。
///   本移植では機器マスタ(FYDM805)の ISAM 読込はリポジトリ側へ委譲する。
/// </summary>
public static class InverterOptionReadPlanner
{
    /// <summary>続行(機器マスタ読込へ進む)。</summary>
    public const int ProceedStatus = 0;

    /// <summary>KW 入力無しエラー。【C原典】return 1。</summary>
    public const int KwMissingStatus = 1;

    /// <summary>パラメータ指定無し(出庫対象外)。【C原典】return 2。</summary>
    public const int ParameterNotSpecifiedStatus = 2;

    /// <summary>ラジオノイズフィルタのオプション番号。【C原典】opno==2。</summary>
    public const int RadioNoiseOpno = 2;

    /// <summary>MC 機械連動子のオプション番号(パラメータチェック対象外)。【C原典】opno==4。</summary>
    public const int MechanicalInterlockOpno = 4;

    /// <summary>付属パラメータ有無の判定しきい値。【C原典】TOL(fyrt808.h:25)。</summary>
    private const double Tolerance = 0.001;

    /// <summary>オプション種別コード。【C原典】optype[5][3]={"AC","DC","RN","LN","MC"}。</summary>
    private static readonly string[] OptionTypes = ["AC", "DC", "RN", "LN", "MC"];

    /// <summary>オプション別定格コンスタントファイル名(RN は専用処理のため null)。</summary>
    private static readonly string?[] ConstantFiles =
        ["invAC.cns", "invDC.cns", null, "invLN.cns", "invMC.cns"];

    /// <summary>
    /// INV オプション機器の機器マスタ読込前提を判定する。
    /// </summary>
    /// <param name="opno">オプション機器番号(0:AC 1:DC 2:RN 3:LN 4:MC)。</param>
    /// <param name="parameterType">dtype[opno+2] スロット(先頭2バイトを種別と照合)。</param>
    /// <param name="loadWatt">付属パラメータ fpalw2(LibCharToInt 済みの整数値)。</param>
    public static InverterOptionReadPlan Plan(int opno, string parameterType, int loadWatt)
    {
        ArgumentNullException.ThrowIfNull(parameterType);
        if (opno < 0 || opno >= OptionTypes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(opno));
        }

        // 【C原典】opno==4(MC)はパラメータチェックしない。それ以外は種別2バイト一致を要求。
        if (opno != MechanicalInterlockOpno && !MatchesOptionType(parameterType, opno))
        {
            return new InverterOptionReadPlan(ParameterNotSpecifiedStatus, 0.0, null, false);
        }

        // 【C原典】fpalw2>TOL で inputKW=(fpalw2/10)/100.0、無入力かつ opno!=2 は KW 無しエラー。
        double inputKw = 0.0;
        if (loadWatt > Tolerance)
        {
            inputKw = (loadWatt / 10) / 100.0;
        }
        else if (opno != RadioNoiseOpno)
        {
            return new InverterOptionReadPlan(KwMissingStatus, 0.0, null, false);
        }

        bool useRadioNoise = opno == RadioNoiseOpno;
        return new InverterOptionReadPlan(ProceedStatus, inputKw, ConstantFiles[opno], useRadioNoise);
    }

    // 【C原典】strncmp(dtype[opno+2], optype[opno], 2)==0。
    private static bool MatchesOptionType(string parameterType, int opno)
    {
        string code = OptionTypes[opno];
        return parameterType.Length >= code.Length
            && parameterType.AsSpan(0, code.Length).SequenceEqual(code);
    }
}
