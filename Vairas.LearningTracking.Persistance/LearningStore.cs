using System.Linq.Expressions;
using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Persistance;

public sealed class LearningStore(IDbContextFactory<LearningDbContext> factory) : ILearningStore
{
    public Task<ILearningSession> OpenAsync(Guid school, bool write, CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(() => OpenCoreAsync(school, write, ct));
    private async Task<ILearningSession> OpenCoreAsync(Guid school, bool write, CancellationToken ct)
    {
        if (school == Guid.Empty) throw LearningErrors.Invalid();
        var db = await factory.CreateDbContextAsync(ct);
        try
        {
            // READ COMMITTED after the school advisory lock observes preceding commits.
            var tx = await db.Database.BeginTransactionAsync(write ? System.Data.IsolationLevel.ReadCommitted : System.Data.IsolationLevel.RepeatableRead, ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('vairas.school_id',{school.ToString("D")},true)", ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout='3s'; SET CONSTRAINTS ALL DEFERRED", ct);
            if (write)
            {
                var key = BinaryPrimitives.ReadInt64LittleEndian(school.ToByteArray());
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
            }
            else db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            return new Session(db, tx, school);
        }
        catch { await db.DisposeAsync(); throw; }
    }
    private sealed class Session(LearningDbContext db, IDbContextTransaction tx, Guid school) : ILearningSession
    {
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class, ISchoolRow =>
            DatabaseExceptionTranslator.GuardAsync(() => db.Set<T>().Where(x => x.SchoolId == school).SingleOrDefaultAsync(predicate, ct));
        public async Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class, ISchoolRow =>
            await DatabaseExceptionTranslator.GuardAsync<IReadOnlyList<T>>(async () => await db.Set<T>().Where(x => x.SchoolId == school).Where(predicate).ToArrayAsync(ct));
        public void Add<T>(T row) where T : class, ISchoolRow
        {
            if (row.SchoolId != school) throw LearningErrors.Denied();
            db.Add(row);
        }
        public void Remove<T>(T row) where T : class, ISchoolRow => db.Remove(row);
        public Task FlushAsync(CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(() => db.SaveChangesAsync(ct));
        public Task CommitAsync(CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(async () => { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); });
        public async ValueTask DisposeAsync() { await tx.DisposeAsync(); await db.DisposeAsync(); }
    }
}
