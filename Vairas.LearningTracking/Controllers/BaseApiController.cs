using Microsoft.AspNetCore.Mvc;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Integrations;
namespace Vairas.LearningTracking.Controllers;

/// <summary>
/// Common HTTP-level contract for MVC controllers: automatic model validation and binding-source inference, plus the
/// server-resolved actor and the mandatory Idempotency-Key header. Authorization stays on each controller.
/// </summary>
[ApiController]
public abstract class BaseApiController(ILearningActorFactory actors) : ControllerBase
{
    protected Task<LearningActor> Actor(CancellationToken ct) => actors.CreateAsync(HttpContext, ct);
    protected Guid Operation()
    {
        if (!Guid.TryParse(Request.Headers["Idempotency-Key"], out var id) || id == Guid.Empty) throw LearningErrors.Invalid("IDEMPOTENCY_KEY_REQUIRED");
        return id;
    }
    protected ActionResult<T> CreatedOrReplay<T>(CommandResult<T> result, string location) => result.Replayed ? Ok(result.Value) : Created(location, result.Value);
}
