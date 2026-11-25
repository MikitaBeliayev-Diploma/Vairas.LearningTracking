using System.Linq.Expressions;
using System.Text.Json;
using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Tests;

internal sealed class MemoryStore : ILearningStore
{
    private readonly SemaphoreSlim gate = new(1,1);
    public List<ISchoolRow> Rows { get; private set; } = [];
    public async Task<ILearningSession> OpenAsync(Guid school,bool write,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        var rows=Rows.Select(x=>(ISchoolRow)JsonSerializer.Deserialize(JsonSerializer.Serialize(x,x.GetType(),Wire.Json),x.GetType(),Wire.Json)!).ToList();
        return new Session(this,school,rows,write);
    }
    private sealed class Session(MemoryStore owner,Guid school,List<ISchoolRow> rows,bool write):ILearningSession
    {
        public Task<T?> FindAsync<T>(Expression<Func<T,bool>> predicate,CancellationToken ct) where T:class,ISchoolRow => Task.FromResult(rows.OfType<T>().Where(x=>x.SchoolId==school).SingleOrDefault(predicate.Compile()));
        public Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T,bool>> predicate,CancellationToken ct) where T:class,ISchoolRow => Task.FromResult<IReadOnlyList<T>>(rows.OfType<T>().Where(x=>x.SchoolId==school).Where(predicate.Compile()).ToArray());
        public void Add<T>(T row) where T:class,ISchoolRow { Assert.That(write);Assert.That(row.SchoolId,Is.EqualTo(school));rows.Add(row); }
        public void Remove<T>(T row) where T:class,ISchoolRow => rows.Remove(row);
        public Task FlushAsync(CancellationToken ct)=>Task.CompletedTask;
        public Task CommitAsync(CancellationToken ct){Assert.That(write);owner.Rows=rows;return Task.CompletedTask;}
        public ValueTask DisposeAsync(){owner.gate.Release();return ValueTask.CompletedTask;}
    }
}
