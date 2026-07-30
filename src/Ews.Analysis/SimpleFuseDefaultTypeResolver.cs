using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// ヒューズ(F)のデフォルト機器タイプ設定(LAMP22 無効時の簡易版)。
/// 【C原典】PropChgFuseType_SY2(Fysk00.c:6959, 改訂&lt;83&gt;)。
///   回路内容記述にタイプ指定 "+(" が無く、特注/ブロックコンポ(cpf=0)なら機器タイプを "GT"、
///   メーカー指定 "MK=" が無ければメーカーを FT にする。地区グループ・品番情報・後続ランプには依存しない。
///   (LAMP22 有効時の詳細版は <see cref="FuseDefaultTypeResolver"/>=PropChgFuseType_SY)
/// </summary>
public sealed class SimpleFuseDefaultTypeResolver
{
    private const string GtType = "GT     ";

    private readonly CircuitDescriptionArea _circuitDescriptions;

    public SimpleFuseDefaultTypeResolver(CircuitDescriptionArea circuitDescriptions)
    {
        ArgumentNullException.ThrowIfNull(circuitDescriptions);
        _circuitDescriptions = circuitDescriptions;
    }

    /// <summary>ヒューズの機器タイプ・メーカーを調整する。</summary>
    /// <param name="fuse">ヒューズの主回路レコード。【C原典】sk。</param>
    /// <param name="makerCodes">メーカーコード選定順位(破壊的に更新)。【C原典】mcod。</param>
    /// <param name="dataTypes">機器タイプ(7×7, 破壊的に更新)。【C原典】dtype。</param>
    /// <param name="displayTypes">表示用機器タイプ(7×7, 破壊的に更新)。【C原典】wtype。</param>
    /// <param name="specKind">仕様(0:特注/ブロック 1:コンポ)。【C原典】cpf。</param>
    public void Resolve(MainCircuitResult fuse, string[] makerCodes, string[] dataTypes,
                        string[] displayTypes, int specKind)
    {
        ArgumentNullException.ThrowIfNull(fuse);
        ArgumentNullException.ThrowIfNull(makerCodes);
        ArgumentNullException.ThrowIfNull(dataTypes);
        ArgumentNullException.ThrowIfNull(displayTypes);

        if (!Matches(fuse.Data.ReservedWord, "F ", 2))
        {
            return;
        }

        string kairoar = _circuitDescriptions.GetDescriptionAt(
            fuse.Data.DescriptionRow, fuse.Data.DescriptionColumn);
        if (kairoar.Length == 0)
        {
            return;         // 取得 NG
        }

        if (!Matches(makerCodes[0], "K  ", 3))
        {
            return;
        }

        string defType = string.Empty;
        if (!Contains(kairoar, "+("))       // タイプ指定なし
        {
            if (specKind == 0)              // 特注、ブロックコンポ
            {
                defType = GtType;
            }
        }

        if (defType.Length > 0)
        {
            displayTypes[0] = defType;
            dataTypes[0] = defType;
            fuse.Data.DataType[0] = defType;
        }

        // メーカー入力なしで機器タイプが GT のとき、特注/ブロックはメーカーを FT にする。
        if (!Contains(kairoar, "MK=") &&
            Matches(dataTypes[0], "GT ", 3) &&
            specKind == 0)
        {
            makerCodes[0] = "FT ";
        }
    }

    private static bool Contains(string value, string token) =>
        value.Contains(token, StringComparison.Ordinal);

    private static bool Matches(string value, string expected, int width) =>
        string.CompareOrdinal(value.PadRight(width)[..width], expected.PadRight(width)[..width]) == 0;
}
