using Ews.Domain.Analysis;
using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// INV オプション機器の機器マスタ検索用データ(FYDF812)を、入力 kw に対応する
/// 定格(品名)で設定する。
/// 【C原典】Fysk01_Kiki_Set_INV_OP_teikaku(toku/sekkei/src/Fysk01.c:6006)。
///   コンスタント読込(ReadCnstINV_OP)後、入力 kw から直近上位の定格を選定
///   (<see cref="InverterOptionKwSelector"/>)し、該当があれば予約語 PT・メーカーコード M・
///   パラメータタイプ全枠空白・定格キー=選定定格 を設定する。該当なしは NOGOOD。
///
/// ラジオノイズフィルタ用の固定「FR-BIF」版は <see cref="InverterOptionRadioNoiseSearchKey"/>。
/// </summary>
public static class InverterOptionSearchKey
{
    /// <summary>正常終了。【C原典】GOOD(fyrt808.h)。</summary>
    public const int Good = 0;

    /// <summary>該当なし(機器選定エラー)。【C原典】NOGOOD(fyrt808.h)。</summary>
    public const int NoGood = 1;

    /// <summary>予約語(PT 固定)。【C原典】key.yoyaku[8]="PT      "。</summary>
    public const string ReservedWord = "PT      ";

    /// <summary>メーカーコード(M 固定)。【C原典】key.mkcd[3]="M  "。</summary>
    public const string MakerCode = "M  ";

    private const int ParameterTypeSlotCount = 7;
    private const int ParameterTypeWidth = 7;
    private const int RatingKeyWidth = 80;

    /// <summary>
    /// 入力 kw に対応する定格を選定し、検索用データを設定する。
    /// 【C原典】Fysk01_Kiki_Set_INV_OP_teikaku(inputKW, cdata, filename)。
    /// 選定定格が空(該当なし)の場合は cdata を変更せず <see cref="NoGood"/> を返す。
    /// </summary>
    /// <param name="cdata">直近上下位参照データ(I/O)。【C原典】struct FYDF812 *cdata。</param>
    /// <param name="constants">INV オプションコンスタント(invop_prm [])。【C原典】ReadCnstINV_OP の結果。</param>
    /// <param name="inputKw">入力 kw 値。【C原典】inputKW。</param>
    public static int Apply(
        NearestRankReference cdata, IReadOnlyList<InverterOptionConstant> constants, double inputKw)
    {
        ArgumentNullException.ThrowIfNull(cdata);
        ArgumentNullException.ThrowIfNull(constants);

        // 【C原典】Fysk01_ChkInvKw_OP で入力 kw から直近上位定格を検索。
        string? teikaku = InverterOptionKwSelector.SelectProductName(constants, inputKw);

        // 【C原典】直近上位がない場合は機器選定エラー(cdata 未設定)。
        if (string.IsNullOrEmpty(teikaku))
        {
            return NoGood;
        }

        cdata.ReservedWord = ReservedWord;
        cdata.MakerCode = MakerCode;

        var blankTypes = new string[ParameterTypeSlotCount];
        for (int i = 0; i < ParameterTypeSlotCount; i++)
        {
            blankTypes[i] = new string(' ', ParameterTypeWidth);
        }
        cdata.ParameterTypes = blankTypes;

        cdata.EquipmentMasterRatingKey = teikaku.PadRight(RatingKeyWidth);

        return Good;
    }
}
