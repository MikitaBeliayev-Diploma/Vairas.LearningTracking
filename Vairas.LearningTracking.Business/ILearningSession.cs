using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

public interface ILearningSession : IAsyncDisposable
{
    Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class, ISchoolRow;
    Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class, ISchoolRow;
    void Add<T>(T row) where T : class, ISchoolRow;
    void Remove<T>(T row) where T : class, ISchoolRow;
    Task FlushAsync(CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
}
