
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
                builder.Services.AddSingleton(StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
                builder.Services.AddSingleton<SmartCollectAPI.Services.IJobQueue, SmartCollectAPI.Services.RedisJobQueue>();
                // Register background worker dependencies
                builder.Services.AddSingleton<SmartCollectAPI.Services.IContentDetector, SmartCollectAPI.Services.SimpleContentDetector>();
                builder.Services.AddSingleton<SmartCollectAPI.Services.IJsonParser, SmartCollectAPI.Services.JsonParser>();
                builder.Services.AddSingleton<SmartCollectAPI.Services.IXmlParser, SmartCollectAPI.Services.XmlParser>();
                builder.Services.AddSingleton<SmartCollectAPI.Services.ICsvParser, SmartCollectAPI.Services.CsvParser>();
                builder.Services.AddHostedService<SmartCollectAPI.Services.IngestWorker>();
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseCors("Dev");
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            // Minimal API endpoints for Hour 1
            app.MapGet("/health", () => Results.Ok(new { status = "ok", ts = DateTimeOffset.UtcNow }))
               .WithName("Health")
               .WithOpenApi();

            app.MapPost("/api/ingest", async (HttpRequest request, SmartCollectAPI.Services.IStorageService storage, SmartCollectAPI.Services.IJobQueue? queue, CancellationToken ct) =>
            {
                if (!request.HasFormContentType)
                {
                    return Results.BadRequest(new { error = "multipart/form-data required" });
                }

                var form = await request.ReadFormAsync(ct);
                var file = form.Files.GetFile("file");
                var notify = form["notify_email"].FirstOrDefault();
                if (file is null || file.Length == 0)
                {
                    return Results.BadRequest(new { error = "file is required" });
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
            })
            .DisableAntiforgery()
            .WithOpenApi();

            app.Run();
        }
    }
}
