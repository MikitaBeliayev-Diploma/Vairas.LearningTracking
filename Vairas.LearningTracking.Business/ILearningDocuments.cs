namespace Vairas.LearningTracking.Business;

public interface ILearningDocuments
{
    Task<DocumentEvidencePayload> DocumentEvidenceAsync(Guid school, Guid enrollment, Guid snapshot, Guid actor, CancellationToken ct);
    Task<DocumentReleaseResponse> DecideDocumentAsync(Guid operation, DocumentReleaseRequest request, CancellationToken ct);
    Task<DocumentReleaseResponse?> DocumentDecisionAsync(Guid school, Guid operation, Guid actor, CancellationToken ct);
    Task<long> DocumentRevisionAsync(Guid school, Guid enrollment, CancellationToken ct);
}
