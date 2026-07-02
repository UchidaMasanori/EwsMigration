using Ews.Data.Abstractions;
using Ews.Data.SqlServer;
using Ews.Domain.Analysis;
using Ews.Domain.Circuits;
using Ews.Domain.Masters;
using Ews.Domain.Projects;

namespace Ews.Analysis;

/// <summary>
/// 回路解析処理(本体)。
///
/// 【C原典】
///   - 入口  : toku/qrespo/sekkei/fyskews/src/FyskEwsMain.c (回路解析処理)
///   - 本体  : libfysek.a の Fysk10_Main (回路解析メイン)
///   - 入力  : 物件情報 FYDF801 / 回路内容記述 FYDF805 / 機器マスター FYDM805 ほか
///   - 出力  : 主回路エリア FYRT800 等 → 複合回路ファイル FYDF807
///
/// 旧 C ではこれらを EWS-ISAM のファイルから直接読み込んでいた。本移行では
/// データ取得を SQL Server リポジトリ経由に置き換え、解析ロジックは段階的に移植する。
///
/// 【現状】縦断パイロットとしてデータ取得?結果生成の骨組み(オーケストレーション)を実装。
/// 行種別の詳細解析(機器選定・積算・複合回路展開)は <c>Fysk10_Main</c> の各サブ処理を
/// 順次移植して肉付けする。
/// </summary>
public sealed class CircuitAnalyzer
{
    private readonly SqlCircuitDescriptionRepository _circuitRepository;
    private readonly SqlEquipmentMasterRepository _equipmentRepository;
    private readonly CircuitStringChecker _stringChecker;

    public CircuitAnalyzer(
        SqlCircuitDescriptionRepository circuitRepository,
        SqlEquipmentMasterRepository equipmentRepository,
        CircuitStringChecker stringChecker)
    {
        _circuitRepository = circuitRepository;
        _equipmentRepository = equipmentRepository;
        _stringChecker = stringChecker;
    }

    /// <summary>
    /// 指定の依頼明細番号について回路解析を実行し、主回路結果の一覧を返す。
    /// 【C原典】Fysk10_Main() のメインループ(回路内容記述を行番号順に走査して解析)。
    /// </summary>
    /// <param name="requestNumber">新規登録依頼番号。【C原典】airaino。</param>
    /// <param name="itemNumber">新規登録明細番号。【C原典】ameisano。</param>
    public CircuitAnalysisResult Analyze(string requestNumber, string itemNumber)
    {
        var key = new CircuitLineKey(requestNumber, itemNumber);

        // 【C原典】FyIsamStartR(FYDF805) → FyIsamNextR ループ。
        var lines = _circuitRepository.ReadSequential(key).ToList();

        // 【C原典】FyskEwsMain.c main() が Fysk10_Main を呼ぶ前に実行する
        //         行種別の前処理(コンマ整理・行結合・行種変換 等)を一括適用する。
        CircuitLineNormalizer.Normalize(lines);

        var results = new List<MainCircuitResult>();
        var warnings = new List<string>();

        // 【C原典】Fysk10_Main → Fyss11_Mojiretu_Check。
        //         回路記述を系統/行種/仕様/機器テーブルへ展開し、記述エラーを収集する。
        // 物件情報(bukken1/bukken2)は本パイロットでは未取得のため暫定の空インスタンスを渡す。
        var project = new ProjectInfo { NewRequestNumber = requestNumber, NewItemNumber = itemNumber };
        CircuitParseResult parseResult = _stringChecker.Check(project, project, lines);
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

        return new CircuitAnalysisResult(requestNumber, itemNumber, results, warnings, parseResult);
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
