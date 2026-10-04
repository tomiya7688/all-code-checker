# all-code-checker

複数言語のソースコードをまとめて解析し、**コードから機械的に高い確度で判断できる問題**を共通の診断形式で報告するチェッカーです。

設計の良し悪しを主観的に評価するのではなく、構文・型・実行時リスク・ライフタイム・並行処理・設定不整合・脆弱性・テスト異常など、静的解析や既存言語ツールから根拠を得られる問題を対象にします。

> 現在は v1.0.0 に向けた実装初期段階です。

## v1.0.0 対応予定言語

- TypeScript
- C#
- Python
- Go

v1.0.0 後に追加予定:

- Rust
- C++
- Luau
- Ruby
- Bitlang（言語・Compilerが十分成熟した後）

1 repository = 1 language とは仮定しません。1つのrepository内に複数のprojectや言語が存在する場合、それぞれを検出・解析して診断を統合します。

## 何を検出するか

v1.0.0 では主に次の領域を対象にします。

- 構文エラー、未解決symbol、明白な型不整合
- null / None / nil / undefined の危険なflow
- narrowing / implicit conversion / dynamic type change
- resource leak、use-after-dispose、scope escape、危険なreference lifetime
- 無限loop、終了不能再帰、deadlock、async misuse
- errorの握りつぶし、戻り値無視、不完全なerror handling
- 外部process実行、TOCTOU、外部情報の過信
- SQL injection、command injection、path traversal、XSS等の高確度な脆弱性
- integer overflow / underflow
- catastrophic regex、resource exhaustion
- JSON / INI / XML / XAML / HTML の基本的不整合
- dependency / package / build metadata の異常
- test failure、test discovery異常、optional coverage
- 長すぎる関数、深すぎるnest、duplicate code、unused code等のreview向けdiagnostic

詳細なrule方針は [docs/project-direction.md](docs/project-direction.md) を正本とします。

## 診断レベル

all-code-checker は3段階のseverityを使います。

- **危険**: 実際の失敗・不正操作・重大な安全性問題であることを強く示せるもの
- **警告**: bugや誤動作につながる可能性が高いもの
- **注意**: reviewする価値があるもの。比較的積極的に出す

例:

```text
ACI001 危険 解析対象を正しく解析できませんでした
ACI104 危険 deadlockする待機cycleが検出されました
ACI214 注意 この行は非常に長く、編集・reviewが困難になる可能性があります
```

rule ID は `ACIxxx` 形式です。同じ系統のruleは近い番号にまとめます。

## 入力

解析rootとして次を扱います。

- project / repository folder
- project / build definition
  - 例: `.sln`, `.csproj`
- 単一のentry source file

単一fileを指定した場合も、そのfileだけを孤立して解析するのではなく、import / include / module / project metadata 等から必要な最小scopeを構築します。

## CUI

予定している基本形:

```bash
all-code-checker <target> [options]
```

主要option:

```text
--config <path>
--no-danger
--no-warning
--no-notice
--coverage
--fail-on <danger|warning|notice|never>
--format <text|json|sarif|github>
--output <path>
--version
--help
```

全severityはdefaultで有効です。

defaultのCI failure thresholdは `danger` です。all-code-checker自身のstrict self-checkでは `--fail-on notice` を使用します。

### Exit code

```text
0  解析成功。failure threshold以上のdiagnosticなし
1  解析成功。failure threshold以上のdiagnosticあり
2  CLI / config / unsupported input等の入力契約エラー
3  checker/analyzer failure またはpartial analysis
```

## 設定

標準設定ファイル:

```text
all-code-checker.json
```

targetから親directory方向へ探索し、最も近い設定を使用します。

明示指定:

```bash
all-code-checker src --config ./all-code-checker.json
```

明示指定した設定が存在しない、または不正な場合はdefaultへ黙ってfallbackせず失敗します。

## GUI

v1.0.0 では Avalonia GUI を予定しています。

CUIとGUIは別のcheckerを持たず、同じChecker Coreを共有します。

GUIでは少なくとも次を扱います。

- folder / project file / entry file選択
- 危険 / 警告 / 注意 filter
- optional coverage
- 実行 / cancel
- grouped diagnostic一覧
- file / lineへのnavigation

## 配布方針

v1.0.0 は **.NET 10** をtargetにします。

利用者へ開発toolchainやruntimeの事前installを要求しないことを重要な要件とします。

> release archiveをdownload・展開し、そのままsource analysisを実行できること。

予定:

- all-code-checker本体: .NET 10 self-contained
- TypeScript: Compiler API helper + 必要runtime/componentsを同梱
- Python: analyzer + portable runtime/componentsを同梱
- Go: analyzer helperをnative executableとして事前buildして同梱

source analysis本体は、利用者のPATH上にある Node.js / Python / Go / .NET SDK に依存しない設計にします。

対象project自身のtestを実行する機能については、そのproject固有runtimeが必要になる場合があります。その場合でもsource analysis本体まで利用不能にはしません。

## 現在のsource構成

詳細構成は実装に合わせて増やしますが、基本的に `src/` 配下へ置きます。

```text
src/
├─ AllCodeChecker.Core/
└─ AllCodeChecker.Cli/
```

今後、責務が明確になった段階でGUIやlanguage analyzer adapterを追加します。

## 開発・CI

all-code-checker自身もcheckerの適用対象から除外しません。

現在のCIでは少なくとも次をgateにします。

- Windows / Linux / macOSでのRelease build
- .NET Analyzer
- warnings as errors
- UPD Commander Base Design checker
- OOP Design Checker
- all-code-checker自身のself-check

設計checker:

- [upd-commander-base-design](https://github.com/tomiya7688/upd-commander-base-design)
- [oop-design-checker](https://github.com/tomiya7688/oop-design-checker)

両方ともstrictなCI gateとして利用します。

最終的には:

```text
build all-code-checker
        ↓
all-code-checkerで自身のsrc/を解析
        ↓
許容されない自己違反
        ↓
CI failure
```

という状態にします。

## AI支援開発

開発時のcontext肥大化を避けるため、[ai-context-reducer](https://github.com/tomiya7688/ai-context-reducer) の考え方を使います。

repository rootの [AI_CONTEXT.md](AI_CONTEXT.md) を小さいrouting indexとして使い、必要なsource / tests / rule specificationだけを読む方針です。

`ai-context-reducer` はruntime/build dependencyではありません。

## 実装管理

実装はGitHub Issues単位で進めます。

大まかな順序:

```text
core contract / input discovery / config
  ↓
analyzer orchestration
  ↓
C# / Python / TypeScript / Go backend
  ↓
共通rule群
  ↓
test / coverage
  ↓
GUI
  ↓
strict self-check
```

同じAST・semantic・data-flow・lifetime・concurrency基盤を使うruleは、必要に応じて同じ実装単位へまとめます。

## 対象外

all-code-checkerは、人間の文脈判断が必要な次のような問題を自動で断定しません。

- architectureが良いか
- domain modelが正しいか
- class/module分割が理想的か
- database設計がbusinessに適切か
- product/API設計が良いか
- DDD aggregateが正しいか

測定可能な複雑度や責務過多の兆候を `注意` として出すことはありますが、それ自体を `危険` にはしません。

## Status

現在はbootstrap実装段階です。

仕様・rule・実装順は [docs/project-direction.md](docs/project-direction.md) とGitHub Issuesで管理しています。
