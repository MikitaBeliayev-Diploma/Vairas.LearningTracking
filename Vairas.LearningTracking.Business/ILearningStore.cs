using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

public interface ILearningStore
{
    Task<ILearningSession> OpenAsync(Guid school, bool write, CancellationToken ct);
}
