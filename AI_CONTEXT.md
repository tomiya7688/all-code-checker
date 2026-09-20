# AI Context

> all-code-checker 開発で AI が最初に読む小さい索引です。詳細仕様をここへ複製せず、必要な原典へ routing します。

## Project

- Purpose: 設計判断そのものではなく、機械的に検証可能なコード品質・実行リスク・CI項目を多言語で統一チェックする。
- Main implementation: C# / .NET。
- v1.0.0 target languages: TypeScript / C# / Python / Go。
- Primary source root: `src/`。
- Default diagnostic language: 日本語。
- Severity: 危険 / 警告 / 注意。

## Source of Truth

- 方針・ルール仕様: `docs/project-direction.md`
- 実装: `src/`
- CI: `.github/workflows/ci.yml`
- Current task: GitHub Issue / PR とユーザー決定

## Read First

1. current Issue / PR / user decision
2. この `AI_CONTEXT.md`
3. `docs/project-direction.md` の対象ルール周辺
4. 対象 source + matching tests
5. direct dependency only when needed

全ルール・全言語実装を最初から読まない。

## Development Rules

- Search first, read second。
- Goal / Required / Acceptance が揃ったら探索を止める。
- unrelated refactor を混ぜない。
- source / tests / specification を要約より優先する。
- `ai-context-reducer` は開発支援であり runtime/build dependency にしない。
- 参照プロジェクトの構造を丸ごとコピーせず、有効な原則だけ一般化して取り込む。

## Reference Projects

- `tomiya7688/upd-commander-base-design`: design checkerとしてCIへ導入済み。project適合のため既定thresholdを使用
- `tomiya7688/oop-design-checker`: OOP design checkerとしてCIへ導入済み。CI failure thresholdは`danger`
- `tomiya7688/ai-context-reducer`: search-first / targeted validation / context reduction

## Ignore Normally

- `bin/`, `obj/`, build artifacts, caches
- 成功したCIログ全文
- unrelated Issues / PR history
- generated/vendored code（対象ルールが必要な場合を除く）

## Self-check

all-code-checker 自身も自身の基準から免除しない。

現在のbootstrap段階では以下を必須gateとする。

1. Release build
2. .NET analyzers + warnings as errors
3. UPD Commander Base Design check（既定threshold）
4. OOP Design Checker `--fail-on danger`
5. CLI self-check entry point against `src/`

ルール実装が利用可能になったものから、同じself-checkへ順次追加し、最終的には all-code-checker 自身に危険・警告・注意の許容方針を適用する。

## Validation

- C# core変更: targeted build + relevant tests + self-quality-gate
- rule変更: matching rule tests + self-check
- CI変更: workflow syntax / build path / required gateを確認
- docs only: link/path整合を確認し、不要なfull buildは避ける
