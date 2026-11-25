using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Participant response for one immutable practical fact version.</summary>
public sealed record ParticipantResponse(ResponseRole Role, ResponseKind Response, int FactVersion);
