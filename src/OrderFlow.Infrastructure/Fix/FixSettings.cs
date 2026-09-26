using Microsoft.Extensions.Configuration;
using QuickFix;

namespace OrderFlow.Infrastructure.Fix;

public static class FixSettings
{
    public static SessionSettings Load(IConfiguration configuration)
    {
        var path = configuration["Fix:Config"] ?? throw new InvalidOperationException("Configure Fix__Config.");

        if (!Path.IsPathRooted(path) && !File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, path);

        var text = File.ReadAllText(path)
            .Replace("${FIX_HOST}", configuration["Fix:Host"] ?? "localhost")
            .Replace("${FIX_PORT}", configuration["Fix:Port"] ?? "5001")
            .Replace("${FIX_STORE}", configuration["Fix:Store"] ?? "store")
            .Replace("${FIX_DICTIONARY}", Path.Combine(AppContext.BaseDirectory, "config", "FIX44.xml"));

        return new SessionSettings(new StringReader(text));
    }
}
