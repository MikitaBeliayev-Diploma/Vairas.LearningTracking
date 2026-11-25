using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Respond to the exact current aggregate revision; confirmations never carry forward to corrected facts.</summary>
public sealed record ConfirmationRequest(long Revision);
