using System.Net;
using System.Text.Json;

namespace SmartCollectAPI.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred while processing request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse();

        switch (exception)
        {
            case ArgumentException argEx:
                response.Error = "Invalid request parameters";
                response.Details = argEx.Message;
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;

            case FileNotFoundException fileEx:
                response.Error = "Resource not found";
                response.Details = fileEx.Message;
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;

            case UnauthorizedAccessException:
                response.Error = "Access denied";
                response.Details = "You do not have permission to access this resource";
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                break;

            case TimeoutException:
                response.Error = "Request timeout";
                response.Details = "The request took too long to process";
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                break;

            case InvalidOperationException invalidOpEx:
                response.Error = "Invalid operation";
                response.Details = invalidOpEx.Message;
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                break;

            default:
                response.Error = "Internal server error";
                response.Details = "An unexpected error occurred";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? RequestId { get; set; }
}