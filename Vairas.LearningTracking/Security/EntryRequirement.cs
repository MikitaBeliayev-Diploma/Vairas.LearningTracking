using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Middleware;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Security;


public sealed record EntryRequirement(string? Scope = null) : IAuthorizationRequirement;
