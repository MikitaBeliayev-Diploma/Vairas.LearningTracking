using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.LearningTracking.Business;

namespace Vairas.LearningTracking.Controllers;

[ApiController, Route("internal/v1/notifications/assessments/{assessmentId:guid}"), Authorize(Policy = "learning.notifications.read")]
public sealed class NotificationSourceController(INotificationSourceService service) : ControllerBase
{
    /// <summary>Read the current notification-safe assessment state, including corrections and invalidation.</summary>
    [HttpGet(Name = "getAssessmentNotificationSource")]
    public Task<NotificationSourceResponse> Read(Guid assessmentId, Guid schoolId, CancellationToken ct) => service.ReadAsync(schoolId, assessmentId, ct);
}
