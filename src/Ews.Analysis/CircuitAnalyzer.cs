using Ews.Data.Abstractions;
using Ews.Data.SqlServer;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Masters;
using Ews.Domain.Projects;

namespace Ews.Analysis;

/// <summary>
/// 主回路設計メイン(ライブラリ本体)。
///
/// 【C原典】
///   - 本体  : libfysek.a の Fysk10_Main (主回路設計メイン, toku/sekkei/src/Fysk10.c)
///   - 入力  : 物件情報 FYDF801(bukken1/bukken2) / 回路内容記述 FYDF805(imagea) /
///             メーカー指定 FYDF802(makea) / 機器マスター FYDM805 ほか
///   - 出力  : 主回路エリア FYRT800 等 → 複合回路ファイル FYDF807
///
/// <c>Fysk10_Main</c> はライブラリ関数であり、fyskews だけでなく FySin80 / FySin40s5 /
/// AutoSinKairo / FyskEwsPnlMain など複数のプログラムから呼び出される。呼び出し側が
/// データ読込(Fysk_Set_data 相当)と行種別前処理を済ませた <c>imagea</c> を引数で受け取り、
/// 解析を行うのが原典の責務分担である。
///
/// したがって、fyskews 固有の行整形前処理(<see cref="CircuitLineNormalizer"/> =
/// FyskEwsMain.c の Fysk_* 群)や回路記述データのロードは本クラスでは行わず、
/// 呼び出し側(<c>CircuitAnalysisJob</c> = FyskEwsMain.c main 相当)の責務とする。
///
/// 【現状】縦断パイロットとして解析の骨組み(Fyss11 系統文字列チェック + 行単位の骨組み)を
/// 実装。行種別の詳細解析(機器選定・積算・複合回路展開)は <c>Fysk10_Main</c> の各サブ処理を
/// 順次移植して肉付けする。
/// </summary>
public sealed class CircuitAnalyzer
{
    private readonly SqlEquipmentMasterRepository _equipmentRepository;
    private readonly CircuitStringChecker _stringChecker;

    public CircuitAnalyzer(
        SqlEquipmentMasterRepository equipmentRepository,
        CircuitStringChecker stringChecker)
    {
        _equipmentRepository = equipmentRepository;
        _stringChecker = stringChecker;
    }

    /// <summary>
    /// 主回路設計メインを実行し、主回路結果の一覧を返す。
    /// 【C原典】Fysk10_Main()。呼び出し側が読込・前処理済みの回路内容記述(imagea)を受け取る。
    /// </summary>
    /// <param name="bukken1">物件明細エリア共通。【C原典】bukken1(FYDF801)。</param>
    /// <param name="bukken2">物件明細エリア明細。【C原典】bukken2(FYDF801)。</param>
    /// <param name="lines">
    /// 前処理済みの回路内容記述エリア(呼び出し側で <see cref="CircuitLineNormalizer"/> 適用済み)。
    /// 【C原典】imagec / imagea(FYDF805)。
    /// </param>
    public CircuitAnalysisResult Analyze(
        ProjectInfo bukken1,
        ProjectInfo bukken2,
        IReadOnlyList<CircuitDescriptionLine> lines)
    {
        var results = new List<MainCircuitResult>();
        var warnings = new List<string>();

        // 【C原典】Fysk10_Main → Fyss11_Mojiretu_Check。
        //         回路記述を系統/行種/仕様/機器テーブルへ展開し、記述エラーを収集する。
        CircuitParseResult parseResult = _stringChecker.Check(bukken1, bukken2, lines);
        foreach (CircuitParseError error in parseResult.Errors)
        {
            warnings.Add($"回路記述エラー: {error.ErrorCode} 行番号={error.LineNumber} ({error.MessageId})");
        }

        foreach (CircuitDescriptionLine line in lines)
        {
            // 行種(gyosyu)に応じた解析の振り分け。
            // 【C原典】Fysk10_Main 内の行種判定スイッチ(P/SP/TM/M/BO 等)。
            MainCircuitResult result = AnalyzeLine(line, warnings);
            results.Add(result);
        }

        // 【C原典】airaino / ameisano。結果の識別子は物件情報(新規登録依頼明細番号)から取得する。
        return new CircuitAnalysisResult(
            bukken2.NewRequestNumber, bukken2.NewItemNumber, results, warnings, parseResult);
    }

    /// <summary>
    /// 回路記述1行を解析して主回路結果を生成する。
    /// 【C原典】Fysk10_Main 内の行単位処理(機器選定区分・設定電流値の算出等)。
    /// </summary>
    private MainCircuitResult AnalyzeLine(CircuitDescriptionLine line, List<string> warnings)
    {
        var result = new MainCircuitResult
        {
            SequenceNumber = line.LineNumber.ToString("D3"),
            Work = new CircuitWork
            {
                // 行種先頭文字から機器選定区分を仮設定(詳細ロジックは順次移植)。
                EquipmentSelectionKind = line.LineType.Length > 0 ? line.LineType[0] : ' ',
            },
        };

        // 回路記述から品番を抽出し、機器マスターを引く想定(抽出規則は Fysk10 配下を移植)。
        // 【C原典】FyIsamRead(FYDM805, 品番) による定格容量・補助情報の取得。
        string? partNumber = TryExtractPartNumber(line.CircuitText);
        if (partNumber is not null)
        {
            (IsamStatus status, EquipmentMaster? master) = _equipmentRepository.Read(partNumber);
            if (status == IsamStatus.Ok && master is not null)
            {
                result.Work.RatedCapacity = NumericConverter.ParseImplicitDecimal(
                    master.ElectricalParameters, implicitDecimals: 0);
            }
            else
            {
                warnings.Add($"機器マスター未登録: 品番={partNumber} 行番号={line.LineNumber}");
            }
        }

        return result;
    }

    /// <summary>
    /// 回路記述文字列から品番を抽出する(暫定実装)。
    /// 【C原典】Fysk10 配下の回路記述パーサ(行種ごとのフォーマットに依存)。
    /// </summary>
    private static string? TryExtractPartNumber(string circuitText)
    {
        string trimmed = circuitText.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

/// <summary>
/// 回路解析の結果一式。
/// 【C原典】Fysk10_Main の出力(主回路エリア群 + 解析ログ/エラー FYRT805)。
/// </summary>
/// <param name="RequestNumber">依頼番号。</param>
/// <param name="ItemNumber">明細番号。</param>
/// <param name="MainCircuits">主回路結果。【C原典】FYRT800 群。</param>
/// <param name="Warnings">解析時の警告。【C原典】エラー領域 FYRT805 相当。</param>
/// <param name="ParseResult">系統文字列チェックの出力(系統/行種/仕様/機器テーブル)。【C原典】Fyss11_Mojiretu_Check 出力。</param>
public sealed record CircuitAnalysisResult(
    string RequestNumber,
    string ItemNumber,
    IReadOnlyList<MainCircuitResult> MainCircuits,
    IReadOnlyList<string> Warnings,
    CircuitParseResult ParseResult);
