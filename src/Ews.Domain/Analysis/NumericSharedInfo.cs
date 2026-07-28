namespace Ews.Domain.Analysis;

/// <summary>
/// 直近上下位共用情報の数値変換データ。
///
/// 【C原典】struct kyoyojg_s (toku/include/sekkei/fyscommon.h)。
///
/// <c>Fysk01_Change_Chokin</c>(=SharedInfoConverter.Convert)が
/// <see cref="Ews.Domain.Masters.NearestRankSharedInfo"/>(文字列)を数値化した結果。
/// 定格値チェック(Fysk02_Check_Teikakuchi)が電圧・感度電流の照合に用いる。
/// </summary>
public sealed class NumericSharedInfo
{
    /// <summary>主電源 AC/DC 共用区分。【C原典】ksadkbn。</summary>
    public char MainPowerSharedAcDc { get; set; } = ' ';

    /// <summary>制御電源 AC/DC 共用区分。【C原典】kcadkbn。</summary>
    public char ControlPowerSharedAcDc { get; set; } = ' ';

    /// <summary>
    /// 感度電流(MA)。【C原典】km_s.kyomad[3](DOUBLE[3])。
    /// 元データは 4 枠だが数値構造体は 3 枠で、変換時に 4 枠目は隣接領域へ溢れて
    /// 直後に上書きされ破棄される(原典の挙動)。よって 3 個のみを保持する。
    /// </summary>
    public IReadOnlyList<double> SensitivityCurrents { get; set; } = [];

    /// <summary>一次定格電圧の値(3 個)。【C原典】kv1_s.kyov1d1/d2/d3。</summary>
    public IReadOnlyList<double> PrimaryVoltageValues { get; set; } = [];

    /// <summary>一次定格電圧の区分(2 個)。【C原典】kv1_s.kyov1k1/k2。</summary>
    public IReadOnlyList<char> PrimaryVoltageKinds { get; set; } = [];

    /// <summary>二次定格電圧の値(4 個)。【C原典】kv2_s.kyov2d1/d2/d3/d4。</summary>
    public IReadOnlyList<double> SecondaryVoltageValues { get; set; } = [];

    /// <summary>
    /// 二次定格電圧の区分(3 個)。【C原典】kv2_s.kyov2k1/k2/k3。
    /// k1 は原典で一次電圧区分(kv1k1)から複写される(コピー由来の仕様)。
    /// </summary>
    public IReadOnlyList<char> SecondaryVoltageKinds { get; set; } = [];

    /// <summary>制御電圧の値(4 個)。【C原典】kvc_s.kyovcd1/d2/d3/d4。</summary>
    public IReadOnlyList<double> ControlVoltageValues { get; set; } = [];

    /// <summary>
    /// 制御電圧の区分(3 個)。【C原典】kvc_s.kyovck1/k2/k3。
    /// k1 は一次電圧区分(kv1k1)、k2/k3 は二次電圧区分(kv2k2/kv2k3)から
    /// 複写される(コピー由来の仕様)。制御電圧自身の区分は使われない。
    /// </summary>
    public IReadOnlyList<char> ControlVoltageKinds { get; set; } = [];

    /// <summary>制御電圧適応範囲(from)。【C原典】vcfrom(=Stof(vcfrom)/100、0 なら 1.0)。</summary>
    public double ControlVoltageRangeFrom { get; set; }

    /// <summary>制御電圧適応範囲(to)。【C原典】vcto(=Stof(vcto)/100、0 なら 1.0)。</summary>
    public double ControlVoltageRangeTo { get; set; }
}
