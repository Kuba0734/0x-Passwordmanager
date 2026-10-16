namespace PasswordVault.Core.Services;

/// <summary>
/// Ordnet gespeicherte URLs einer besuchten Seite zu (für die Browser-Erweiterung).
/// </summary>
public static class UrlMatcher
{
    // Zweistufige öffentliche Suffixe, bei denen die Basisdomain drei Labels hat
    private static readonly HashSet<string> SecondLevelSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.uk", "org.uk", "ac.uk", "gov.uk", "com.au", "net.au", "org.au",
        "co.at", "or.at", "co.nz", "co.jp", "com.br", "com.tr", "co.za", "com.mx"
    };

    /// <summary>
    /// Liefert den Hostnamen ohne "www." oder null, wenn die URL unbrauchbar ist.
    /// Akzeptiert auch Eingaben ohne Schema ("github.com/login").
    /// </summary>
    public static string? GetHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var candidate = url.Trim();
        if (!candidate.Contains("://"))
            candidate = "https://" + candidate;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Host))
            return null;
        if (uri.Scheme is not ("http" or "https"))
            return null;

        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.") ? host[4..] : host;
    }

    public static string? GetBaseDomain(string? host)
    {
        if (string.IsNullOrEmpty(host)) return null;
        if (System.Net.IPAddress.TryParse(host, out _) || !host.Contains('.'))
            return host;

        var labels = host.Split('.');
        if (labels.Length <= 2) return host;

        var lastTwo = string.Join('.', labels[^2..]);
        return SecondLevelSuffixes.Contains(lastTwo)
            ? string.Join('.', labels[^3..])
            : lastTwo;
    }

    /// <summary>
    /// 0 = kein Treffer, 1 = gleiche Basisdomain, 2 = Subdomain-Beziehung, 3 = exakter Host.
    /// </summary>
    public static int MatchScore(string? entryUrl, string? pageUrl)
    {
        var entryHost = GetHost(entryUrl);
        var pageHost = GetHost(pageUrl);
        if (entryHost == null || pageHost == null) return 0;

        if (entryHost == pageHost) return 3;

        // Alles Weitere nur innerhalb derselben registrierbaren Domain
        var entryBase = GetBaseDomain(entryHost);
        if (entryBase == null || entryBase != GetBaseDomain(pageHost)) return 0;

        return pageHost.EndsWith("." + entryHost) || entryHost.EndsWith("." + pageHost) ? 2 : 1;
    }
}
