namespace Ews.Data.Abstractions;

/// <summary>
/// ISAM 1テーブル(=旧 1データファイル)に対するアクセスを表す移行互換インターフェース。
///
/// 【C原典】cmnisam.h の FyIsam 関数群。レガシーコードのアクセスパターン
/// (キー読込・順次走査・追加・更新)を温存しつつ、実体は SQL Server で実装する。
///
/// <list type="table">
///   <item><term>FyIsamRead</term><description><see cref="Read"/></description></item>
///   <item><term>FyIsamStartR / FyIsamNextR / FyIsamEndR</term><description><see cref="ReadSequential"/></description></item>
///   <item><term>FyIsamAdd</term><description><see cref="Add"/></description></item>
///   <item><term>FyIsamRewrite</term><description><see cref="Rewrite"/></description></item>
///   <item><term>FyIsamDelete</term><description><see cref="Delete"/></description></item>
/// </list>
/// </summary>
/// <typeparam name="TRecord">対応するドメインレコード型。</typeparam>
/// <typeparam name="TKey">主キー型。</typeparam>
public interface IIsamTable<TRecord, in TKey>
{
    /// <summary>
    /// 主キー一致でレコードを1件読み込む。
    /// 【C原典】FyIsamRead(P_CHAR, P_CHAR, SHORT[])。
    /// </summary>
    /// <returns>取得結果。見つからない場合は <see cref="IsamStatus.NotFound"/>。</returns>
    (IsamStatus Status, TRecord? Record) Read(TKey key, LockMode lockMode = LockMode.Unlock);

    /// <summary>
    /// 指定キー前方一致で順次読込を行い、ヒットしたレコードを列挙する。
    /// 【C原典】FyIsamStartR → FyIsamNextR(...) → FyIsamEndR のループ。
    /// </summary>
    IEnumerable<TRecord> ReadSequential(TKey partialKey);

    /// <summary>
    /// レコードを追加する。【C原典】FyIsamAdd(P_CHAR, P_CHAR)。
    /// </summary>
    IsamStatus Add(TRecord record);

    /// <summary>
    /// レコードを更新する。【C原典】FyIsamRewrite(P_CHAR, P_CHAR)。
    /// </summary>
    IsamStatus Rewrite(TRecord record);

    /// <summary>
    /// レコードを削除する。【C原典】FyIsamDelete(P_CHAR, P_CHAR, SHORT[])。
    /// </summary>
    IsamStatus Delete(TKey key);
}
