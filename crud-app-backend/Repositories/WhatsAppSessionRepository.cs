using crud_app_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace crud_app_backend.Repositories
{
    public class WhatsAppSessionRepository : IWhatsAppSessionRepository
    {
        private readonly AppDbContext _db;
        private readonly ILogger<WhatsAppSessionRepository> _logger;

        public WhatsAppSessionRepository(
            AppDbContext db,
            ILogger<WhatsAppSessionRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ── GET ──────────────────────────────────────────────────────────

        public async Task<WhatsAppSession?> GetByPhoneAsync(
            string phone, CancellationToken ct = default)
        {
            return await _db.WhatsAppSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Phone == phone, ct);
        }

        // ── UPSERT ───────────────────────────────────────────────────────
        // Pure EF Core — no stored procedure needed.
        // Uses an execution strategy so it works with SQL Server retry policy.

        public async Task UpsertAsync(
            WhatsAppSession session,
            string? rawMessage,
            CancellationToken ct = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database
                    .BeginTransactionAsync(ct);

                try
                {
                    // ── 1. Fetch existing row (tracked) ──────────────────
                    var existing = await _db.WhatsAppSessions
                        .FirstOrDefaultAsync(s => s.Phone == session.Phone, ct);

                    string fromStep;

                    if (existing == null)
                    {
                        // ── INSERT ────────────────────────────────────────
                        fromStep = "NEW";
                        session.CreatedAt = DateTime.UtcNow;
                        session.UpdatedAt = DateTime.UtcNow;
                        await _db.WhatsAppSessions.AddAsync(session, ct);

                        _logger.LogInformation(
                            "[WA-Session] INSERT phone={Phone} step={Step}",
                            session.Phone, session.CurrentStep);
                    }
                    else
                    {
                        // ── UPDATE ────────────────────────────────────────
                        fromStep = existing.CurrentStep;

                        existing.CurrentStep = session.CurrentStep;
                        existing.PreviousStep = session.PreviousStep;
                        existing.TempData = session.TempData;
                        existing.PendingReport = session.PendingReport;
                        existing.PendingShopReg = session.PendingShopReg;
                        existing.UpdatedAt = DateTime.UtcNow;
                        // CreatedAt never changes

                        _logger.LogInformation(
                            "[WA-Session] UPDATE phone={Phone} {From}→{To}",
                            session.Phone, fromStep, session.CurrentStep);
                    }

                    // ── 2. Write history row ──────────────────────────────
                    var historyRow = new WhatsAppSessionHistory
                    {
                        Phone = session.Phone,
                        FromStep = fromStep,
                        ToStep = session.CurrentStep,
                        RawMessage = rawMessage,
                        TempDataSnapshot = session.TempData,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _db.WhatsAppSessionHistories.AddAsync(historyRow, ct);

                    // ── 3. Save both rows in one round-trip ───────────────
                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(ct);
                    _logger.LogError(ex,
                        "[WA-Session] Upsert FAILED for phone={Phone}", session.Phone);
                    throw;
                }
            });
        }

        // ── DELETE ───────────────────────────────────────────────────────

        public async Task<bool> DeleteAsync(
            string phone, CancellationToken ct = default)
        {
            var session = await _db.WhatsAppSessions
                .FirstOrDefaultAsync(s => s.Phone == phone, ct);

            if (session == null) return false;

            _db.WhatsAppSessions.Remove(session);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "[WA-Session] DELETED phone={Phone}", phone);
            return true;
        }

        // ── HISTORY ──────────────────────────────────────────────────────

        public async Task<List<WhatsAppSessionHistory>> GetHistoryAsync(
            string phone, int limit = 20, CancellationToken ct = default)
        {
            return await _db.WhatsAppSessionHistories
                .AsNoTracking()
                .Where(h => h.Phone == phone)
                .OrderByDescending(h => h.CreatedAt)
                .Take(limit)
                .ToListAsync(ct);
        }
    }
}
