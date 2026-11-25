using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Full immutable Present/Absent participant row.</summary>
public sealed record AttendanceInput(Guid EnrollmentId, Guid StudentId, Attendance Attendance);
