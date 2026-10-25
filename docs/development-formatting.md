# PowerShell の整形

リポジトリのルートで PowerShell 7 を使います。

```powershell
./tools/format-powershell.ps1
./tools/format-powershell.ps1 -Check
```

PSScriptAnalyzer 1.25.0 を初回実行時に PowerShell Gallery から `.formatter-cache/` に取得します。このディレクトリは Git 管理から除外しています。既定の対象は `verify-published.ps1` と整形コマンド自身です。別の PowerShell ファイルを整形するときは `-Paths` にリポジトリ内の相対パスを指定します。CI の 3 OS の `build` ジョブでも書換えなしの確認を実行します。

C# は .NET SDK 付属の `dotnet format` を使用します。YAML と Markdown の formatter は別の増分で整備します。
