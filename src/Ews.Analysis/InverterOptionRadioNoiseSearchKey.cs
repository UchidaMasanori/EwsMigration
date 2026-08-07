using Ews.Domain.Masters;

namespace Ews.Analysis;

/// <summary>
/// INV オプション機器(ラジオノイズフィルタ)の機器マスタ検索用データ(FYDF812)を設定する。
/// 【C原典】Fysk01_Kiki_Set_INV_OP_teikaku_RN(toku/sekkei/src/Fysk01.c:6065)。
///   予約語 PT・メーカーコード M 固定、パラメータタイプは全てなし、定格キーは品名固定「FR-BIF」。
/// </summary>
public static class InverterOptionRadioNoiseSearchKey
{
    /// <summary>予約語(PT 固定)。【C原典】key.yoyaku[8]="PT      "。</summary>
    public const string ReservedWord = "PT      ";

    /// <summary>メーカーコード(M 固定)。【C原典】key.mkcd[3]="M  "。</summary>
    public const string MakerCode = "M  ";

    /// <summary>定格キーの品名(固定値)。【C原典】teikkey に sprintf("FR-BIF")。</summary>
    public const string FixedProductName = "FR-BIF";

    private const int ParameterTypeSlotCount = 7;
    private const int ParameterTypeWidth = 7;
    private const int RatingKeyWidth = 80;

    /// <summary>
    /// 検索用データを設定する。【C原典】Fysk01_Kiki_Set_INV_OP_teikaku_RN(cdata)。
    /// パラメータタイプは全枠空白、定格キーは「FR-BIF」+空白埋め(80 桁)。他フィールドは変更しない。
    /// </summary>
    /// <param name="cdata">直近上下位参照データ(I/O)。【C原典】struct FYDF812 *cdata。</param>
    public static void Apply(NearestRankReference cdata)
    {
        ArgumentNullException.ThrowIfNull(cdata);

        cdata.ReservedWord = ReservedWord;
        cdata.MakerCode = MakerCode;

        var blankTypes = new string[ParameterTypeSlotCount];
        for (int i = 0; i < ParameterTypeSlotCount; i++)
        {
            blankTypes[i] = new string(' ', ParameterTypeWidth);
        }
        cdata.ParameterTypes = blankTypes;

        cdata.EquipmentMasterRatingKey = FixedProductName.PadRight(RatingKeyWidth);
    }
}
