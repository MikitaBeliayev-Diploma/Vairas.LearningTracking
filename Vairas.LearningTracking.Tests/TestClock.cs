using System.Linq.Expressions;
using System.Text.Json;
using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Tests;

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026,11,24,12,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
