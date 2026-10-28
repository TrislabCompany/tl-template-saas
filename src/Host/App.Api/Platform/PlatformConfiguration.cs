using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace App.Api.Platform;

/// <summary>
/// The one place that knows the platform's setting names (13-app-templates.md
/// section 6.1 in tl-cloud-infrastructure). It turns them into the options the
/// building blocks read, so nothing else reads a platform setting. A setting
/// the stamp doesn't set leaves its option as appsettings.json has it, and the
/// building block falls back to what needs no service. Add a mapping here for
/// each setting or secret the product adds. The migrator compiles this file too.
/// </summary>
internal static class PlatformConfiguration
{
    /// <summary>The environment name when PLATFORM_ENVIRONMENT is unset.</summary>
    public const string LocalEnvironment = "local";

    /// <summary>The bucket the stamp's storage service creates, when S3_BUCKET is unset.</summary>
    public const string DefaultBucket = "storage";

    /// <summary>
    /// Adds the options mapped from the platform's settings. Call it before
    /// anything reads the options; configuration added after it, such as a
    /// test host's, still wins.
    /// </summary>
    public static void AddPlatformSettings(this IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = new Dictionary<string, string?>
        {
            ["Platform:Environment"] = Value(configuration, "PLATFORM_ENVIRONMENT") ?? LocalEnvironment,
        };

        if (Value(configuration, "DATABASE_URL") is { } databaseUrl)
        {
            settings["DatabaseOptions:Provider"] = "POSTGRESQL";
            settings["DatabaseOptions:ConnectionString"] = ToNpgsqlConnectionString(databaseUrl);
        }

        if (Value(configuration, "REDIS_URL") is { } redisUrl)
        {
            settings["CachingOptions:Redis"] = ToRedisConfiguration(redisUrl);
        }

        if (Value(configuration, "MESSAGING_URL") is { } messagingUrl)
        {
            AddRabbitMq(settings, messagingUrl);
        }

        if (Value(configuration, "S3_ENDPOINT") is { } s3Endpoint)
        {
            AddS3(settings, configuration, s3Endpoint);
        }

        Map(settings, configuration, "JWT_SIGNING_KEY", "JwtOptions:SigningKey");
        Map(settings, configuration, "HANGFIRE_PASSWORD", "HangfireOptions:Password");
        Map(settings, configuration, "SEED_ADMIN_PASSWORD", "Seed:DefaultAdminPassword");
        Map(settings, configuration, "SEED_DEMO", "Seed:Demo");
        Map(settings, configuration, "SMTP_PASSWORD", "MailOptions:Smtp:Password");

        configuration.AddInMemoryCollection(settings);
    }

    /// <summary>
    /// Turns a URL such as postgresql://user:password@host:5432/db?sslmode=require,
    /// the form the stamp's DATABASE_URL has, into an Npgsql connection string.
    /// A value that is already a connection string is returned as it is.
    /// </summary>
    internal static string ToNpgsqlConnectionString(string databaseUrl)
    {
        if (!TryParseUrl(databaseUrl, ["postgres", "postgresql"], out var uri))
        {
            return databaseUrl;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
        };

        var (user, password) = UserInfo(uri);
        if (user is not null)
        {
            builder.Username = user;
        }
        if (password is not null)
        {
            builder.Password = password;
        }

        foreach (var (key, value) in QueryHelpers.ParseQuery(uri.Query))
        {
            builder[key switch
            {
                "sslmode" => "SSL Mode",
                "channel_binding" => "Channel Binding",
                _ => key,
            }] = value.ToString();
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Turns redis://user:password@host:6379 or rediss://… (TLS), the form the
    /// stamp's REDIS_URL has, into a StackExchange.Redis configuration string.
    /// A value that is already one, such as host:6379, is returned as it is.
    /// </summary>
    internal static string ToRedisConfiguration(string redisUrl)
    {
        if (!TryParseUrl(redisUrl, ["redis", "rediss"], out var uri))
        {
            return redisUrl;
        }

        var parts = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"{uri.Host}:{(uri.Port > 0 ? uri.Port : 6379)}"),
        };

        var (user, password) = UserInfo(uri);
        if (!string.IsNullOrEmpty(user))
        {
            parts.Add($"user={user}");
        }
        if (!string.IsNullOrEmpty(password))
        {
            parts.Add($"password={password}");
        }
        if (uri.Scheme == "rediss")
        {
            parts.Add("ssl=true");
        }

        parts.Add("abortConnect=false");
        return string.Join(',', parts);
    }

    private static void AddRabbitMq(Dictionary<string, string?> settings, string messagingUrl)
    {
        if (!TryParseUrl(messagingUrl, ["amqp", "amqps"], out var uri))
        {
            throw new InvalidOperationException("MESSAGING_URL must be an AMQP URL, such as amqp://<user>:<password>@<host>:5672.");
        }

        var tls = uri.Scheme == "amqps";
        var (user, password) = UserInfo(uri);
        var virtualHost = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        settings["EventingOptions:Provider"] = "RabbitMQ";
        settings["EventingOptions:RabbitMQ:Host"] = uri.Host;
        var defaultPort = tls ? 5671 : 5672;
        settings["EventingOptions:RabbitMQ:Port"] = (uri.Port > 0 ? uri.Port : defaultPort).ToString(CultureInfo.InvariantCulture);
        settings["EventingOptions:RabbitMQ:UseSsl"] = tls ? "true" : "false";
        settings["EventingOptions:RabbitMQ:VirtualHost"] = virtualHost.Length > 0 ? virtualHost : "/";
        if (user is not null)
        {
            settings["EventingOptions:RabbitMQ:UserName"] = user;
        }
        if (password is not null)
        {
            settings["EventingOptions:RabbitMQ:Password"] = password;
        }
    }

    private static void AddS3(Dictionary<string, string?> settings, IConfiguration configuration, string endpoint)
    {
        var bucket = Value(configuration, "S3_BUCKET") ?? DefaultBucket;

        settings["Storage:Provider"] = "s3";
        settings["Storage:S3:ServiceUrl"] = endpoint;
        settings["Storage:S3:Bucket"] = bucket;
        settings["Storage:S3:Region"] = "us-east-1";
        settings["Storage:S3:ForcePathStyle"] = "true";
        settings["Storage:S3:AccessKey"] = Value(configuration, "S3_ACCESS_KEY");
        settings["Storage:S3:SecretKey"] = Value(configuration, "S3_SECRET_KEY");

        // The browser reaches the storage through the front end's /storage proxy, so
        // presigned and public URLs use the page's hostname, not the internal endpoint.
        if (Value(configuration, "S3_PUBLIC_ENDPOINT") is { } publicEndpoint)
        {
            settings["Storage:S3:PublicServiceUrl"] = publicEndpoint;
            settings["Storage:S3:PublicBaseUrl"] = $"{publicEndpoint.TrimEnd('/')}/{bucket}";
        }
    }

    private static void Map(Dictionary<string, string?> settings, IConfiguration configuration, string name, string key)
    {
        if (Value(configuration, name) is { } value)
        {
            settings[key] = value;
        }
    }

    private static string? Value(IConfiguration configuration, string name)
    {
        var value = configuration[name];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool TryParseUrl(string value, string[] schemes, out Uri uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri!) && schemes.Contains(uri.Scheme, StringComparer.Ordinal);
    }

    private static (string? User, string? Password) UserInfo(Uri uri)
    {
        if (uri.UserInfo.Length == 0)
        {
            return (null, null);
        }

        var parts = uri.UserInfo.Split(':', 2);
        return (
            parts[0].Length > 0 ? Uri.UnescapeDataString(parts[0]) : null,
            parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : null);
    }
}
