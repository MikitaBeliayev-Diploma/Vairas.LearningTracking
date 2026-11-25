using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Complete editable programme draft. Published content can only be changed in a new version.</summary>
public sealed record ProgramVersionRequest(Guid SchoolId, long ExpectedRevision, string PathwayCode, int RequiredTheoryMinutes, int RequiredPracticalMinutes,
    int TheoryMinutesPerUnit, int PracticalMinutesPerUnit, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid ChecklistVersionId,
    TopicInput[] Topics, DefinitionInput[] Assessments, RuleSourceInput[] Sources, PlanningPolicyInput[] PlanningPolicies);
