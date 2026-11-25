using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Verified external checklist configuration, immutable by version identifier.</summary>
public sealed record ChecklistPolicy(Guid VersionId, Guid SchoolId, string Category, string Pathway, string SourceUri, string Edition,
    Guid VerifiedBy, DateTimeOffset VerifiedAt, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool Synthetic, string[] PracticalFields, string[] JournalChecks);
