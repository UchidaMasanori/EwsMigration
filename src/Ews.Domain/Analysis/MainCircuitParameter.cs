namespace Ews.Domain.Analysis;

/// <summary>
/// 主回路パラメータ(相・線式・極数・電圧・AC/DC 区分)。
/// 【C原典】toku/sekkei/src/Fyss14.c:106 struct MCPRMS。
///
/// 機器選定・上流パラメータ生成(Fyss13-15)で相・線式・極数・回路電圧を
/// 受け渡す中核構造体。C 原典のフィールド並び(ph, wr, p, v[3], vkbn)を
/// そのまま保持する。electrical パラメータ整形の各テーブル照合
/// (Element_Gen / Pole_Gen / Volt_Conv 等)がこの構造体を入出力とする。
/// </summary>
public sealed class MainCircuitParameter
{
    /// <summary>相。【C原典】SHORT ph。</summary>
    public short Phase { get; set; }

    /// <summary>線式。【C原典】SHORT wr。</summary>
    public short WireType { get; set; }

    /// <summary>極数。【C原典】SHORT p。</summary>
    public short Pole { get; set; }

    /// <summary>回路電圧(3 要素)。【C原典】SHORT v[3]。</summary>
    public short[] Voltage { get; } = new short[3];

    /// <summary>AC/DC 区分。【C原典】SHORT vkbn。</summary>
    public short AcDcKind { get; set; }
}
