using Xunit;

namespace Ews.Tests;

/// <summary>
/// ゴールデン突合(回路解析の C 版出力 vs C# 版出力)ハーネス。
///
/// 【C原典】toku/qrespo/sekkei/fyskews/src/FyskEwsMain.c の出力
///          (複合回路ファイル FYDF807 / 主回路エリア FYRT800 等)。
///
/// 検証基準データ(AIX 実機での fyskews 出力サンプル)は後日入手予定のため、
/// 現時点では Skip とし、ハーネスの枠組みのみ用意する。データ入手後は
/// <c>Skip</c> を外し、baselineDir に C 版出力、actual に C# 版出力を与えて突合する。
/// </summary>
public sealed class GoldenComparisonHarnessTests
{
    private const string PendingReason =
        "検証基準データ(AIX fyskews 出力)未入手のため保留。入手後に Skip を解除する。";

    [Fact(Skip = PendingReason)]
    public void 回路解析_C版出力と一致する()
    {
        // TODO(データ入手後):
        //  1. baselineDir から C 版の FYDF807/FYRT* 出力を読み込む
        //  2. 同一入力で CircuitAnalyzer.Analyze を実行
        //  3. フィールド単位で突合(許容差は仕様で定義)
        Assert.True(true);
    }
}
