namespace Ews.Domain.Analysis;

/// <summary>
/// スターデルタ用 MC/THR 選定容量テーブルの 1 行。
/// 【C原典】struct mcthr_seltbl(toku/include/sekkei/struct.h:220)。
///   出力容量・電圧をキーに、MC(品名52/42/6)とサーマルリレー(THR)のヒータ呼び容量を保持する。
///   選定機器品番(mc1_nm/mc2_nm/mc3_nm/thr_nm)は参考用のため本移植では保持しない。
/// </summary>
/// <param name="Voltage">電圧(V)。【C原典】denatu[3]。</param>
/// <param name="OutputCapacity">出力容量(W)。【C原典】youryo[7]。</param>
/// <param name="HeaterCapacity52">MC 品名52 のヒータ呼び容量。【C原典】mc52[6]。</param>
/// <param name="HeaterCapacity42">MC 品名42 のヒータ呼び容量。【C原典】mc42[6]。</param>
/// <param name="HeaterCapacity6">MC 品名6 のヒータ呼び容量。【C原典】mc6[6]。</param>
/// <param name="ThermalHeaterCapacity">サーマル(THR)のヒータ呼び容量。【C原典】thr_h[6]。</param>
public sealed record StarDeltaCapacityEntry(
    string Voltage,
    string OutputCapacity,
    string HeaterCapacity52,
    string HeaterCapacity42,
    string HeaterCapacity6,
    string ThermalHeaterCapacity);
