# EwsMigration

EWS（電気設計支援システム）の C 言語資産を C# / .NET へ移行するプロジェクトです。
ISAM・固定長ファイル・`.cns` マスタといった旧データストアを SQL Server へ、
C の処理ロジックを .NET のクラスライブラリ／バッチアプリへ段階的に置き換えていきます。

---

## 移行の基本方針

- **C 資産の意味（挙動）は保持する。** 変数名・関数名・ソースファイル名は分かりづらいため、
  コメントから意図を汲み取り**現代的な英語命名**へ置き換えます。
- **旧名との対応を必ず残す。** 生成した C# コードには `【C原典】` コメントで元の C 名を記載し、
  加えて [docs/name-mapping.csv](docs/name-mapping.csv) に対応表を**追記専用**で蓄積します。
  （列: `種別,元C名,新C#名,出典ファイル,意味`）
- **既存の `CSharp/` ディレクトリは使用しない。** 本 `EwsMigration/` 配下に新規に構築します。
- **C 原典は `toku/sekkei/src` を正とする**（最新版）。旧 `qre_sekkei` 版は参照しません。

ポインタ／`\0`（NUL終端）・固定長文字列の扱いといった移植上の詳細方針は
[docs/migration-policy-pointers-and-strings.md](docs/migration-policy-pointers-and-strings.md) を参照してください。

---

## ソリューション構成

標準的な .NET レイヤードアーキテクチャです。ライブラリ・実行形式を問わず本体コードは `src/` に置き、
テストは `tests/` に分離します。

```
EwsMigration/
├── Ews.Migration.sln
├── .gitignore
├── README.md                ← 本ファイル
├── docs/                     ← 移行方針・名称対応表
│   ├── migration-policy-pointers-and-strings.md
│   └── name-mapping.csv      ← C ? C# 名称対応表（追記専用）
├── sql/                      ← SQL Server スキーマ／シード
│   └── 001_schema.sql
├── src/
│   ├── Ews.Domain/           ← ドメイン層（エンティティ・値オブジェクト）。依存なし
│   ├── Ews.Data/             ← データアクセス層（SQL Server / ISAM 抽象 / .cns 取込）→ Domain
│   ├── Ews.Analysis/         ← 回路解析ロジック（sekkei 系の移植先）→ Domain, Data
│   └── Ews.App.Batch/        ← 実行形式（バッチ入口・DI ホスト）→ Domain, Data, Analysis
└── tests/
    └── Ews.Tests/            ← 単体テスト（xUnit）→ Domain, Data, Analysis
```

### プロジェクトの役割と依存方向

| プロジェクト | 種別 | 役割 | 依存先 |
|---|---|---|---|
| `Ews.Domain` | ライブラリ | ドメインモデル（`EquipmentTableEntry`=KIKITABLE 等の作業テーブル、マスタ、値オブジェクト） | なし（最下層） |
| `Ews.Data` | ライブラリ | SQL Server リポジトリ（Dapper）、`.inf` 構成読込、`.cns` マスタ取込、ISAM 抽象 | Domain |
| `Ews.Analysis` | ライブラリ | 回路文字列チェック・主回路生成など解析ロジック（`toku/sekkei` の移植先） | Domain, Data |
| `Ews.App.Batch` | 実行形式（Exe） | バッチ入口。`Microsoft.Extensions.Hosting` による DI ホストとジョブ実行 | Domain, Data, Analysis |
| `Ews.Tests` | テスト | xUnit による単体テスト | Domain, Data, Analysis |

> ライブラリと実行形式は `OutputType` の違いのみで、いずれも `src/` 配下に置きます。
> 今後 `seigyo` / `sakuzu` 等のモジュールを移植する際は、C の 1 ディレクトリ＝1 プロジェクトと機械的に写すのではなく、
> **ドメイン境界（同じ理由で一緒に変わるか）でプロジェクト粒度を決める**方針です。

---

## 開発環境

- **.NET 9 (`net9.0`)** / C#。全プロジェクトで `ImplicitUsings` と `Nullable` を有効化。
- 主要パッケージ:
  - データ: `Dapper`, `Microsoft.Data.SqlClient`, `System.Text.Encoding.CodePages`
  - ホスティング: `Microsoft.Extensions.Hosting` / `Configuration` / `DependencyInjection`
  - テスト: `xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`

---

## ビルドとテスト

リポジトリルート（`EwsMigration/`）で実行します。

```powershell
# 復元＆ビルド
dotnet build Ews.Migration.sln

# テスト
dotnet test Ews.Migration.sln
```

---

## 文字エンコーディング（重要）

- **ソースコード（`.cs` / `.csv` / `.sql`）は Shift-JIS / CP932（BOM なし）** で保存します。
  旧システムの固定長・`.cns` データが CP932 前提であり、突合や固定長エンコードの互換性を保つためです。
  実行時の CP932 取り扱いには `System.Text.Encoding.CodePages` を使用します。
- **本 `README.md` など GitHub 上で表示するドキュメントは UTF-8** とします
  （GitHub は UTF-8 を前提にレンダリングするため）。
- ファイル編集後は文字化け（U+FFFD）が混入していないか検証してからコミットします。

```powershell
# CP932 健全性チェック（0 であるべき）
$enc = [System.Text.Encoding]::GetEncoding(932)
$t = [System.IO.File]::ReadAllText((Resolve-Path "src\Ews.Analysis\CircuitStringChecker.cs").Path, $enc)
[regex]::Matches($t, [char]0xFFFD).Count
```

> `docs/name-mapping.csv` は**追記専用**で扱い、既存全文を CP932 で読み込み → 末尾に行を追加 → CP932 で書き戻します。
> LF 改行を維持し、先頭行が `種別,元C名,新C#名,出典ファイル,意味` であること、
> 別ファイル内容の混入がないことを編集後に必ず確認します。

---

## リポジトリ

- GitHub: https://github.com/UchidaMasanori/EwsMigration

---

## 移行の進捗（概要）

回路解析（`toku/sekkei` 系）を先行して移植しています。主な移植済み範囲:

- 回路文字列チェック `CircuitStringChecker`（`Fyss11.c` / `Fyss1c.c` / `Fyss1d.c`）
- 電気パラメータ検証と定格値（key_tbl）格納 `ElectricalParameterChecker`
- 主回路生成 `MainCircuitBuilder`（`Fyss12.c`）の各ステップ（系統／行種／機器情報／回路区分／
  電気パラメータ一致チェック、機器 No スケーリングと D_No 昇順ソート 等）

各対応関係の詳細は [docs/name-mapping.csv](docs/name-mapping.csv) を参照してください。
