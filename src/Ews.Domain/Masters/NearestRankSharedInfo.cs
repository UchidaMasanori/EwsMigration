namespace Ews.Domain.Masters;

/// <summary>
/// 直近上下位参照ファイルの共用情報部(生の固定長文字列)。
///
/// 【C原典】struct kyoyojg (toku/include/common/fydf812.h の struct FYDF812.jg)。
///
/// 主/制御電源の共用区分・感度電流・一次/二次定格電圧・制御電圧の各値と区分を
/// 文字列のまま保持する。数値への変換は <c>Fysk01_Change_Chokin</c>
/// (=SharedInfoConverter.Convert)で行い、定格値チェック(Check_Teikakuchi)が
/// 参照する数値共用情報(kyoyojg_s)を得る。
/// </summary>
public sealed class NearestRankSharedInfo
{
    /// <summary>感度電流の枠数。【C原典】km.kyomad[4][4]。</summary>
    public const int SensitivityCurrentCount = 4;

    /// <summary>主電源 AC/DC 共用区分。【C原典】jg.ksadkbn。</summary>
    public char MainPowerSharedAcDc { get; set; } = ' ';

    /// <summary>制御電源 AC/DC 共用区分。【C原典】jg.kcadkbn。</summary>
    public char ControlPowerSharedAcDc { get; set; } = ' ';

    /// <summary>感度電流(MA, 4 枠)。【C原典】jg.km.kyomad[4][4](各 4 文字)。</summary>
    public IReadOnlyList<string> SensitivityCurrents { get; set; } = [];

    /// <summary>一次定格電圧の値(3 個)。【C原典】jg.kv1.kyov1d1/d2/d3。</summary>
    public IReadOnlyList<string> PrimaryVoltageValues { get; set; } = [];

    /// <summary>一次定格電圧の区分(2 個)。【C原典】jg.kv1.kyov1k1/k2。</summary>
    public IReadOnlyList<char> PrimaryVoltageKinds { get; set; } = [];

    /// <summary>二次定格電圧の値(4 個)。【C原典】jg.kv2.kyov2d1/d2/d3/d4。</summary>
    public IReadOnlyList<string> SecondaryVoltageValues { get; set; } = [];

    /// <summary>二次定格電圧の区分(3 個)。【C原典】jg.kv2.kyov2k1/k2/k3。</summary>
    public IReadOnlyList<char> SecondaryVoltageKinds { get; set; } = [];

    /// <summary>制御電圧の値(4 個)。【C原典】jg.kvc.kyovcd1/d2/d3/d4。</summary>
    public IReadOnlyList<string> ControlVoltageValues { get; set; } = [];

    /// <summary>制御電圧の区分(3 個)。【C原典】jg.kvc.kyovck1/k2/k3。</summary>
    public IReadOnlyList<char> ControlVoltageKinds { get; set; } = [];
}
