using System.Security.Cryptography;
using System.Text;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Verified source identifier returned for subsequent exact-source journal commands.</summary>
public sealed record LessonSourceSnapshotResult(Guid SourceSnapshotId, Guid LessonId, int LessonRevision, Freshness Freshness, bool PhysicallyCompleted, DateTimeOffset? ActualStart, DateTimeOffset? ActualEnd);
