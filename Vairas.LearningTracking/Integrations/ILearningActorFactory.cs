using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Middleware;
using Vairas.LearningTracking.Configuration.Options;

namespace Vairas.LearningTracking.Integrations;


public interface ILearningActorFactory { Task<LearningActor> CreateAsync(HttpContext http, CancellationToken ct); }
