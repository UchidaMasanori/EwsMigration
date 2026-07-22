namespace Ews.Domain.Masters;

/// <summary>
/// 工場(部署)別仕様書一覧マスター。
///
/// 【C原典】
///   - テキストマスタ: siyosyo.cns (toku/const/interf/siyosyo.cns, Shift-JIS)
///   - パーサ        : Zs20SiyoInfoRead (toku/interf/zs50/src/Fymzs40Cns.c) の
///                     struct SIYO_INFO / struct SYUBETU_INFO。
///   - 形式(階層)    :
///       部署:&lt;部署コード&gt;
///         仕様書:&lt;種別名&gt;
///           仕様書説明:&lt;説明&gt;
///           仕様書パス:&lt;パス&gt;
///           仕様書ファイル:&lt;ファイル名&gt;   (0..N)
///         END仕様書
///       END部署
///
/// C 原典は環境変数 ZONECD に一致する部署のみを保持するが、マスタ取込用途では
/// 全部署を読み込む(参照側でフィルタする)。図面サイズ(no/scale/zmnsyu/kenzu)は
/// SiyosyoSizeCheck が実図面ファイルを参照して求めるため本モデルには含めない。
/// </summary>
/// <param name="DepartmentCode">部署コード。【C原典】SIYO_INFO.bumon(「部署:」の値)。</param>
/// <param name="Kinds">仕様書種別一覧。【C原典】SIYO_INFO.s_info[]。</param>
public sealed record SpecificationInfo(
    string DepartmentCode,
    IReadOnlyList<SpecificationKind> Kinds);

/// <summary>
/// 仕様書種別(1 部署内の 1 仕様書エントリ)。
/// 【C原典】struct SYUBETU_INFO (Fymzs40Cns.c)。
/// </summary>
/// <param name="Name">種別名。【C原典】kaninm(「仕様書:」の値)。</param>
/// <param name="Description">説明。【C原典】explain(「仕様書説明:」の値)。</param>
/// <param name="Path">仕様書パス。【C原典】path(「仕様書パス:」の値)。</param>
/// <param name="Files">仕様書ファイル名一覧。【C原典】file_name[](「仕様書ファイル:」の値)。</param>
public sealed record SpecificationKind(
    string Name,
    string Description,
    string Path,
    IReadOnlyList<string> Files);
