using Dapper;
using Ews.Domain.Common;
using Ews.Domain.Masters;

namespace Ews.Data.Seeding;

/// <summary>
/// .cns テキストマスタ(Shift-JIS)を読み込み、SQL Server の正規化テーブルへシードする。
///
/// 【C原典】
///   - 部門マスタ: bumon.*.cns
///   - 解析方式 : fopen("rt") + fgets + strchr によるカンマ分割
///     (例 toku/usr/.../InstructionsCns.c, toku/master/fydm830/src/Fydm830Info.c)
///
/// 本パイロットでは部門(bumon)マスタを代表例として実装する。営業所(eigyo*)・
/// 電力管内(denkan)等も同様のパターンで追加可能。
/// </summary>
public sealed class CnsMasterLoader
{
    private readonly SqlServer.SqlConnectionFactory _factory;

    public CnsMasterLoader(SqlServer.SqlConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// bumon.*.cns をパースして <see cref="DepartmentMaster"/> 一覧を返す。
    /// 【C原典】bumon.cns(コード, 名称, 電話番号,)。
    /// </summary>
    public static IReadOnlyList<DepartmentMaster> ParseDepartmentMaster(string cnsPath)
    {
        var list = new List<DepartmentMaster>();
        foreach (string rawLine in File.ReadLines(cnsPath, FixedFieldCodec.ShiftJis))
        {
            string line = rawLine.TrimEnd('\r', '\n');
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            string[] fields = line.Split(',');
            if (fields.Length < 2)
            {
                continue;
            }

            list.Add(new DepartmentMaster(
                DepartmentCode: fields[0].Trim(),
                DepartmentName: fields[1].Trim(' ', '\u3000'),       // 全角空白パディング除去
                PhoneNumber: fields.Length > 2 ? fields[2].Trim() : string.Empty));
        }

        return list;
    }

    /// <summary>
    /// 部門マスタを SQL Server の DepartmentMaster テーブルへ投入する(全置換)。
    /// </summary>
    /// <returns>投入件数。</returns>
    public int SeedDepartmentMaster(string cnsPath)
    {
        IReadOnlyList<DepartmentMaster> rows = ParseDepartmentMaster(cnsPath);

        using var connection = _factory.CreateOpen();
        using var transaction = connection.BeginTransaction();

        connection.Execute("DELETE FROM DepartmentMaster", transaction: transaction);
        connection.Execute(
            """
            INSERT INTO DepartmentMaster (DepartmentCode, DepartmentName, PhoneNumber)
            VALUES (@DepartmentCode, @DepartmentName, @PhoneNumber)
            """,
            rows,
            transaction: transaction);

        transaction.Commit();
        return rows.Count;
    }

    // ---- 部署別仕様書一覧マスタ (siyosyo.cns) ------------------------------

    private const string IdEndDepartment = "END部署";
    private const string IdDepartment = "部署:";
    private const string IdEndSpecification = "END仕様書";
    private const string IdSpecification = "仕様書:";
    private const string IdSpecificationDescription = "仕様書説明:";
    private const string IdSpecificationPath = "仕様書パス:";
    private const string IdSpecificationFile = "仕様書ファイル:";

    /// <summary>
    /// siyosyo.cns をパースして部署別の <see cref="SpecificationInfo"/> 一覧を返す。
    ///
    /// 【C原典】Zs20SiyoInfoRead (toku/interf/zs50/src/Fymzs40Cns.c) の
    /// while(fgets) 状態機械を忠実に移植する。C 原典は ZONECD 一致部署のみを
    /// 保持するが、マスタ取込用途では全部署を読み込む(参照側でフィルタ)。
    /// 図面サイズ(no/scale/zmnsyu/kenzu = SiyosyoSizeCheck 依存)は取り込まない。
    /// </summary>
    public static IReadOnlyList<SpecificationInfo> ParseSpecificationMaster(string cnsPath)
    {
        var result = new List<SpecificationInfo>();

        // 現在処理中の部署ブロック(【C原典】struct SIYO_INFO siyosyo)。
        string? currentDepartment = null;
        List<SpecificationKind>? currentKinds = null;

        // 現在処理中の仕様書種別(【C原典】s_info[s_info_cnt])の可変フィールド。
        string? kindName = null;
        string kindDescription = string.Empty;
        string kindPath = string.Empty;
        List<string>? kindFiles = null;

        bool inDepartment = false;  // 【C原典】sw_blk
        bool inKind = false;        // 【C原典】sw_syu

        // 直近の仕様書種別を result へ確定する。
        void FlushKind()
        {
            if (kindName is not null && currentKinds is not null)
            {
                currentKinds.Add(new SpecificationKind(
                    kindName, kindDescription, kindPath, kindFiles ?? []));
            }

            kindName = null;
            kindDescription = string.Empty;
            kindPath = string.Empty;
            kindFiles = null;
        }

        // 現在の部署ブロックを result へ確定する。
        void FlushDepartment()
        {
            FlushKind();
            if (currentDepartment is not null && currentKinds is not null)
            {
                result.Add(new SpecificationInfo(currentDepartment, currentKinds));
            }

            currentDepartment = null;
            currentKinds = null;
        }

        foreach (string rawLine in File.ReadLines(cnsPath, FixedFieldCodec.ShiftJis))
        {
            string buf = rawLine.TrimEnd('\r', '\n');

            // 【C原典】if( buf[0] == '#' ) continue;
            if (buf.Length > 0 && buf[0] == '#')
            {
                continue;
            }

            // 【C原典】strstr(buf, ID_END="END部署") → sw_blk=OFF。
            if (buf.Contains(IdEndDepartment, StringComparison.Ordinal))
            {
                FlushDepartment();
                inDepartment = false;
                continue;
            }

            // 【C原典】strchr(buf, ':') == NULL → continue。
            // これにより「END仕様書」(コロン無し)は以降の判定へ到達しない。
            if (!buf.Contains(':'))
            {
                continue;
            }

            // 【C原典】strstr(buf, ID_BUMON="部署:")。
            int at = buf.IndexOf(IdDepartment, StringComparison.Ordinal);
            if (at >= 0)
            {
                // 【C原典】sw_blk==ON のときは END 無しで次部署 → 前ブロックを閉じる。
                if (inDepartment)
                {
                    FlushDepartment();
                    inDepartment = false;
                    continue;
                }

                currentDepartment = StrSPCut(buf[(at + IdDepartment.Length)..]);
                currentKinds = [];
                inDepartment = true;
                continue;
            }

            // 【C原典】if( sw_blk != ON ) continue;
            if (!inDepartment)
            {
                continue;
            }

            // 【C原典】strstr(buf, ID_ENDSIYO="END仕様書") → sw_syu=OFF。
            if (buf.Contains(IdEndSpecification, StringComparison.Ordinal))
            {
                FlushKind();
                inKind = false;
                continue;
            }

            // 【C原典】strstr(buf, ID_SIYO="仕様書:") → 新しい種別。
            at = buf.IndexOf(IdSpecification, StringComparison.Ordinal);
            if (at >= 0)
            {
                FlushKind();
                inKind = true;
                kindName = StrSPCut(buf[(at + IdSpecification.Length)..]);
                kindFiles = [];
                continue;
            }

            // 【C原典】else if( sw_syu != ON ) continue;
            if (!inKind)
            {
                continue;
            }

            // 【C原典】strstr(buf, ID_SIYOSPEC="仕様書説明:")。
            at = buf.IndexOf(IdSpecificationDescription, StringComparison.Ordinal);
            if (at >= 0)
            {
                kindDescription = StrSPCut(buf[(at + IdSpecificationDescription.Length)..]);
                continue;
            }

            // 【C原典】strstr(buf, ID_SIYOPATH="仕様書パス:")。
            at = buf.IndexOf(IdSpecificationPath, StringComparison.Ordinal);
            if (at >= 0)
            {
                kindPath = StrSPCut(buf[(at + IdSpecificationPath.Length)..]);
                continue;
            }

            // 【C原典】strstr(buf, ID_SIYOFILE="仕様書ファイル:")。図面サイズ解決は
            // 取り込まないためファイル名のみを常に追加する。
            at = buf.IndexOf(IdSpecificationFile, StringComparison.Ordinal);
            if (at >= 0)
            {
                (kindFiles ??= []).Add(StrSPCut(buf[(at + IdSpecificationFile.Length)..]));
            }
        }

        // 【C原典】ファイル終端。END 無しで終わる末尾ブロックも確定する。
        FlushDepartment();

        return result;
    }

    /// <summary>
    /// 【C原典】StrSPCut。前後の半角空白・タブ・改行を除去する。
    /// </summary>
    private static string StrSPCut(string value) => value.Trim(' ', '\t', '\r', '\n');
}

