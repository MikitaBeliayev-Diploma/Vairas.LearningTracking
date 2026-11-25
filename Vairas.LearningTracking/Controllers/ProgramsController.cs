using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Integrations;
namespace Vairas.LearningTracking.Controllers;

[ApiController, Authorize(Policy = "LearningEntry"), Route("api/v1/training-programs")]
public sealed class ProgramsController(ILearningService service, ILearningActorFactory actors) : BaseApiController(actors)
{
    /// <summary>Create a school-owned programme identity.</summary>
    [HttpPost(Name = "createTrainingProgram"), ProducesResponseType<ProgramResponse>(201)]
    public async Task<ActionResult<ProgramResponse>> Create(CreateProgramRequest request, CancellationToken ct)
    { var r = await service.CreateProgramAsync(request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/training-programs?schoolId={request.SchoolId:D}"); }
    /// <summary>List the current administrator's school programmes.</summary>
    [HttpGet(Name = "listTrainingPrograms")]
    public async Task<ActionResult<ProgramResponse[]>> List(Guid schoolId, CancellationToken ct) => Ok(await service.ProgramsAsync(schoolId, await Actor(ct), ct));
    /// <summary>List all retained draft, published and retired versions in the administrator's school.</summary>
    [HttpGet("{programId:guid}/versions", Name = "listProgramVersions")]
    public async Task<ActionResult<ProgramVersionResponse[]>> Versions(Guid programId, Guid schoolId, CancellationToken ct) =>
        Ok(await service.ProgramVersionsAsync(schoolId, programId, await Actor(ct), ct));
    /// <summary>Create an editable curriculum version under optimistic programme revision.</summary>
    [HttpPost("{programId:guid}/versions", Name = "createProgramVersion"), ProducesResponseType<ProgramVersionResponse>(201)]
    public async Task<ActionResult<ProgramVersionResponse>> CreateVersion(Guid programId, ProgramVersionRequest request, CancellationToken ct)
    { var r = await service.SaveVersionAsync(programId, null, request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/training-program-versions/{r.Value.ProgramVersionId:D}?schoolId={request.SchoolId:D}"); }
    /// <summary>Replace draft content; published and retired content is immutable.</summary>
    [HttpPut("{programId:guid}/versions/{versionId:guid}", Name = "editProgramDraft")]
    public async Task<ActionResult<ProgramVersionResponse>> Edit(Guid programId, Guid versionId, ProgramVersionRequest request, CancellationToken ct) => Ok((await service.SaveVersionAsync(programId, versionId, request, Operation(), await Actor(ct), ct)).Value);
    /// <summary>Read the complete exact curriculum version.</summary>
    [HttpGet("/api/v1/training-program-versions/{versionId:guid}", Name = "getProgramVersion")]
    public async Task<ActionResult<ProgramVersionResponse>> Get(Guid versionId, Guid schoolId, CancellationToken ct) => Ok(await service.VersionAsync(schoolId, versionId, await Actor(ct), ct));
    /// <summary>Publish or retire a version with current verified applicability and explicit revision.</summary>
    [HttpPost("/api/v1/training-program-versions/{versionId:guid}/{transition:regex(^(publish|retire)$)}", Name = "changeProgramLifecycle")]
    public async Task<ActionResult<ProgramVersionResponse>> Lifecycle(Guid versionId, string transition, Guid schoolId, RevisionRequest request, CancellationToken ct) => Ok((await service.LifecycleAsync(schoolId, versionId, transition, request, Operation(), await Actor(ct), ct)).Value);
}

