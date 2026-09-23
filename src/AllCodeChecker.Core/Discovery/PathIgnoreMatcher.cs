using System.Text;
using System.Text.RegularExpressions;

namespace AllCodeChecker.Discovery;

public sealed class PathIgnoreMatcher
{
    private readonly Regex[] patterns;

    public PathIgnoreMatcher(IEnumerable<string>? patterns)
    {
        this.patterns = (patterns ?? [])
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => new Regex(
                ToRegexPattern(Normalize(pattern)),
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            .ToArray();
    }

    public bool IsMatch(string relativePath)
    {
        string normalizedPath = Normalize(relativePath).Trim('/');

        return patterns.Any(pattern => pattern.IsMatch(normalizedPath));
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim();

    private static string ToRegexPattern(string pattern)
    {
        string normalized = pattern.Trim('/');
        bool matchAnyDepth = !normalized.Contains('/');

        if (normalized.EndsWith("/**", StringComparison.Ordinal))
        {
            string directoryPattern = normalized[..^3];
            return BuildPattern(directoryPattern, matchAnyDepth) + "(?:/.*)?$";
        }

        return BuildPattern(normalized, matchAnyDepth) + "$";
    }

    private static string BuildPattern(string pattern, bool matchAnyDepth)
    {
        var builder = new StringBuilder("^");

        if (matchAnyDepth)
        {
            builder.Append("(?:.*/)?");
        }

        for (int index = 0; index < pattern.Length; index++)
        {
            char current = pattern[index];

            if (current == '*')
            {
                bool doubleStar = index + 1 < pattern.Length && pattern[index + 1] == '*';

                if (doubleStar)
                {
                    index++;

                    bool followedBySlash = index + 1 < pattern.Length && pattern[index + 1] == '/';

                    if (followedBySlash)
                    {
                        index++;
                        builder.Append("(?:.*/)?");
                    }
                    else
                    {
                        builder.Append(".*");
                    }

                    continue;
                }

                builder.Append("[^/]*");
                continue;
            }

            if (current == '?')
            {
                builder.Append("[^/]");
                continue;
            }

            builder.Append(Regex.Escape(current.ToString()));
        }

        return builder.ToString();
    }
}
