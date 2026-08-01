namespace LogSift.Services;

/// <summary>
/// Generates a realistic sample log file for demo purposes.
/// </summary>
public static class LogGenerator
{
    private static readonly string[] Sources = ["AuthService", "OrderService", "PaymentGateway", "DatabasePool", "CacheManager", "ApiGateway", "NotificationHub"];
    private static readonly string[] ErrorMessages =
    [
        "Connection timeout after 30000ms waiting for database response",
        "NullReferenceException in UserRepository.GetById — userId was null",
        "Failed to process payment: gateway returned status 502",
        "JWT token validation failed: signature mismatch",
        "Unhandled exception in background job ProcessPendingOrders",
        "Index out of range in PaginationHelper.GetPage",
        "HTTP 503 from downstream service after 3 retries",
    ];
    private static readonly string[] WarnMessages =
    [
        "Response time exceeded 2000ms threshold (actual: {0}ms)",
        "Retry attempt {0}/3 for external API call",
        "Cache miss rate above 40% — consider warming the cache",
        "Memory usage at {0}% — approaching limit",
        "Rate limit approaching: {0}/1000 requests used",
    ];
    private static readonly string[] InfoMessages =
    [
        "Application started on port 8080",
        "User {0} authenticated successfully",
        "Order #{0} created and queued for processing",
        "Background job completed: processed {0} records in {1}ms",
        "Health check passed — all services operational",
        "Database migration applied: {0}",
    ];

    public static async Task GenerateAsync(string filePath, int lineCount = 50_000)
    {
        var rng = new Random(42);
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var writer = new StreamWriter(filePath, append: false);

        for (int i = 0; i < lineCount; i++)
        {
            var ts = baseDate.AddSeconds(i * 1.73).ToString("yyyy-MM-dd HH:mm:ss.fff");
            var src = Sources[rng.Next(Sources.Length)];
            var roll = rng.NextDouble();

            string level, message;
            if (roll < 0.005)
            {
                level = "FATAL";
                message = "System is shutting down due to unrecoverable error in " + src;
            }
            else if (roll < 0.04)
            {
                level = "ERROR";
                message = ErrorMessages[rng.Next(ErrorMessages.Length)];
            }
            else if (roll < 0.15)
            {
                level = "WARN";
                message = string.Format(WarnMessages[rng.Next(WarnMessages.Length)], rng.Next(100, 5000), rng.Next(1, 4));
            }
            else if (roll < 0.3)
            {
                level = "DEBUG";
                message = $"Entering method {src}.Handle with correlationId={Guid.NewGuid():N}";
            }
            else
            {
                level = "INFO";
                message = string.Format(InfoMessages[rng.Next(InfoMessages.Length)], rng.Next(1000, 99999), rng.Next(50, 3000));
            }

            await writer.WriteLineAsync($"{ts} | {level,-5} | {src,-20} | {message}");
        }
    }
}
