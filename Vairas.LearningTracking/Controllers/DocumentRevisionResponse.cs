using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Integrations;
using Vairas.LearningTracking.Configuration.Options;
using Microsoft.Extensions.Options;
namespace Vairas.LearningTracking.Controllers;

/// <summary>Current owner watermark; never a release authorization.</summary>
public sealed record DocumentRevisionResponse(long DependencyRevision);
