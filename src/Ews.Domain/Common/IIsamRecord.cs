namespace Ews.Domain.Common;

/// <summary>
/// レガシー ISAM レコードに対応するドメインモデルが実装するマーカーインターフェース。
///
/// 【背景】
/// C 資産では各データファイルが固定長バイト列(struct FYDFxxx / FYDMxxx)で表現され、
/// EWS-ISAM ライブラリ(cmnisam.h)経由でアクセスされていた。本移行では各 struct を
/// C# クラスへ起こし、SQL Server のテーブル 1 行へマッピングする。
///
/// 各実装は元レコードのバイト長(<see cref="RecordLength"/>)と、固定長バイト列との
/// 相互変換を提供することで、移行期間中はファイル/DB どちらからの読込でも同じ
/// ドメインモデルを再利用できる。
/// </summary>
public interface IIsamRecord
{
    /// <summary>C 構造体のレコード長(バイト)。【C原典】各ヘッダの「ﾚｺｰﾄﾞ長」コメント。</summary>
    static abstract int RecordLength { get; }
}
