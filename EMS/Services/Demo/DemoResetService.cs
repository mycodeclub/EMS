using Microsoft.Extensions.Options;

namespace EMS.Services.Demo;

/// <summary>
/// Builds the demo organization on startup if it is missing or was built before today, then rebuilds it every midnight
/// (in Demo:TimeZone, or the server's time zone).
/// </summary>
public class DemoResetService(IServiceScopeFactory scopes, IOptions<DemoOptions> options, ILogger<DemoResetService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var zone = TimeZone();

        await RunAsync(onlyIfStale: true, zone, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
            var midnight = new DateTimeOffset(now.Date.AddDays(1), zone.GetUtcOffset(now.Date.AddDays(1)));
            var wait = midnight - now;
            logger.LogInformation("Next demo reset at {Midnight} ({Wait:hh\\:mm} from now).", midnight, wait);
            try
            {
                await Task.Delay(wait + TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            await RunAsync(onlyIfStale: false, zone, stoppingToken);
        }
    }

    private async Task RunAsync(bool onlyIfStale, TimeZoneInfo zone, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
            if (onlyIfStale && await seeder.LastResetAsync(ct) is { } last
                && TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(last, DateTimeKind.Utc), zone).Date
                   == TimeZoneInfo.ConvertTime(DateTime.UtcNow, zone).Date)
            {
                return; // already built today
            }
            await seeder.ResetAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The demo organization could not be reset.");
        }
    }

    private TimeZoneInfo TimeZone()
    {
        if (string.IsNullOrWhiteSpace(options.Value.TimeZone)) return TimeZoneInfo.Local;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            logger.LogWarning("Unknown Demo:TimeZone {Zone}; using the server's time zone.", options.Value.TimeZone);
            return TimeZoneInfo.Local;
        }
    }
}
