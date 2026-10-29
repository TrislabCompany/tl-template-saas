using App.Api.Platform;
using Microsoft.Extensions.Configuration;

namespace Integration.Tests.Tests.Health;

/// <summary>The mapping from the platform's settings to the building blocks' options. Needs no container.</summary>
public sealed class PlatformConfigurationTests
{
    private static ConfigurationManager Map(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(settings);
        configuration.AddPlatformSettings();
        return configuration;
    }

    [Fact]
    public void Unset_Settings_Leave_The_Fallbacks()
    {
        var configuration = Map([]);

        configuration["Platform:Environment"].ShouldBe("local");
        configuration["DatabaseOptions:ConnectionString"].ShouldBeNull();
        configuration["CachingOptions:Redis"].ShouldBeNull();
        configuration["EventingOptions:Provider"].ShouldBeNull();
        configuration["Storage:Provider"].ShouldBeNull();
        configuration["HangfireOptions:Password"].ShouldBeNull();
    }

    [Fact]
    public void DatabaseUrl_Becomes_An_Npgsql_ConnectionString()
    {
        var configuration = Map(new() { ["DATABASE_URL"] = "postgresql://app:p%40ss@db.example:6543/app?sslmode=require" });

        var connectionString = configuration["DatabaseOptions:ConnectionString"]!;
        connectionString.ShouldContain("Host=db.example");
        connectionString.ShouldContain("Port=6543");
        connectionString.ShouldContain("Database=app");
        connectionString.ShouldContain("Username=app");
        connectionString.ShouldContain("Password=p@ss");
        connectionString.ShouldContain("SSL Mode=Require");
    }

    [Fact]
    public void RedisUrl_Becomes_A_Redis_Configuration()
    {
        var configuration = Map(new() { ["REDIS_URL"] = "rediss://default:secret@cache.example:6380" });

        configuration["CachingOptions:Redis"].ShouldBe("cache.example:6380,user=default,password=secret,ssl=true,abortConnect=false");
    }

    [Fact]
    public void MessagingUrl_Switches_To_RabbitMq()
    {
        var configuration = Map(new() { ["MESSAGING_URL"] = "amqp://app:secret@mq.example:5672/" });

        configuration["EventingOptions:Provider"].ShouldBe("RabbitMQ");
        configuration["EventingOptions:RabbitMQ:Host"].ShouldBe("mq.example");
        configuration["EventingOptions:RabbitMQ:UserName"].ShouldBe("app");
        configuration["EventingOptions:RabbitMQ:VirtualHost"].ShouldBe("/");
    }

    [Fact]
    public void S3_Settings_Switch_To_S3_With_Public_Urls()
    {
        var configuration = Map(new()
        {
            ["S3_ENDPOINT"] = "http://ca-shr-con-dev-storage:9000",
            ["S3_PUBLIC_ENDPOINT"] = "https://dev.example.com",
            ["S3_ACCESS_KEY"] = "key",
            ["S3_SECRET_KEY"] = "secret",
        });

        configuration["Storage:Provider"].ShouldBe("s3");
        configuration["Storage:S3:ServiceUrl"].ShouldBe("http://ca-shr-con-dev-storage:9000");
        configuration["Storage:S3:Bucket"].ShouldBe("storage");
        configuration["Storage:S3:PublicServiceUrl"].ShouldBe("https://dev.example.com");
        configuration["Storage:S3:PublicBaseUrl"].ShouldBe("https://dev.example.com/storage");
    }

    [Fact]
    public void Secrets_Map_Onto_Their_Options()
    {
        var configuration = Map(new()
        {
            ["JWT_SIGNING_KEY"] = "jwt",
            ["HANGFIRE_PASSWORD"] = "hangfire",
            ["SEED_ADMIN_PASSWORD"] = "admin",
            ["SMTP_PASSWORD"] = "smtp",
        });

        configuration["JwtOptions:SigningKey"].ShouldBe("jwt");
        configuration["HangfireOptions:Password"].ShouldBe("hangfire");
        configuration["Seed:DefaultAdminPassword"].ShouldBe("admin");
        configuration["MailOptions:Smtp:Password"].ShouldBe("smtp");
    }
}
