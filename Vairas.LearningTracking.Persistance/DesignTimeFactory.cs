using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Vairas.LearningTracking.Persistance;
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<LearningDbContext>()
        .UseLearning(Environment.GetEnvironmentVariable("VAIRAS_MIGRATION_CONNECTION") ?? "Host=localhost;Database=vairas_learning_tracking;Username=v_learning_tracking_migration").Options);
}
