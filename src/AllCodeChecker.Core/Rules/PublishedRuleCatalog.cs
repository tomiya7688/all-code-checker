namespace AllCodeChecker.Rules;

public static class PublishedRuleCatalog
{
    private static readonly RuleDefinition[] PublishedRules =
    [
        Rule("ACI001", "解析失敗"),
        Rule("ACI002", "構文解析不能"),
        Rule("ACI003", "未解決シンボル"),
        Rule("ACI004", "明らかな型不整合"),
        Rule("ACI005", "不正なメンバー参照"),
        Rule("ACI006", "不正な呼び出しシグネチャ"),
        Rule("ACI007", "必須定義ファイル不足"),
        Rule("ACI008", "明示されていないnull"),
        Rule("ACI009", "暗黙の危険な型変換"),
        Rule("ACI010", "狭い型への危険な代入"),
        Rule("ACI101", "無限ループ"),
        Rule("ACI102", "終了不能再帰"),
        Rule("ACI103", "危険な循環参照"),
        Rule("ACI104", "同期デッドロック"),
        Rule("ACI105", "非同期デッドロック"),
        Rule("ACI106", "危険な参照寿命"),
        Rule("ACI107", "リソース解放漏れ"),
        Rule("ACI108", "非決定的な結果"),
        Rule("ACI109", "リソース枯渇"),
        Rule("ACI110", "TOCTOU"),
        Rule("ACI201", "長すぎる関数"),
        Rule("ACI202", "深すぎるネスト"),
        Rule("ACI203", "循環的複雑度が高い"),
        Rule("ACI204", "重複コード"),
        Rule("ACI205", "未使用コード"),
        Rule("ACI206", "無意味なコード"),
        Rule("ACI207", "同じ意味のクラスまたはファイル"),
        Rule("ACI208", "同名シンボル解決の紛らわしさ"),
        Rule("ACI209", "コメントがまったくない"),
        Rule("ACI210", "誤解を招く名前"),
        Rule("ACI211", "言語標準と衝突する命名"),
        Rule("ACI212", "ハードコード値の重複"),
        Rule("ACI213", "無駄に長い処理経路"),
        Rule("ACI214", "長すぎる1行"),
        Rule("ACI215", "引数過多"),
        Rule("ACI216", "同型引数の取り違えリスク"),
        Rule("ACI217", "シャドーイング"),
        Rule("ACI218", "不完全なエラーハンドリング"),
        Rule("ACI219", "意味を失ったエラー処理"),
        Rule("ACI301", "単体テスト失敗"),
        Rule("ACI302", "テスト未検出"),
        Rule("ACI303", "静的シミュレーション失敗可能性"),
        Rule("ACI304", "カバレッジ不足"),
        Rule("ACI401", "JSON構文またはSchema不整合"),
        Rule("ACI402", "INI構文または定義不整合"),
        Rule("ACI403", "XML破損"),
        Rule("ACI404", "XAML破損"),
        Rule("ACI405", "HTML破損"),
        Rule("ACI406", "プロジェクトまたはビルド定義不整合"),
        Rule("ACI501", "危険な外部プロセス呼び出し"),
        Rule("ACI502", "Secretの危険な扱い"),
        Rule("ACI503", "入力汚染"),
        Rule("ACI504", "パストラバーサル"),
        Rule("ACI505", "危険なHTML出力"),
        Rule("ACI506", "危険なデシリアライズ"),
        Rule("ACI507", "認証または認可の明らかな欠落"),
        Rule("ACI508", "弱い暗号またはTLS設定"),
        Rule("ACI509", "不確定な外部情報の過信"),
        Rule("ACI510", "危険なポインタまたは参照ハック"),
        Rule("ACI601", "受け取り側でのデータ欠損"),
        Rule("ACI602", "型未指定の受け渡し"),
        Rule("ACI603", "危険な文字コード変換"),
        Rule("ACI604", "暗黙の型変更"),
        Rule("ACI605", "ランダム性不足"),
        Rule("ACI606", "非ランダム用途の結果揺れ"),
        Rule("ACI701", "依存関係解決失敗"),
        Rule("ACI702", "packageまたはlockfile不整合"),
        Rule("ACI703", "既知の重大脆弱性を持つ依存"),
        Rule("ACI704", "外部ツールへの過剰依存"),
        Rule("ACI901", "checker内部エラー"),
        Rule("ACI902", "analyzerプロセス異常終了"),
        Rule("ACI903", "部分解析")
    ];

    public static IReadOnlyList<RuleDefinition> All => PublishedRules;

    public static RuleRegistry CreateRegistry()
    {
        var registry = new RuleRegistry();

        foreach (RuleDefinition rule in PublishedRules)
        {
            registry.Register(rule);
        }

        return registry;
    }

    private static RuleDefinition Rule(string id, string name) =>
        new(AciRuleId.Parse(id), name);
}
