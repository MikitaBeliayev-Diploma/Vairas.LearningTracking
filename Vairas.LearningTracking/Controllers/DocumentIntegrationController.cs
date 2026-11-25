using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Integrations;
using Vairas.LearningTracking.Configuration.Options;
using Microsoft.Extensions.Options;
namespace Vairas.LearningTracking.Controllers;

[ApiController, Route("internal/v1/documents")]
public sealed class DocumentIntegrationController(ILearningDocuments service, IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings) : ControllerBase
{
    private readonly DependencyOptions options = optionsSettings.Value;
    private async Task Active(Guid actor, CancellationToken ct)
    {
        var result = await transport.SendAsync<ActorStatus>(options.UserManagementBaseUrl,$"internal/v1/users/{actor:D}/status",null,null,ct);
        if (result.UserId != actor || !result.Active) throw LearningErrors.Denied();
    }
    /// <summary>Expand exactly one retained enrollment evidence snapshot for document rendering.</summary>
    [HttpGet("evidence/{snapshotId:guid}",Name = "getDocumentEvidence"), Authorize(Policy = "learning.documents.read")]
    public async Task<ActionResult<DocumentEvidencePayload>> Evidence(Guid snapshotId, Guid schoolId, Guid enrollmentId, Guid actorUserId, CancellationToken ct)
    { await Active(actorUserId,ct); return Ok(await service.DocumentEvidenceAsync(schoolId,enrollmentId,snapshotId,actorUserId,ct)); }
    /// <summary>Evaluate the authoritative current dependency revision after source reconciliation.</summary>
    [HttpGet("enrollments/{enrollmentId:guid}/revision",Name = "getDocumentEvidenceRevision"), Authorize(Policy = "learning.documents.read")]
    public async Task<ActionResult<DocumentRevisionResponse>> Revision(Guid enrollmentId, Guid schoolId, CancellationToken ct) => Ok(new DocumentRevisionResponse(await service.DocumentRevisionAsync(schoolId,enrollmentId,ct)));
    /// <summary>Commit an idempotent Ready decision under the same local lock as evidence corrections.</summary>
    [HttpPost("decisions/{operationId:guid}",Name = "decideDocumentRelease"), Authorize(Policy = "learning.documents.decide")]
    public async Task<ActionResult<DocumentReleaseResponse>> Decide(Guid operationId, DocumentReleaseRequest request, CancellationToken ct)
    { await Active(request.ActorUserId,ct); return Ok(await service.DecideDocumentAsync(operationId,request,ct)); }
    /// <summary>Recover the historical outcome of a decision after a lost response.</summary>
    [HttpGet("decisions/{operationId:guid}",Name = "getDocumentReleaseDecision"), Authorize(Policy = "learning.documents.decide")]
    public async Task<ActionResult<DocumentReleaseResponse>> Decision(Guid operationId, Guid schoolId, Guid actorUserId, CancellationToken ct)
    { await Active(actorUserId,ct); var result = await service.DocumentDecisionAsync(schoolId,operationId,actorUserId,ct); return result is null ? NotFound() : Ok(result); }
    private sealed record ActorStatus(Guid UserId, bool Active);
}
