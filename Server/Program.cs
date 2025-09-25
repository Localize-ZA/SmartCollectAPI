
using Microsoft.EntityFrameworkCore;
using SmartCollectAPI.Data;
using StackExchange.Redis;
using Npgsql;
using SmartCollectAPI.Services.Providers;
using SmartCollectAPI.Middleware;

namespace SmartCollectAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Add Entity Framework with PostgreSQL
            // Resolve connection string: prefer DefaultConnection, fall back to "Postgres" (present in appsettings.Development.json)
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = builder.Configuration.GetConnectionString("Postgres");
            }
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                // Build a shared NpgsqlDataSource and enable dynamic JSON serialization so System.Text.Json.Nodes
                // (JsonNode/JsonObject) can be mapped to PostgreSQL jsonb columns.
                var dsBuilder = new NpgsqlDataSourceBuilder(connectionString);
                dsBuilder.EnableDynamicJson();
                var dataSource = dsBuilder.Build();
                builder.Services.AddSingleton(dataSource);

                // Wire EF Core to use the shared data source and pgvector
                builder.Services.AddDbContext<SmartCollectDbContext>((sp, options) =>
                {
                    var ds = sp.GetRequiredService<NpgsqlDataSource>();
                    options.UseNpgsql(ds, npgsqlOptions => npgsqlOptions.UseVector());
                });
            }

            // Add health checks
            // Health check: connect to the default 'postgres' database to avoid false negatives
            // if the app database is temporarily missing during setup.
            var hcBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString ?? string.Empty)
            {
                Database = "postgres"
            };
            builder.Services.AddHealthChecks()
                .AddNpgSql(hcBuilder.ToString(), name: "postgres")
                .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? string.Empty, name: "redis");

            // Configuration: Storage and Redis
            var storageSection = builder.Configuration.GetSection("Storage");
            var provider = storageSection.GetValue<string>("Provider") ?? "Local";
            if (string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.Configure<SmartCollectAPI.Services.LocalStorageOptions>(o =>
                {
                    o.LocalPath = storageSection.GetValue<string>("LocalPath") ?? "uploads";
                });
                builder.Services.AddSingleton<SmartCollectAPI.Services.IStorageService, SmartCollectAPI.Services.LocalStorageService>();
            }

            // Configure provider options
            builder.Services.Configure<ServicesOptions>(builder.Configuration.GetSection("Services"));
            builder.Services.Configure<GoogleCloudOptions>(builder.Configuration.GetSection("GoogleCloud"));
            builder.Services.Configure<GmailOptions>(builder.Configuration.GetSection("Gmail"));
            builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

            // CORS for local dev (allow Next.js on :3000)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("Dev", policy =>
                {
                    policy
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .WithOrigins("http://localhost:3000", "https://localhost:3000");
                });
            });

            // Redis connection and job queue
            var redisConn = builder.Configuration.GetConnectionString("Redis");
            if (!string.IsNullOrWhiteSpace(redisConn))
            {
                builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
                {
                    var options = StackExchange.Redis.ConfigurationOptions.Parse(redisConn);
                    options.AbortOnConnectFail = false; // keep retrying in background
                    options.ConnectRetry = Math.Max(3, options.ConnectRetry);
                    options.ConnectTimeout = Math.Max(5000, options.ConnectTimeout);
                    return StackExchange.Redis.ConnectionMultiplexer.Connect(options);
                });
                builder.Services.AddSingleton<SmartCollectAPI.Services.IJobQueue, SmartCollectAPI.Services.RedisJobQueue>();
                
                // Register basic services (still needed for structured data parsing)
                builder.Services.AddSingleton<SmartCollectAPI.Services.IContentDetector, SmartCollectAPI.Services.SimpleContentDetector>();
                builder.Services.AddSingleton<SmartCollectAPI.Services.IJsonParser, SmartCollectAPI.Services.JsonParser>();
                builder.Services.AddSingleton<SmartCollectAPI.Services.IXmlParser, SmartCollectAPI.Services.XmlParser>();
                builder.Services.AddSingleton<SmartCollectAPI.Services.ICsvParser, SmartCollectAPI.Services.CsvParser>();

                // Register Google Cloud providers
                builder.Services.AddScoped<GoogleDocAiParser>();
                builder.Services.AddScoped<GoogleVisionOcrService>();
                builder.Services.AddScoped<GoogleEntityExtractionService>();
                builder.Services.AddScoped<VertexEmbeddingService>();
                builder.Services.AddScoped<GmailNotificationService>();

                // Register OSS fallback providers
                builder.Services.AddScoped<SimplePdfParser>();
                builder.Services.AddScoped<SimpleEmbeddingService>();
                builder.Services.AddScoped<SmtpNotificationService>();

                // Register provider factory
                builder.Services.AddScoped<IProviderFactory, ProviderFactory>();

                // Register enhanced processing pipeline
                builder.Services.AddScoped<SmartCollectAPI.Services.IDocumentProcessingPipeline, SmartCollectAPI.Services.DocumentProcessingPipeline>();
                
                // Register background worker
                builder.Services.AddHostedService<SmartCollectAPI.Services.IngestWorker>();
            }


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            // Add global exception handling middleware first
            app.UseMiddleware<GlobalExceptionMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseCors("Dev");
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            // Add health checks endpoint
            app.MapHealthChecks("/health");

            // Minimal API endpoints for Hour 1
            app.MapGet("/health/basic", () => Results.Ok(new { status = "ok", ts = DateTimeOffset.UtcNow }))
               .WithName("BasicHealth")
               .WithOpenApi();

            app.MapPost("/api/ingest", async (HttpRequest request, SmartCollectAPI.Services.IStorageService storage, SmartCollectAPI.Services.IJobQueue? queue, CancellationToken ct) =>
            {
                try
                {
                    if (!request.HasFormContentType)
                    {
                        return Results.BadRequest(new { error = "multipart/form-data required", details = "Request must use multipart/form-data content type" });
                    }

                    var form = await request.ReadFormAsync(ct);
                    var file = form.Files.GetFile("file");
                    var notify = form["notify_email"].FirstOrDefault();
                    
                    if (file is null || file.Length == 0)
                    {
                        return Results.BadRequest(new { error = "file is required", details = "A valid file must be provided in the 'file' field" });
                    }

                    // Validate file size (e.g., max 100MB)
                    const long maxFileSize = 100L * 1024 * 1024; // 100MB
                    if (file.Length > maxFileSize)
                    {
                        return Results.BadRequest(new { error = "file too large", details = $"File size cannot exceed {maxFileSize / (1024 * 1024)}MB" });
                    }

                    // Validate email format if provided
                    if (!string.IsNullOrWhiteSpace(notify) && !IsValidEmail(notify))
                    {
                        return Results.BadRequest(new { error = "invalid email format", details = "The notify_email field must contain a valid email address" });
                    }

                    await using var stream = file.OpenReadStream();
                    var sha = await SmartCollectAPI.Services.Hashing.ComputeSha256Async(stream, resetPosition: true, ct);
                    var savedPath = await storage.SaveAsync(stream, file.FileName, ct);
                    var mime = file.ContentType ?? "application/octet-stream";

                    var job = new SmartCollectAPI.Models.JobEnvelope(
                        JobId: Guid.NewGuid(),
                        SourceUri: savedPath,
                        MimeType: mime,
                        Sha256: sha,
                        ReceivedAt: DateTimeOffset.UtcNow,
                        Origin: "web",
                        NotifyEmail: string.IsNullOrWhiteSpace(notify) ? null : notify
                    );

                    if (queue is not null)
                    {
                        await queue.EnqueueAsync(job, ct);
                    }

                    return Results.Accepted($"/api/jobs/{job.JobId}", new { job_id = job.JobId, sha256 = sha, source_uri = savedPath });
                }
                catch (Exception ex) when (ex is OperationCanceledException)
                {
                    return Results.BadRequest(new { error = "request cancelled", details = "The request was cancelled before completion" });
                }
                catch (Exception ex) when (ex is IOException)
                {
                    return Results.Json(new { error = "file processing error", details = "An error occurred while processing the uploaded file" }, statusCode: 500);
                }
            })
            .DisableAntiforgery()
            .WithOpenApi();

            app.Run();
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
