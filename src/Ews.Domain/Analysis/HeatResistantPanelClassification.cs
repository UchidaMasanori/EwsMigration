namespace Ews.Domain.Analysis;

/// <summary>
/// 耐熱盤部材判定用 自由文字コンスタントファイル(tainetuPT.cns)の1レコード。
///
/// 【C原典】struct taiPT_prm(toku/include/sekkei/struct.h:263, 改訂&lt;13&gt;)。
///   gyono(行番号) / jiyuumoji[81](自由文字) / bunrui(自由文字分類 'A'-'K')。
///   <see cref="FreeText"/> は原典ローダ(Fysk01_ReadCnst_TainetuBOX)が
///   先頭空白までで切り詰めた状態を保持する。
/// </summary>
/// <param name="LineNumber">行番号(gyono)。0=1行一致 / 1=2行一致の1行目 / 2=2行目。</param>
/// <param name="FreeText">自由文字(jiyuumoji)。先頭空白までに切詰め済み。</param>
/// <param name="Category">自由文字分類(bunrui)。'A'-'K'。</param>
public sealed record HeatResistantPanelClassificationConstant(int LineNumber, string FreeText, char Category);

/// <summary>
/// 耐熱盤部材判定結果ワーク。系統ごとに最大1件の分類を保持する。
///
/// 【C原典】struct taiPT_tmp(toku/include/sekkei/struct.h:269, 改訂&lt;13&gt;)。
///   kno(系統番号) / sou(相数) / sen(線数) / bunrui(自由文字分類)。
/// </summary>
/// <param name="SystemNumber">系統番号(kno)。</param>
/// <param name="PhaseCount">相数(sou)。</param>
/// <param name="WireCount">線数(sen)。</param>
/// <param name="Category">自由文字分類(bunrui)。</param>
public sealed record HeatResistantPanelClassificationResult(int SystemNumber, int PhaseCount, int WireCount, char Category);
