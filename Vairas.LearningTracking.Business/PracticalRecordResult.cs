using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Current practical fact version with independent aggregate revision and response state.</summary>
public sealed record PracticalRecordResult(Guid RecordId, Guid EnrollmentId, long Revision, int FactVersion, string Status, PracticalFactsRequest Facts, ParticipantResponse[] Responses, bool CreditEligible = false);
