
using smtp.Services;

namespace smtp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
                { 
                    Title = "SMTP Microservice", 
                    Version = "v1",
                    Description = "Email service for sending notifications and templated emails"
                });
            });

            // Add CORS for frontend integration
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // Add health checks
            builder.Services.AddHealthChecks()
                .AddCheck("smtp-connection", () =>
                {
                    // This will be replaced with actual SMTP check
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("SMTP service is running");
                });

            // Add email services
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddSingleton<ITemplateService, TemplateService>();

            // Add HTTP client for external calls
            builder.Services.AddHttpClient();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SMTP Microservice v1");
                    c.RoutePrefix = "swagger";
                });
            }

            app.UseCors("AllowFrontend");

            app.UseHttpsRedirection();
            app.UseAuthorization();

            // Add health check endpoint
            app.MapHealthChecks("/health");

            // Add basic health endpoint for compatibility
            app.MapGet("/health/basic", () => Results.Ok(new { status = "ok", ts = DateTimeOffset.UtcNow, service = "smtp-microservice" }))
               .WithName("BasicHealth");

            app.MapControllers();

            // Add a simple ping endpoint
            app.MapGet("/ping", () => Results.Ok(new { message = "SMTP Microservice is running", timestamp = DateTime.UtcNow }))
               .WithName("Ping");

            app.Run();
        }
    }
}
