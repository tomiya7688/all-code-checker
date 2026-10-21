# TypeScript Analyzer の境界

Issue #8 の TypeScript backend は、TypeScript Compiler API を同梱 helper 内で呼び出す。C# の CLI は source を実行せず、helper と JSON で通信する。

## 入力と解析

- `tsconfig.json` がある scope は、その compiler options と対象 source の交差を解析する。単一 source 入力では選択ファイルだけを root とし、同じ tsconfig の options を適用する。
- discovery が分けた入れ子の `tsconfig.json` 境界を越えて、別 project の source を結果へ含めない。import の型解決には Compiler API の標準 module resolution を使う。
- 構文診断は `ACI002`、意味診断と設定診断は `ACI001` として返す。解析できた宣言の型、symbol、`any`/`unknown`、null/undefined、構造的な property 名、`package.json` のテスト script 名を analysis data に残す。これらの metadata 自体には独自の危険判定を加えない。
- helper の欠落、異常終了、応答破損、通信失敗は `ACI902` になる。構文や設定の一部に問題があっても取得できた結果は保持し、scope を partial とする。

## 配布

CI は固定版 TypeScript を helper directory にインストールし、CLI build/publish 時に実行中の Node runtime、helper、Compiler API を `typescript-runtime/` へ同梱する。利用者側の `PATH` や Node/TypeScript インストールは参照しない。配布用 package は対象 OS 上で作る。

開発環境で CLI を build/publish する前に `src/AllCodeChecker.TypeScript` で `npm install --no-package-lock --ignore-scripts --no-audit --no-fund` を実行する。依存物がない build は失敗する。Node なしで core だけを build する場合は packaging target を呼ばない。
