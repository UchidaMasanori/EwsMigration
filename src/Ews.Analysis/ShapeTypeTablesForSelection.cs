using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 変換形状タイプ表(選択番号方式)。
/// 【C原典】type_t_xxx / type_tbl (fyrt819.h:185-)。
///
/// <c>Fysk01_Keijyoutype_Check</c>(=<see cref="ShapeTypeSelector.BuildConvertedShapeTypes"/>)と
/// <c>Fysk01_HandleRock_Check</c>(=<see cref="ShapeTypeSelector.CheckHandleLock"/>)が参照する。
///
/// 選択番号(seleno)の意味合(Type_T のビットフラグ):
///   0x01 製作仕様(河村標準以外) / 0x02 封印区分 / 0x04 盤種別 /
///   0x08 行種(分岐) / 0x10 回路3相。
/// </summary>
public static class ShapeTypeTablesForSelection
{
    private static SelectionShapeVariant V(int selectionNumber, params string[] types)
        => new(selectionNumber, types);

    /// <summary>
    /// 選択番号の並び順(MCB/ELB 系で共通)。
    /// 【C原典】type_t_xxx の行順(0/4/2/16/20/18/8/12/10/24/28/26/1/5/3/17/21/19/9/13/11/25/29/27)。
    /// </summary>
    private static readonly int[] SelectionOrder =
        [0, 4, 2, 16, 20, 18, 8, 12, 10, 24, 28, 26, 1, 5, 3, 17, 21, 19, 9, 13, 11, 25, 29, 27];

    /// <summary>全選択番号で同一の設定タイプを持つテーブルを生成する(SB/RMCB/RELB/RMMCB/RELMB)。</summary>
    private static IReadOnlyList<SelectionShapeVariant> Uniform(params string[] types)
        => [.. SelectionOrder.Select(s => new SelectionShapeVariant(s, types))];

    // ---- MCB (選択番号ごとに su/typ が異なる) 【C原典】type_t_mcb ----
    private static readonly IReadOnlyList<SelectionShapeVariant> Mcb =
    [
        V(0, "KY", "ET", "ST"), V(4, "KT", "ET", "ST"), V(2, "ET", "ST"),
        V(16, "KT", "ET", "ST"), V(20, "KT", "ET", "ST"), V(18, "ET", "ST"),
        V(8, "KM", "KY", "ET", "ST"), V(12, "KM", "ET", "ST"), V(10, "ET", "ST"),
        V(24, "KM", "ET", "ST"), V(28, "KM", "ET", "ST"), V(26, "ET", "ST"),
        V(1, "ET", "ST"), V(5, "ET", "ST"), V(3, "ET", "ST"),
        V(17, "ET", "ST"), V(21, "ET", "ST"), V(19, "ET", "ST"),
        V(9, "KM", "KY", "ET", "ST"), V(13, "KM", "ET", "ST"), V(11, "ET", "ST"),
        V(25, "ET", "ST"), V(29, "ET", "ST"), V(27, "ET", "ST"),
    ];

    // ---- ELB 【C原典】type_t_elb(改訂<4>有効版, 末尾に seleno=99 ZS/ZB) ----
    private static readonly IReadOnlyList<SelectionShapeVariant> Elb =
    [
        V(0, "ET", "ST"), V(4, "ET", "ST"), V(2, "ET", "ST"),
        V(16, "ET", "ST"), V(20, "ET", "ST"), V(18, "ET", "ST"),
        V(8, "KM", "ET", "ST"), V(12, "KM", "ET", "ST"), V(10, "ET", "ST"),
        V(24, "KM", "ET", "ST"), V(28, "KM", "ET", "ST"), V(26, "ET", "ST"),
        V(1, "ET", "ST"), V(5, "ET", "ST"), V(3, "ET", "ST"),
        V(17, "ET", "ST"), V(21, "ET", "ST"), V(19, "ET", "ST"),
        V(9, "KM", "ET", "ST"), V(13, "KM", "ET", "ST"), V(11, "ET", "ST"),
        V(25, "ET", "ST"), V(29, "ET", "ST"), V(27, "ET", "ST"),
        V(99, "ZS", "ZB"),
    ];

    // ---- MMCB 【C原典】type_t_mmcb ----
    private static readonly IReadOnlyList<SelectionShapeVariant> Mmcb =
    [
        V(0, "KM", "ET", "ST"), V(4, "KM", "ET", "ST"), V(2, "ET", "ST"),
        V(16, "KM", "ET", "ST"), V(20, "KM", "ET", "ST"), V(18, "ET", "ST"),
        V(8, "KM", "ET", "ST"), V(12, "KM", "ET", "ST"), V(10, "ET", "ST"),
        V(24, "KM", "ET", "ST"), V(28, "KM", "ET", "ST"), V(26, "ET", "ST"),
        V(1, "ET", "ST"), V(5, "ET", "ST"), V(3, "ET", "ST"),
        V(17, "ET", "ST"), V(21, "ET", "ST"), V(19, "ET", "ST"),
        V(9, "ET", "ST"), V(13, "ET", "ST"), V(11, "ET", "ST"),
        V(25, "ET", "ST"), V(29, "ET", "ST"), V(27, "ET", "ST"),
    ];

    // ---- ELMB 【C原典】type_t_elmb(MMCB と同一) ----
    private static readonly IReadOnlyList<SelectionShapeVariant> Elmb = Mmcb;

    // ---- MC 【C原典】type_t_mc ----
    private static readonly IReadOnlyList<SelectionShapeVariant> Mc =
    [
        V(0, "SF", "SK"), V(4, "SF", "SK"), V(2, "SF", "SK"),
        V(16, "SK"), V(20, "SK"), V(18, "SK"),
        V(8, "SF", "SK"), V(12, "SF", "SK"), V(10, "SF", "SK"),
        V(24, "SK"), V(28, "SK"), V(26, "SK"),
        V(1, "SF", "SK"), V(5, "SF", "SK"), V(3, "SF", "SK"),
        V(17, "SK"), V(21, "SK"), V(19, "SK"),
        V(9, "SF", "SK"), V(13, "SF", "SK"), V(11, "SF", "SK"),
        V(25, "SK"), V(29, "SK"), V(27, "SK"),
    ];

    // ---- 位置指定/特殊(選択番号非使用: 位置 n で参照) ----
    private static readonly IReadOnlyList<SelectionShapeVariant> Pbs = [V(0, "NOTHING")];            // type_t_pbs
    private static readonly IReadOnlyList<SelectionShapeVariant> Wh = [V(0, "NOTHING", "KE")];       // type_t_wh
    private static readonly IReadOnlyList<SelectionShapeVariant> Tr = [V(0, "RT"), V(1, "UT", "RT")]; // type_t_tr
    private static readonly IReadOnlyList<SelectionShapeVariant> Cr = [V(0, "PR", "MY", "LY", "MC")]; // type_t_cr
    private static readonly IReadOnlyList<SelectionShapeVariant> Ee = [V(0, "UM", "RO", "CP")];       // type_t_ee(改訂<5>)

    /// <summary>
    /// 予約語→変換形状タイプ表。【C原典】type_tbl (fyrt819.h)。
    /// ReservedWord は前方一致に使うため末尾空白を含む(strlen 相当長)。
    /// </summary>
    public static IReadOnlyList<SelectionShapeTableEntry> ConversionTable { get; } =
    [
        new("MCB ", 0, 5, Mcb),
        new("ELB ", 0, 5, Elb),
        new("MMCB ", 0, -1, Mmcb),
        new("ELMB ", 0, -1, Elmb),
        new("SB ", 0, 1, Uniform("SES", "SE", "SS", "ET")),
        new("RMCB ", 0, -1, Uniform("BR")),
        new("RELB ", 0, -1, Uniform("KR")),
        new("RMMCB ", 0, -1, Uniform("DR")),
        new("RELMB ", 0, -1, Uniform("YR")),
        new("MC ", 0, -1, Mc),
        new("PBS ", 6, -1, Pbs),
        new("TR ", 0, -1, Tr),
        new("WH ", 3, -1, Wh),
        new("CR ", 0, -1, Cr),
        new("EE ", 0, -1, Ee),
    ];
}
