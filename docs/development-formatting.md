# PowerShell の整形

リポジトリのルートで PowerShell 7 を使います。

```powershell
./tools/format-powershell.ps1
./tools/format-powershell.ps1 -Check
```

PSScriptAnalyzer 1.25.0 を初回実行時に PowerShell Gallery から `.formatter-cache/` に取得します。このディレクトリは Git 管理から除外しています。既定の対象は `verify-published.ps1` と整形コマンド自身です。新しい PowerShell ファイルは `-Paths` にリポジトリ内の相対パスを指定して整形してください。対象は UTF-8（BOM の有無は保持）です。リンクやジャンクションを経由する対象とキャッシュ内のファイルは拒否します。CI の 3 OS の `build` ジョブでも書換えなしの確認を実行します。

C# には .NET SDK 付属の `dotnet format` が使用できます。プロジェクト共通の C# 整形コマンドと、以下に列挙した対象以外の YAML・Markdown の整形は別の増分で整備します。

TypeScript helper では `src/AllCodeChecker.TypeScript` で依存関係をインストール後、`npm run format` で helper・検証スクリプト・package.json・CI とこの手順書を整形し、`npm run format:check` で確認します。Prettier は 3.6.2 に固定しています。対象はコマンドに列挙したファイルだけです。
