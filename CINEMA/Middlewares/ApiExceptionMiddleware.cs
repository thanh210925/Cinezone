using System.Net;
using System.Text.Json;

namespace CINEMA.Middlewares
{
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Nếu request là API path (/api/...) thì trả về JSON error chuẩn
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    _logger.LogError(ex, "Unhandled API Exception on path: {Path}", context.Request.Path);
                    await HandleApiExceptionAsync(context, ex, _env);
                }
                else
                {
                    // Nếu là MVC view thì rethrow để app.UseExceptionHandler("/Home/Error") xử lý
                    throw;
                }
            }
        }

        private static async Task HandleApiExceptionAsync(HttpContext context, Exception exception, IHostEnvironment env)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var errorMessage = exception.InnerException != null 
                ? $"{exception.Message} -> Inner: {exception.InnerException.Message}"
                : exception.Message;

            var response = new
            {
                success = false,
                message = "Đã xảy ra lỗi hệ thống khi xử lý API. Vui lòng thử lại sau.",
                error = env.IsDevelopment() ? errorMessage : null,
                detail = env.IsDevelopment() ? exception.StackTrace : null
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(response, options);
            await context.Response.WriteAsync(json);
        }
    }

    public static class ApiExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseApiExceptionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiExceptionMiddleware>();
        }
    }
}
