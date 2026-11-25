using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

public interface IChecklistRegistry { ChecklistPolicy? Find(Guid version, Guid school, string category, string pathway, DateOnly date); }
