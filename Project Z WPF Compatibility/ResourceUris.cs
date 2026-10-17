namespace ProjectZ.WpfCompatibility;

/// <summary>Resolve WPF resource identifiers through native build deployment.</summary>
internal static class ResourceUris
{
    static readonly object registrationLock = new();
    internal static void RegisterPackScheme()
    {
        lock (registrationLock)
        {
            if (!UriParser.IsKnownScheme("pack"))
                UriParser.Register(new GenericUriParser(GenericUriParserOptions.GenericAuthority), "pack", -1);
        }
    }

    internal static string FilePath(Uri uri)
    {
        var path = uri.OriginalString;
        if (uri.IsAbsoluteUri)
        {
            if (uri.IsFile) return uri.LocalPath;
            if (uri.Scheme != "pack" || uri.Host is not ("application:,,," or "siteoforigin:,,,"))
                throw new NotSupportedException($"Native image resource URI is not supported: {uri}");
            path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
            var component = path.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
            if (component >= 0) path = path[(component + ";component/".Length)..];
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}
