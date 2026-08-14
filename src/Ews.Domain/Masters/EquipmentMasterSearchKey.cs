namespace Ews.Domain.Masters;

/// <summary>
/// 機器マスタ(FYDM805)の基本検索キー生成。
/// 【C原典】Fysk01_Kiki_Read(toku/sekkei/src/Fysk01.c:2821)のキー生成部。
///
/// 直近上下位該当データ(FYDF812)の予約語 yoyaku・メーカーコード mkcd・
/// パラメータタイプ ptype[7][7]・定格キー teikkey をそのまま機器マスタ(FYDM805)の
/// 主キー(struct p805_key)へ写して 1 件読む。ただしパラメータタイプの各スロット
/// (7 バイト×7)先頭が "HL"(ハンドルロック指定)のときは、その 2 バイトを空白へ
/// 置き換えて検索キーから除外する(改訂&lt;19&gt;)。
/// </summary>
public static class EquipmentMasterSearchKey
{
    /// <summary>パラメータタイプのスロット数。【C原典】ptype[7]。</summary>
    public const int ParameterTypeSlotCount = 7;

    /// <summary>パラメータタイプ 1 スロットのバイト長。【C原典】ptype[..][7]。</summary>
    public const int ParameterTypeSlotSize = 7;

    /// <summary>パラメータタイプ全体のバイト長(7×7)。</summary>
    public const int ParameterTypeLength = ParameterTypeSlotCount * ParameterTypeSlotSize;

    /// <summary>ハンドルロック指定マーカー。【C原典】memcmp(ptype[i], "HL", 2)。</summary>
    public const string HandleLockMarker = "HL";

    /// <summary>
    /// パラメータタイプ(49 バイト)からハンドルロック指定 "HL" を取り除く。
    /// 【C原典】各スロット先頭 2 バイトが "HL" なら memcpy(ptype[i], "  ", 2) で
    /// 先頭 2 バイトのみ空白化する(スロット内の残り 5 バイトは保持)。
    /// 戻り値は 49 バイト固定幅の正規化済みパラメータタイプ。
    /// </summary>
    public static string NormalizeParameterType(string parameterType)
    {
        parameterType ??= string.Empty;

        char[] buffer = parameterType.Length >= ParameterTypeLength
            ? parameterType[..ParameterTypeLength].ToCharArray()
            : parameterType.PadRight(ParameterTypeLength).ToCharArray();

        for (int slot = 0; slot < ParameterTypeSlotCount; slot++)
        {
            int at = slot * ParameterTypeSlotSize;
            if (buffer[at] == 'H' && buffer[at + 1] == 'L')
            {
                buffer[at] = ' ';
                buffer[at + 1] = ' ';
            }
        }

        return new string(buffer);
    }
}
