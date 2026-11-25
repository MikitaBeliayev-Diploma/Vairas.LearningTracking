using Microsoft.EntityFrameworkCore.Migrations;
namespace Vairas.LearningTracking.Persistance.Migrations;
public partial class InitialLearningTracking : Migration
{
    protected override void Up(MigrationBuilder builder)
    {
        builder.Sql(CanonicalSql.Read("001-initial.sql"));
        builder.Sql(CanonicalSql.Read("002-null-safe-evidence.sql"));
    }
    protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Restore a reviewed backup; retained learning evidence cannot be dropped automatically.");
}
