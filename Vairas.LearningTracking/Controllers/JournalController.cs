using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
using Vairas.LearningTracking.Integrations;
namespace Vairas.LearningTracking.Controllers;

[ApiController, Authorize(Policy = "LearningEntry"), Route("api/v1")]
public sealed class JournalController(ILearningService service, ILearningActorFactory actors) : BaseApiController(actors)
{
    /// <summary>Read the exact retained historical roster for a theory lesson.</summary>
    [HttpGet("lessons/{lessonId:guid}/theory-roster", Name = "getTheoryLessonRoster")]
    public async Task<ActionResult<Roster>> Roster(Guid lessonId, Guid schoolId, CancellationToken ct) =>
        Ok(await service.TheoryRosterAsync(schoolId, lessonId, await Actor(ct), ct));
    /// <summary>Discover existing theory records independently of browser state.</summary>
    [HttpGet("lessons/{lessonId:guid}/theory-records", Name = "listTheoryLessonRecords")]
    public async Task<ActionResult<TheoryRecordResult[]>> TheoryRecords(Guid lessonId, Guid schoolId, CancellationToken ct) =>
        Ok(await service.TheoryRecordsAsync(schoolId, lessonId, await Actor(ct), ct));
    /// <summary>List practical records for one currently authorized enrollment.</summary>
    [HttpGet("student-trainings/{enrollmentId:guid}/practical-records", Name = "listPracticalRecords")]
    public async Task<ActionResult<PracticalRecordResult[]>> Records(Guid enrollmentId, Guid schoolId, CancellationToken ct) =>
        Ok(await service.PracticalRecordsAsync(schoolId, enrollmentId, await Actor(ct), ct));
    /// <summary>Verify one authoritative lesson source and obtain its immutable snapshot identity.</summary>
    [HttpPost("lessons/{lessonId:guid}/source-verifications", Name = "verifyLessonSource")]
    public async Task<ActionResult<LessonSourceSnapshotResult>> Source(Guid lessonId, Guid schoolId, CancellationToken ct) => Ok(await service.VerifySourceAsync(schoolId, lessonId, await Actor(ct), ct));
    /// <summary>Create one practical record against a completed physical lesson.</summary>
    [HttpPost("practical-records", Name = "createPracticalRecord"), ProducesResponseType<PracticalRecordResult>(201)]
    public async Task<ActionResult<PracticalRecordResult>> Practical(PracticalFactsRequest request, CancellationToken ct)
    { var r = await service.PracticalAsync(null, request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/practical-records/{r.Value.RecordId:D}?schoolId={request.SchoolId:D}"); }
    /// <summary>Read the authorized practical version and participant response history.</summary>
    [HttpGet("practical-records/{recordId:guid}", Name = "getPracticalRecord")]
    public async Task<ActionResult<PracticalRecordResult>> PracticalGet(Guid recordId, Guid schoolId, CancellationToken ct) => Ok(await service.PracticalReadAsync(schoolId, recordId, await Actor(ct), ct));
    /// <summary>Append corrected facts. Both participants must confirm the new version.</summary>
    [HttpPost("practical-records/{recordId:guid}/versions", Name = "revisePracticalRecord"), ProducesResponseType<PracticalRecordResult>(201)]
    public async Task<ActionResult<PracticalRecordResult>> PracticalVersion(Guid recordId, PracticalFactsRequest request, CancellationToken ct)
    { var r = await service.PracticalAsync(recordId, request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/practical-records/{recordId:D}?schoolId={request.SchoolId:D}"); }
    /// <summary>Assigned instructor or enrolled student confirms the exact current revision.</summary>
    [HttpPost("practical-records/{recordId:guid}/confirmations", Name = "confirmPracticalRecord")]
    public async Task<ActionResult<PracticalRecordResult>> Confirm(Guid recordId, Guid schoolId, ConfirmationRequest request, CancellationToken ct) => Ok((await service.RespondAsync(schoolId, recordId, request.Revision, ResponseKind.Confirm, null, Operation(), await Actor(ct), ct)).Value);
    /// <summary>Dispute the current fact version, permanently closing its credit.</summary>
    [HttpPost("practical-records/{recordId:guid}/disputes", Name = "disputePracticalRecord")]
    public async Task<ActionResult<PracticalRecordResult>> Dispute(Guid recordId, Guid schoolId, DisputeRequest request, CancellationToken ct) => Ok((await service.RespondAsync(schoolId, recordId, request.Revision, ResponseKind.Dispute, request.Reason, Operation(), await Actor(ct), ct)).Value);
    /// <summary>Create a whole-group theory record with a complete frozen roster.</summary>
    [HttpPost("theory-records", Name = "recordTheoryAttendance"), ProducesResponseType<TheoryRecordResult>(201)]
    public async Task<ActionResult<TheoryRecordResult>> Theory(TheoryFactsRequest request, CancellationToken ct)
    { var r = await service.TheoryAsync(null, request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/theory-records/{r.Value.RecordId:D}?schoolId={request.SchoolId:D}"); }
    /// <summary>Read the full roster only under assigned-teacher or administrator journal authority.</summary>
    [HttpGet("theory-records/{recordId:guid}", Name = "getTheoryRecord")]
    public async Task<ActionResult<TheoryRecordResult>> TheoryGet(Guid recordId, Guid schoolId, CancellationToken ct) => Ok(await service.TheoryReadAsync(schoolId, recordId, await Actor(ct), ct));
    /// <summary>Append a complete corrected theory version without retaining its former confirmation.</summary>
    [HttpPost("theory-records/{recordId:guid}/versions", Name = "reviseTheoryRecord"), ProducesResponseType<TheoryRecordResult>(201)]
    public async Task<ActionResult<TheoryRecordResult>> TheoryVersion(Guid recordId, TheoryFactsRequest request, CancellationToken ct)
    { var r = await service.TheoryAsync(recordId, request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/theory-records/{recordId:D}?schoolId={request.SchoolId:D}"); }
    /// <summary>Teacher explicitly confirms the exact current full-roster theory version.</summary>
    [HttpPost("theory-records/{recordId:guid}/confirmations", Name = "confirmTheoryRecord")]
    public async Task<ActionResult<TheoryRecordResult>> TheoryConfirm(Guid recordId, Guid schoolId, ConfirmationRequest request, CancellationToken ct) => Ok((await service.ConfirmTheoryAsync(schoolId, recordId, request.Revision, Operation(), await Actor(ct), ct)).Value);
}

