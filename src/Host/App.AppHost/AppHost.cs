using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Per-app prefix from the AppHost assembly name (App.AppHost -> app); namespaces Docker volumes + resource names so multiple FSH apps don't clash.
#pragma warning disable CA1308 // resource + volume names are conventionally lowercase
var appPrefix = builder.Environment.ApplicationName
    .Replace(".AppHost", string.Empty, StringComparison.OrdinalIgnoreCase)
    .Replace('.', '-')
    .ToLowerInvariant();
#pragma warning restore CA1308

// Postgres + pgAdmin sidecar (auto-discovers registered databases); persistent so volumes and saved state survive restarts.
var postgresServer = builder.AddPostgres("postgres")
    .WithImageTag("17.11-alpine")
    .WithDataVolume($"{appPrefix}-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(pa => pa
        .WithHostPort(5050)
        .WithLifetime(ContainerLifetime.Persistent));

var postgres = postgresServer.AddDatabase("fsh-db");

// Warm pooled-connection floor for the long-running API — Npgsql's default Minimum Pool Size of 0 lets the pool drain to cold, so /readyz's ~10 concurrent DbContext checks cold-open a cohort at once and intermittently stall the probe; a floor keeps connections warm for reuse.
var apiPgConnection = ReferenceExpression.Create(
    $"{postgres.Resource.ConnectionStringExpression};Minimum Pool Size=5");

// Valkey (BSD-3 Redis fork) as a plain container: Aspire 13.4.0 AddRedis() forces TLS-by-default in run mode and never materializes the container, so we drop to plain RESP over TCP. Name stays "redis" so config keys don't churn.
var redis = builder.AddContainer("redis", "valkey/valkey", "9.1.0")
    .WithEndpoint(targetPort: 6379, scheme: "tcp", name: "tcp")
    .WithVolume($"{appPrefix}-redis-data", "/data")
    .WithLifetime(ContainerLifetime.Persistent);

var redisEndpoint = redis.GetEndpoint("tcp");
var redisConnectionString = ReferenceExpression.Create(
    $"{redisEndpoint.Property(EndpointProperty.HostAndPort)}");

// RedisInsight cache browser (dev-only) sidecar; RI_REDIS_* pre-registers the Valkey connection via the container-network alias "redis".
builder.AddContainer("redis-insight", "redis/redisinsight", "latest")
    .WithHttpEndpoint(port: 5540, targetPort: 5540, name: "http")
    .WithEnvironment("RI_REDIS_HOST0", "redis")
    .WithEnvironment("RI_REDIS_PORT0", "6379")
    .WithEnvironment("RI_REDIS_ALIAS0", "fsh-cache")
    .WithEnvironment("RI_ACCEPT_TERMS_AND_CONDITIONS", "true")
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(redis);

// Object storage, S3-compatible, with the image a dev stamp runs. The migrator creates the bucket.
var storageUser = builder.AddParameter("storage-user", "storage-local");
var storagePassword = builder.AddParameter("storage-password", "storage-local-secret", secret: true);

var storage = builder.AddContainer("storage", "rustfs/rustfs", "1.0.3")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithEnvironment("RUSTFS_ACCESS_KEY", storageUser)
    .WithEnvironment("RUSTFS_SECRET_KEY", storagePassword)
    .WithVolume($"{appPrefix}-storage-data", "/data")
    .WithLifetime(ContainerLifetime.Persistent);

var storageEndpoint = storage.GetEndpoint("api");

// The front ends' dev servers. The dashboard proxies /storage, so presigned URLs use its origin.
const int AdminPort = 5173;
const int DashboardPort = 5174;
var storagePublicEndpoint = $"http://localhost:{DashboardPort}";

// The platform's setting names (13-app-templates.md section 6.1), as a stamp sets them.
// PlatformConfiguration.cs maps them onto the options; local values only.
void AddPlatformSettings(IResourceBuilder<ProjectResource> project) => project
    .WithEnvironment("PLATFORM_ENVIRONMENT", "local")
    .WithEnvironment("S3_ENDPOINT", storageEndpoint)
    .WithEnvironment("S3_PUBLIC_ENDPOINT", storagePublicEndpoint)
    .WithEnvironment("S3_ACCESS_KEY", storageUser)
    .WithEnvironment("S3_SECRET_KEY", storagePassword);

// DB migrator: applies pending migrations, seeds the root admin (admin@root.com) and the demo tenants, then
// exits; the API waits for its completion so it never starts against an unmigrated DB. Dev-only passwords.
var migrator = builder.AddProject<Projects.App_DbMigrator>($"{appPrefix}-db-migrator")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitFor(storage)
    .WithEnvironment("DATABASE_URL", postgres.Resource.ConnectionStringExpression)
    .WithEnvironment("SEED_ADMIN_PASSWORD", "123Pa$$word!")
    .WithEnvironment("SEED_DEMO", "true")
    .WithArgs("apply", "--seed");
AddPlatformSettings(migrator);

// API Service. JWT_SIGNING_KEY and HANGFIRE_PASSWORD come from appsettings.Development.json; no SMTP_PASSWORD, so mail is off.
var api = builder.AddProject<Projects.App_Api>($"{appPrefix}-api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitFor(redis)
    .WaitForCompletion(migrator)
    .WithExternalHttpEndpoints()
    .WithEnvironment("DATABASE_URL", apiPgConnection)
    .WithEnvironment("REDIS_URL", redisConnectionString);
AddPlatformSettings(api);

// Admin console (React + Vite). Vite proxies /api to the API's HTTPS endpoint, as nginx does in the image —
// UseHttpsRedirection's 307 to https would strip the Authorization header.
builder.AddJavaScriptApp($"{appPrefix}-admin", "../../../clients/admin", "dev")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: AdminPort, targetPort: AdminPort, isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("API_UPSTREAM", api.GetEndpoint("https"))
    .WithEnvironment("STORAGE_UPSTREAM", storageEndpoint);

// Tenant-facing dashboard (React + Vite, with SSE live feed)
builder.AddJavaScriptApp($"{appPrefix}-dashboard", "../../../clients/dashboard", "dev")
    .WithNpm()
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(port: DashboardPort, targetPort: DashboardPort, isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("API_UPSTREAM", api.GetEndpoint("https"))
    .WithEnvironment("STORAGE_UPSTREAM", storageEndpoint);

await builder.Build().RunAsync();
