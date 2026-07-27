using Ews.Domain.Analysis;

namespace Ews.Analysis;

/// <summary>
/// 予約語(機器種別)別の定格値編集用情報テーブル。【C原典】<c>tt_xxx[]</c>(toku/include/sekkei/fyrt817.h)。
///
/// <see cref="RatingKeyBuilder.MakeRatingKey"/> の入力となる静的データ。C 原典の配列リテラルを
/// フィールド並び <c>{len, d_len, kouno, check, fromto, kakunou, c_toku, s_toku}</c> のまま忠実に転記する。
/// 本移植では遮断器・電磁接触器系(MCB?NT)を先行整備し、計器・変成器系(WH/VM/... )は今後追加する。
/// </summary>
public static class RatingKeyTables
{
    // 【C原典】fyrt808.h の比較種別: E=1 GE=2 LE=3。
    private const short Eq = 1;
    private const short Ge = 2;
    private const short Le = 3;

    /// <summary>テーブル終端行。【C原典】{ -1,-1,-1,-1,-1,-1,-1,-1 }。</summary>
    private static readonly RatingKeyTableEntry End = new(-1, -1, -1, -1, -1, -1, -1, -1);

    private static RatingKeyTableEntry Row(
        short width, short decimalScale, short itemNo, short comparison,
        short rangeSide, short storageKind, short columnFlag, short selectFlag)
        => new(width, decimalScale, itemNo, comparison, rangeSide, storageKind, columnFlag, selectFlag);

    /// <summary>MCB(配線用遮断器)。【C原典】tt_mcb(fyrt817.h:14)。at+af+p+e+v。</summary>
    public static readonly RatingKeyTableEntry[] Mcb =
    {
        Row(4, 0, 9, Ge, 0, 0, 0, 0),   // at
        Row(4, 0, 8, Ge, 0, 0, 0, 0),   // af
        Row(1, 0, 6, Ge, 0, 0, 0, 0),   // p
        Row(1, 0, 7, Ge, 0, 0, 0, 0),   // e
        Row(3, 0, 23, Ge, 0, 0, 0, 0),  // v
        End,
    };

    /// <summary>ELB(漏電遮断器)。【C原典】tt_elb(fyrt817.h:23)。ma+at+af+p+e、電圧は範囲照合(-3)。</summary>
    public static readonly RatingKeyTableEntry[] Elb =
    {
        Row(3, 0, 16, Eq, 0, 0, 0, 0),   // ma
        Row(4, 0, 9, Ge, 0, 0, 0, 0),    // at
        Row(4, 0, 8, Ge, 0, 0, 0, 0),    // af
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(3, 0, 23, Le, 0, 0, 0, -3),  // vg
        Row(3, 0, 23, Ge, 0, 0, 0, -3),  // vl
        End,
    };

    /// <summary>MMCB(モータ用遮断器)。【C原典】tt_mmcb(fyrt817.h:34)。at(d_len=2)+af+p+e+v。</summary>
    public static readonly RatingKeyTableEntry[] Mmcb =
    {
        Row(5, 2, 9, Ge, 0, 0, 0, 0),    // at
        Row(3, 0, 8, Ge, 0, 0, 0, 0),    // af
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(3, 0, 23, Ge, 0, 0, 0, 0),   // v
        End,
    };

    /// <summary>ELMB(モータ用漏電遮断器)。【C原典】tt_elmb(fyrt817.h:43)。ma+at(d_len=2)+af+p+e、電圧は範囲照合(-3)。</summary>
    public static readonly RatingKeyTableEntry[] Elmb =
    {
        Row(3, 0, 16, Eq, 0, 0, 0, 0),   // ma
        Row(5, 2, 9, Ge, 0, 0, 0, 0),    // at
        Row(3, 0, 8, Ge, 0, 0, 0, 0),    // af
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(3, 0, 23, Le, 0, 0, 0, -3),  // vg
        Row(3, 0, 23, Ge, 0, 0, 0, -3),  // vl
        End,
    };

    /// <summary>SB(サーキットブレーカ)。【C原典】tt_sb(fyrt817.h:54)。at+e+af+p+v。</summary>
    public static readonly RatingKeyTableEntry[] Sb =
    {
        Row(2, 0, 9, Ge, 0, 0, 0, 0),    // at
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(2, 0, 8, Ge, 0, 0, 0, 0),    // af
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(3, 0, 23, Ge, 0, 0, 0, 0),   // v
        End,
    };

    /// <summary>RMCB(リモコン配線用遮断器)。【C原典】tt_rmcb(fyrt817.h:63)。at+p、制御電圧は範囲照合(-3)。</summary>
    public static readonly RatingKeyTableEntry[] Rmcb =
    {
        Row(2, 0, 9, Ge, 0, 0, 0, 0),    // at
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(3, 0, 29, Le, 1, 0, 0, -3),  // vcl
        Row(3, 0, 29, Ge, 2, 0, 0, -3),  // vcg
        Row(2, 0, 8, Ge, 0, 0, 0, 0),    // af
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(3, 0, 23, Ge, 0, 0, 0, 0),   // v
        End,
    };

    /// <summary>RELB(リモコン漏電遮断器)。【C原典】tt_relb(fyrt817.h:73)。ma+at+p、制御電圧/電圧は範囲照合(-3)。</summary>
    public static readonly RatingKeyTableEntry[] Relb =
    {
        Row(3, 0, 16, Eq, 0, 0, 0, 0),   // ma
        Row(2, 0, 9, Ge, 0, 0, 0, 0),    // at
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(3, 0, 29, Le, 1, 0, 0, -3),  // vcl
        Row(3, 0, 29, Ge, 2, 0, 0, -3),  // vcg
        Row(2, 0, 8, Ge, 0, 0, 0, 0),    // af
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(3, 0, 23, Le, 0, 0, 0, -3),  // vg
        Row(3, 0, 23, Ge, 0, 0, 0, -3),  // vl
        End,
    };

    /// <summary>RMMCB(リモコンモータ用遮断器)。【C原典】tt_rmmcb(fyrt817.h:85)。at(d_len=2)+p、制御電圧は範囲照合(-3)。</summary>
    public static readonly RatingKeyTableEntry[] Rmmcb =
    {
        Row(4, 2, 9, Ge, 0, 0, 0, 0),    // at
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(3, 0, 29, Le, 1, 0, 0, -3),  // vcl
        Row(3, 0, 29, Ge, 2, 0, 0, -3),  // vcg
        Row(2, 0, 8, Ge, 0, 0, 0, 0),    // af
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(3, 0, 23, Ge, 0, 0, 0, 0),   // v
        End,
    };

    /// <summary>RELMB(リモコンモータ用漏電遮断器)。【C原典】tt_relmb(fyrt817.h:96)。ma+at(d_len=2)+p、制御電圧/電圧は範囲照合(-3)。</summary>
    public static readonly RatingKeyTableEntry[] Relmb =
    {
        Row(3, 0, 16, Eq, 0, 0, 0, 0),   // ma
        Row(4, 2, 9, Ge, 0, 0, 0, 0),    // at
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(3, 0, 29, Le, 1, 0, 0, -3),  // vcl
        Row(3, 0, 29, Ge, 2, 0, 0, -3),  // vcg
        Row(2, 0, 8, Ge, 0, 0, 0, 0),    // af
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(3, 0, 23, Le, 0, 0, 0, -3),  // vg
        Row(3, 0, 23, Ge, 0, 0, 0, -3),  // vl
        End,
    };

    /// <summary>MC(電磁接触器)。【C原典】tt_mc(fyrt817.h:108)。a、制御電圧は範囲照合(-3)。</summary>
    public static readonly RatingKeyTableEntry[] Mc =
    {
        Row(3, 0, 11, Ge, 0, 0, 0, 0),   // a
        Row(3, 0, 29, Le, 1, 0, 0, -3),  // vcl
        Row(3, 0, 29, Ge, 2, 0, 0, -3),  // vcg
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(3, 0, 23, Ge, 0, 4, 0, 0),   // v
        Row(1, 0, 34, Ge, 0, 0, 0, 0),   // ac
        Row(1, 0, 35, Ge, 0, 0, 0, 0),   // bc
        End,
    };

    /// <summary>THR(サーマルリレー)。【C原典】tt_thr(fyrt817.h:121)。先頭スキップ(-2)後すぐ範囲照合(-3)→固定キーは空。</summary>
    public static readonly RatingKeyTableEntry[] Thr =
    {
        Row(0, 0, 9, 0, 0, 3, 0, -2),    // atg (skip)
        Row(5, 2, 9, Le, 0, 1, 0, -3),   // atg
        Row(5, 2, 9, Ge, 0, 2, 0, -3),   // atl
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(3, 0, 23, Ge, 0, 0, 0, 0),   // vac
        End,
    };

    /// <summary>MG(電磁開閉器)。【C原典】tt_mg(fyrt817.h:130)。a、制御電圧は範囲照合(-3)で打切り。</summary>
    public static readonly RatingKeyTableEntry[] Mg =
    {
        Row(3, 0, 11, Ge, 0, 0, 0, 0),   // a
        Row(3, 0, 29, Le, 1, 0, 0, -3),  // vcl
        Row(3, 0, 29, Ge, 2, 0, 0, -3),  // vcg
        Row(1, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(0, 0, 9, 0, 0, 3, 0, -2),    // check you (skip)
        Row(5, 2, 9, Le, 0, 1, 0, -3),   // atg
        Row(5, 2, 9, Ge, 0, 2, 0, -3),   // atl
        Row(1, 0, 7, Ge, 0, 0, 0, 0),    // e
        Row(1, 0, 34, Ge, 0, 0, 0, 0),   // ac
        Row(1, 0, 35, Ge, 0, 0, 0, 0),   // bc
        Row(3, 0, 23, Ge, 0, 4, 0, 0),   // v
        End,
    };

    /// <summary>SC(進相コンデンサ)。【C原典】tt_sc(fyrt817.h:145)。p+hz+v+kvar(d_len=2)+uf(d_len=1)。</summary>
    public static readonly RatingKeyTableEntry[] Sc =
    {
        Row(1, 0, 2, Eq, 0, 0, 0, 0),    // p
        Row(2, 0, 5, Eq, 0, 0, 0, 0),    // hz
        Row(3, 0, 23, Ge, 0, 0, 0, 0),   // v
        Row(5, 2, 14, Ge, 0, 0, 0, 0),   // kvar
        Row(5, 1, 15, Le, 0, 0, 0, 0),   // uf
        End,
    };

    /// <summary>NT(中性線切替開閉器)。【C原典】tt_nt(fyrt817.h:154)。a2+p+v。</summary>
    public static readonly RatingKeyTableEntry[] Nt =
    {
        Row(2, 0, 11, Ge, 0, 0, 0, 0),   // a2
        Row(3, 0, 6, Ge, 0, 0, 0, 0),    // p
        Row(3, 0, 23, Ge, 0, 0, 0, 0),   // v
        End,
    };
}
