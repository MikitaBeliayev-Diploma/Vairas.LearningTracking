using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Derived current progress. Percent is null when the configured total is not applicable.</summary>
public sealed record ProgressResponse(Guid EnrollmentId, int TheoryMinutes, int PracticalMinutes, decimal? Percent, string[] OutstandingRequirements,
    int PendingRecordCount, int DisputedRecordCount, int? TheoryDeficitMinutes, int? PracticalDeficitMinutes, long DependencyRevision, bool SourcesVerified, string State = "Assigned");
