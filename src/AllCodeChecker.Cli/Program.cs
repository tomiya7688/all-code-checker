namespace AllCodeChecker.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("使用方法: all-code-checker <プロジェクトフォルダ|プロジェクトファイル|エントリーポイント>");
            return 2;
        }

        string input = Path.GetFullPath(args[0]);

        if (!Directory.Exists(input) && !File.Exists(input))
        {
            Console.Error.WriteLine($"ACI001 危険 解析対象が見つかりません: {input}");
            Console.Error.WriteLine("全体結果: 危険");
            return 2;
        }

        Console.WriteLine($"解析対象: {input}");
        Console.WriteLine("Bootstrap: 解析ルールは順次実装されます。");
        Console.WriteLine("全体結果: 正常");
        return 0;
    }
}
