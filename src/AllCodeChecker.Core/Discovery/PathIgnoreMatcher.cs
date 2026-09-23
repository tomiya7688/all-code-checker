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
        string normalizedPath = Normalize(relativePath).TrimStart('/');

        return patterns.Any(pattern => pattern.IsMatch(normalizedPath));
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim();

    private static string ToRegexPattern(string pattern)
    {
        string normalized = pattern.TrimStart('/');
        var builder = new StringBuilder("^");

        if (!normalized.Contains('/'))
        {
            builder.Append("(?:.*/)?");
        }

        for (int index = 0; index < normalized.Length; index++)
        {
            char current = normalized[index];

            if (current == '*')
            {
                bool doubleStar = index + 1 < normalized.Length && normalized[index + 1] == '*';

                if (doubleStar)
                {
                    index++;

                    bool followedBySlash = index + 1 < normalized.Length && normalized[index + 1] == '/';

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

        if (normalized.EndsWith("/**", StringComparison.Ordinal))
        {
            builder.Append("?");
        }

        builder.Append("$");
        return builder.ToString();
    }
}
