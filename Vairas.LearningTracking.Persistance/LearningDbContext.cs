using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Persistance;

public sealed class LearningDbContext(DbContextOptions<LearningDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        foreach (var type in typeof(ISchoolRow).Assembly.GetTypes().Where(t => t.IsClass && typeof(ISchoolRow).IsAssignableFrom(t)))
        {
            var entity = model.Entity(type);
            entity.ToTable(Snake(type.Name));
            entity.HasKey(nameof(ISchoolRow.Id));
            foreach (var property in type.GetProperties())
            {
                var p = entity.Property(property.Name).HasColumnName(Snake(property.Name));
                if (property.Name == "Id") p.ValueGeneratedNever();
                var actual = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (actual.IsEnum) p.HasColumnType(EnumName(actual));
                if (actual == typeof(decimal)) p.HasPrecision(10, 3);
            }
        }
        model.Entity<LearningOperation>().HasKey(x => new { x.SchoolId, x.Id });
        model.Entity<PolicyAttestation>().HasKey(x => new { x.SchoolId, x.Id });
        model.Entity<OutboxMessage>().HasIndex(x => new { x.SentAt, x.NextAttemptAt });
        model.Entity<LessonPlanningPolicy>().HasIndex(x => new { x.SchoolId, x.ProgramVersionId, x.Kind }).IsUnique();
        model.Entity<LearningOperation>().Property(x => x.ResultJson).HasColumnType("text");
    }
    public static string Snake(string value) => Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    public static string EnumName(Type type) => "lt_" + Snake(type.Name);
}
