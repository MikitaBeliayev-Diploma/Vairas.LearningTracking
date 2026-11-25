using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Configuration;
using Vairas.LearningTracking.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddChecklistPolicyFile();
builder.Logging.AddJsonLogging();
builder.Services
    .AddLearningOptions(builder.Configuration)
    .AddStrictJsonSerialization()
    .AddExceptionHandling()
    .AddOpenApiDocument()
    .AddLearningDatabase()
    .AddLearningServices()
    .AddDependencyHttpClients()
    .AddMessageBroker()
    .AddBearerAuthentication()
    .AddEntryAuthorization()
    .AddServiceHealthChecks()
    .AddRequestRateLimiting()
    .AddServiceTelemetry(builder.Configuration);

var app = builder.Build();

// Fail fast: the external checklist policies are bound and verified before the first request.
_ = app.Services.GetRequiredService<IChecklistRegistry>();

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(context => context.HttpContext.WriteProblemAsync(context.HttpContext.Response.StatusCode, "HTTP_ERROR", "The request could not be completed."));
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.Use((http, next) => Vairas.Delegation.ReadPreviewDelegation.EnforceAsync(http, next, "learning-tracking"));
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapServiceHealthChecks();
app.MapOpenApiDocument();
app.Run();

public partial class Program;
