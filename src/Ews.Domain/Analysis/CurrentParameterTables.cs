namespace Ews.Domain.Analysis;

/// <summary>
/// パラメータ設定タイプの 1 行(amp001.cns)。
/// 【C原典】<c>struct prmtp</c>(toku/include/sekkei/fyss3g01.h)。
///   予約語をキーに、電流に関するパラメータのセット処理タイプ(<see cref="SettingType"/> = PRM_*)と
///   AT/AF/MA/A2/A1 の [1]/[2] 設定フラグ 10 個を保持する。
///   線形リスト(<c>PRMTP_T</c>)のノードは <see cref="Ews.Data.Seeding"/> のローダーが
///   <c>IReadOnlyList</c> として構築する。
/// </summary>
/// <param name="ReservedWord">予約語。【C原典】yoyaku[8](Strset で末尾空白詰めされるが本移植は末尾空白を除いた論理値を保持)。</param>
/// <param name="SequenceNumber">シーケンスナンバー。【C原典】seq_no。</param>
/// <param name="SettingType">設定タイプ(PRM_* 定数)。【C原典】prm_tp。</param>
/// <param name="SettingFlags">設定フラグ 10 個(AT/AF/MA/A2/A1 の [2],[1] 各 5 対)。【C原典】cod[10]。</param>
public sealed record ParameterSettingType(
    string ReservedWord,
    int SequenceNumber,
    int SettingType,
    IReadOnlyList<int> SettingFlags);

/// <summary>
/// 電線サイズ設定の 1 行(amp002.cns)。
/// 【C原典】<c>struct sqset</c>(fyss3g01.h)。電線サイズ(より線)と許容電流・選定フラグを保持する。
/// </summary>
/// <param name="WireSize">電線サイズ(より線)。【C原典】sq。</param>
/// <param name="AllowableCurrent">許容電流(A)。【C原典】denryu。</param>
/// <param name="SelectionFlag">選定フラグ。【C原典】sentei。</param>
public sealed record WireSizeSetting(
    double WireSize,
    double AllowableCurrent,
    int SelectionFlag);

/// <summary>
/// 定格電流２設定の 1 行(amp003.cns)。
/// 【C原典】<c>struct a2set</c>(fyss3g01.h)。負荷種類・回路相数・回路電圧をキーに、
///   定格電流算出係数(<see cref="Coefficient"/>)を保持する。
/// </summary>
/// <param name="LoadKind">負荷種類。【C原典】fpalw1[2](Strset で末尾空白詰め、本移植は末尾空白を除いた論理値)。</param>
/// <param name="CircuitPhase">回路相数。【C原典】kpaph('0':DC '1':単相 '3':三相)。</param>
/// <param name="CircuitVoltage">回路電圧[0](999 は全電圧該当)。【C原典】kpa。</param>
/// <param name="Coefficient">定格電流算出係数。【C原典】kei。</param>
public sealed record RatedCurrent2Setting(
    string LoadKind,
    char CircuitPhase,
    int CircuitVoltage,
    double Coefficient);

/// <summary>
/// 定格電流１設定の 1 行(amp004.cns)。
/// 【C原典】<c>struct a1set</c>(fyss3g01.h)。標準定格電流の 1 値を保持する。
/// </summary>
/// <param name="RatedCurrent">定格電流。【C原典】key。</param>
public sealed record RatedCurrent1Setting(
    double RatedCurrent);
