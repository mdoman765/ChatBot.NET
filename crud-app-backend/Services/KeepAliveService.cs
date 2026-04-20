using Microsoft.EntityFrameworkCore;

namespace crud_app_backend.Services
{
    /// <summary>
    /// Runs a real table query against SQL Server every 3 minutes.
    /// Keeps the EF connection pool warm and prevents IIS app pool idle timeout.
    /// </summary>
    public class KeepAliveService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<KeepAliveService> _logger;

        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(3);

        public KeepAliveService(
            IServiceScopeFactory scopeFactory,
            ILogger<KeepAliveService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[KeepAlive] Service started — pinging every {Min} min",
                Interval.TotalMinutes);

            // Wait 30s after startup so EF migrations / warmup finish first
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await PingAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }

            _logger.LogInformation("[KeepAlive] Service stopped.");
        }

        private async Task PingAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Real table query — fetches the most recently updated session row.
                // TOP 1 + AsNoTracking = zero EF overhead, single lightweight DB read.
                var session = await db.WhatsAppSessions
                    .AsNoTracking()
                    .OrderByDescending(s => s.UpdatedAt)
                    .Select(s => s.Phone)   // only pull one column
                    .FirstOrDefaultAsync(ct);

                _logger.LogDebug("[KeepAlive] DB ping OK at {Time:HH:mm:ss} — last active: {Phone}",
                    DateTime.UtcNow, session ?? "no sessions yet");
            }
            catch (OperationCanceledException)
            {
                // App shutting down — normal, ignore
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[KeepAlive] DB ping failed — will retry in {Min} min",
                    Interval.TotalMinutes);
            }
        }
    }
}
