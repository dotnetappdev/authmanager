using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AuthManager.AspNetCore.Infrastructure;

/// <summary>
/// Sniffs connection strings to determine which database provider is being used,
/// then configures a <see cref="DbContextOptionsBuilder"/> by calling the appropriate
/// Use{Provider}() extension method via reflection.
///
/// Scanning strategy (in order):
///   1. Scan all ConnectionStrings in IConfiguration, pick the first recognisable one.
///   2. If options.DbProvider is set, pick the first string that matches that provider.
///   3. Fall back to the named key (options.ConnectionStringName) if no match is found via scan.
/// </summary>
public static class DbProviderDetector
{
    public enum DetectedProvider { SqlServer, PostgreSQL, MySql, Sqlite, Unknown }

    // Provider assembly names + their EF extension class + extension method name
    private static readonly (string Assembly, string TypeName, string MethodName)[] _providers =
    [
        ("Microsoft.EntityFrameworkCore.SqlServer",
         "Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions",
         "UseSqlServer"),

        ("Npgsql.EntityFrameworkCore.PostgreSQL",
         "Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions",
         "UseNpgsql"),

        ("Pomelo.EntityFrameworkCore.MySql",
         "Microsoft.EntityFrameworkCore.MySqlDbContextOptionsBuilderExtensions",
         "UseMySql"),

        ("Microsoft.EntityFrameworkCore.Sqlite",
         "Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions",
         "UseSqlite"),
    ];

    /// <summary>
    /// Analyses the connection string and returns the detected provider.
    /// Performs no network calls — purely string analysis.
    /// </summary>
    public static DetectedProvider Detect(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return DetectedProvider.Unknown;

        var cs = connectionString;  // keep original case for URI schemes
        var lower = cs.ToLowerInvariant();

        // ---- SQLite ---- (check before SQL Server — "Data Source=*.db" overlaps)
        if (lower.StartsWith("sqlite://") ||
            lower.Contains(":memory:") ||
            (lower.Contains("data source=") &&
             (lower.Contains(".db") || lower.Contains(".sqlite") || lower.Contains(":memory:"))))
            return DetectedProvider.Sqlite;

        // ---- PostgreSQL ----
        if (lower.StartsWith("postgresql://") ||
            lower.StartsWith("postgres://") ||
            (lower.Contains("host=") &&
             (lower.Contains("username=") || lower.Contains("user id=") || lower.Contains("user="))))
            return DetectedProvider.PostgreSQL;

        // ---- MySQL / MariaDB ----
        if (lower.StartsWith("mysql://") ||
            lower.StartsWith("mariadb://") ||
            (lower.Contains("server=") &&
             (lower.Contains("uid=") || lower.Contains("user id=")) &&
             !lower.Contains("initial catalog=") &&    // not SQL Server
             !lower.Contains("integrated security=") &&
             !lower.Contains("(localdb)") &&
             !lower.Contains("tcp:")))
            return DetectedProvider.MySql;

        // ---- SQL Server ---- (most permissive, check last)
        if (lower.StartsWith("server=tcp:") ||
            lower.StartsWith("data source=") ||
            lower.Contains("initial catalog=") ||
            lower.Contains("integrated security=") ||
            lower.Contains("trusted_connection=") ||
            lower.Contains("multipleactiveresultsets=") ||
            lower.Contains("(localdb)") ||
            lower.Contains("server=") && lower.Contains("database="))
            return DetectedProvider.SqlServer;

        return DetectedProvider.Unknown;
    }

    /// <summary>
    /// Scans all connection strings in IConfiguration and returns the first one that matches
    /// the requested provider (or the first recognisable one when provider is Unknown).
    ///
    /// Resolution order:
    ///   1. Iterate ConnectionStrings in config order.
    ///   2. For each value, run Detect().
    ///   3. Return the first entry that satisfies: detected == wantedProvider (or any non-Unknown when wanted == Unknown).
    ///   4. If nothing matched, fall back to the named key (fallbackKey) — useful when the format is
    ///      non-standard but the developer knows which string to use.
    /// </summary>
    /// <param name="config">IConfiguration from the host.</param>
    /// <param name="wantedProvider">Unknown = auto-detect; anything else = match that provider.</param>
    /// <param name="fallbackKey">Fallback connection string name (e.g. "Default").</param>
    /// <returns>Resolved (connectionString, provider) pair.</returns>
    /// <exception cref="InvalidOperationException">No matching connection string found.</exception>
    public static (string ConnectionString, DetectedProvider Provider) Resolve(
        IConfiguration config,
        DetectedProvider wantedProvider = DetectedProvider.Unknown,
        string fallbackKey = "Default")
    {
        var section = config.GetSection("ConnectionStrings");
        var candidates = section.GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToList();

        // Try every connection string value
        foreach (var cs in candidates)
        {
            var detected = Detect(cs);
            if (wantedProvider == DetectedProvider.Unknown && detected != DetectedProvider.Unknown)
                return (cs, detected);
            if (wantedProvider != DetectedProvider.Unknown && detected == wantedProvider)
                return (cs, wantedProvider);
        }

        // Fallback: named key (format may be non-standard but developer knows which one to use)
        var fallback = config.GetConnectionString(fallbackKey);
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            var detected = Detect(fallback!);
            var provider = wantedProvider != DetectedProvider.Unknown ? wantedProvider : detected;
            if (provider != DetectedProvider.Unknown)
                return (fallback!, provider);
        }

        // Build a helpful error showing what was found
        var found = candidates.Count > 0
            ? $"Found {candidates.Count} connection string(s) but none were recognisable: {string.Join(", ", candidates.Select(c => $"\"{Truncate(c, 40)}\""))}"
            : "No connection strings found under ConnectionStrings in configuration.";

        throw new InvalidOperationException(
            $"DotNetAuthManager: could not resolve a connection string. {found} " +
            (wantedProvider != DetectedProvider.Unknown
                ? $"Ensure your {wantedProvider} connection string is present in appsettings.json, or remove options.DbProvider to auto-detect."
                : "Add a ConnectionStrings section to appsettings.json, or set options.DbProvider explicitly."));
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    /// <summary>
    /// Configures the DbContextOptionsBuilder for the given detected provider.
    /// Calls UseSqlServer / UseNpgsql / UseMySql / UseSqlite via reflection so that
    /// the core AuthManager package does not need a direct reference to provider assemblies.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the required provider assembly is not loaded (not installed via NuGet).
    /// The exception message includes the package name to install.
    /// </exception>
    public static void Configure(DbContextOptionsBuilder options, DetectedProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DetectedProvider.SqlServer:
                CallExtensionMethod("Microsoft.EntityFrameworkCore.SqlServer",
                    "Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions",
                    "UseSqlServer", options, connectionString,
                    "Microsoft.EntityFrameworkCore.SqlServer");
                break;

            case DetectedProvider.PostgreSQL:
                CallExtensionMethod("Npgsql.EntityFrameworkCore.PostgreSQL",
                    "Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions",
                    "UseNpgsql", options, connectionString,
                    "Npgsql.EntityFrameworkCore.PostgreSQL");
                break;

            case DetectedProvider.MySql:
                CallMySql(options, connectionString);
                break;

            case DetectedProvider.Sqlite:
                CallExtensionMethod("Microsoft.EntityFrameworkCore.Sqlite",
                    "Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions",
                    "UseSqlite", options, connectionString,
                    "Microsoft.EntityFrameworkCore.Sqlite");
                break;

            default:
                throw new InvalidOperationException(
                    "DotNetAuthManager could not detect the database provider from the connection string. " +
                    "Either set AuthManagerOptions.DbProvider explicitly, or ensure the connection string " +
                    "contains recognisable keywords. " +
                    "Supported formats: SQL Server, PostgreSQL (Npgsql), MySQL/MariaDB (Pomelo), SQLite.");
        }
    }

    /// <summary>
    /// Returns a friendly name and NuGet package name for the detected provider.
    /// </summary>
    public static (string Name, string Package) GetProviderInfo(DetectedProvider provider) => provider switch
    {
        DetectedProvider.SqlServer  => ("SQL Server",  "Microsoft.EntityFrameworkCore.SqlServer"),
        DetectedProvider.PostgreSQL => ("PostgreSQL",  "Npgsql.EntityFrameworkCore.PostgreSQL"),
        DetectedProvider.MySql      => ("MySQL",       "Pomelo.EntityFrameworkCore.MySql"),
        DetectedProvider.Sqlite     => ("SQLite",      "Microsoft.EntityFrameworkCore.Sqlite"),
        _                           => ("Unknown",     "")
    };

    // ---- Private helpers ----

    private static void CallExtensionMethod(
        string assemblyName,
        string typeName,
        string methodName,
        DbContextOptionsBuilder options,
        string connectionString,
        string nugetPackage)
    {
        var type = FindType(assemblyName, typeName)
            ?? throw new InvalidOperationException(
                $"DotNetAuthManager auto-detected '{assemblyName}' as the database provider " +
                $"but the assembly is not referenced. " +
                $"Install it: dotnet add package {nugetPackage}");

        // Find the method with signature (DbContextOptionsBuilder, string, <optional action>)
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
                m.Name == methodName &&
                m.GetParameters() is var p &&
                p.Length >= 2 &&
                p[0].ParameterType.IsAssignableFrom(typeof(DbContextOptionsBuilder)) &&
                p[1].ParameterType == typeof(string))
            ?? throw new InvalidOperationException(
                $"Could not locate {typeName}.{methodName}(DbContextOptionsBuilder, string) in assembly {assemblyName}. " +
                "The installed version may be incompatible.");

        // Build args: first is opts, second is connection string, rest are null (optional delegates)
        var args = method.GetParameters()
            .Select((p, i) => i == 0 ? (object)options : i == 1 ? connectionString : (object?)null)
            .ToArray();

        method.Invoke(null, args);
    }

    private static void CallMySql(DbContextOptionsBuilder options, string connectionString)
    {
        const string assemblyName = "Pomelo.EntityFrameworkCore.MySql";
        const string extensionsType = "Microsoft.EntityFrameworkCore.MySqlDbContextOptionsBuilderExtensions";
        const string serverVersionType = "Microsoft.EntityFrameworkCore.ServerVersion";
        const string nugetPackage = "Pomelo.EntityFrameworkCore.MySql";

        var extType = FindType(assemblyName, extensionsType)
            ?? throw new InvalidOperationException(
                $"DotNetAuthManager auto-detected MySQL as the database provider but '{assemblyName}' is not referenced. " +
                $"Install it: dotnet add package {nugetPackage}");

        var svType = FindType(assemblyName, serverVersionType)
            ?? throw new InvalidOperationException($"Could not locate {serverVersionType} in {assemblyName}.");

        // ServerVersion.AutoDetect(string) — lazily probes the server on first use
        var autoDetectMethod = svType.GetMethod("AutoDetect",
            BindingFlags.Public | BindingFlags.Static, null, [typeof(string)], null)
            ?? throw new InvalidOperationException($"Could not locate {serverVersionType}.AutoDetect(string).");

        var serverVersion = autoDetectMethod.Invoke(null, [connectionString])!;

        // UseMySql(DbContextOptionsBuilder, string, ServerVersion, Action<MySqlDbContextOptionsBuilder>? = null)
        var method = extType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
                m.Name == "UseMySql" &&
                m.GetParameters() is var p &&
                p.Length >= 3 &&
                p[0].ParameterType.IsAssignableFrom(typeof(DbContextOptionsBuilder)) &&
                p[1].ParameterType == typeof(string))
            ?? throw new InvalidOperationException(
                $"Could not locate UseMySql(DbContextOptionsBuilder, string, ServerVersion) in {assemblyName}.");

        var args = method.GetParameters()
            .Select((p, i) => i switch
            {
                0 => (object)options,
                1 => connectionString,
                2 => serverVersion,
                _ => (object?)null
            })
            .ToArray();

        method.Invoke(null, args);
    }

    private static Type? FindType(string assemblySimpleName, string typeName)
    {
        // 1. Check already-loaded assemblies first (no disk I/O)
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblySimpleName);
        if (loaded != null)
            return loaded.GetType(typeName);

        // 2. Try loading by name (works when the assembly is in the probing path)
        try
        {
            var assembly = Assembly.Load(new AssemblyName(assemblySimpleName));
            return assembly.GetType(typeName);
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException)
        {
            return null;
        }
    }
}
