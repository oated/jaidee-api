using System.Diagnostics;
using System.Text.Json;

namespace JaiDee.API.Middleware;

public class GlobalExceptionHandlingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

  public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
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
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
      _logger.LogInformation("Request was canceled by client. TraceId={TraceId}", context.TraceIdentifier);
    }
    catch (Exception ex)
    {
      var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
      _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);

      if (context.Response.HasStarted)
      {
        throw;
      }

      context.Response.ContentType = "application/json";
      context.Response.StatusCode = ex switch
      {
        ArgumentException => StatusCodes.Status400BadRequest,
        InvalidOperationException => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status500InternalServerError
      };

      var payload = JsonSerializer.Serialize(new
      {
        message = context.Response.StatusCode == StatusCodes.Status500InternalServerError
          ? "Something went wrong."
          : ex.Message,
        traceId
      });

      await context.Response.WriteAsync(payload);
    }
  }
}
