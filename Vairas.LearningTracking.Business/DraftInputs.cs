using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Compatible Scheduling input set. Preferences are informational; hard constraints remain owned by Scheduling.</summary>
public sealed record DraftInputs(Guid SchoolId, Guid? ProgramVersionId = null, Guid[]? EnrollmentIds = null, Guid[]? GroupIds = null, Guid[]? TeacherIds = null,
    Guid[]? VehicleIds = null, DateOnly? PlanningStart = null, DateOnly? PlanningEnd = null, Guid[]? RuleVersionIds = null, TimeWindow[]? Windows = null,
    string[]? Preferences = null, int? ExpectedRevision = null, RecurrenceInput[]? Recurrences = null);
