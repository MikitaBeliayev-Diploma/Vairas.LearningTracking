using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Vairas.LearningTracking.Persistance.Migrations;
[DbContext(typeof(LearningDbContext)), Migration("20261124113100_LearningReliability")]
public sealed class LearningReliability : Migration
{
    protected override void Up(MigrationBuilder builder) => builder.Sql(CanonicalSql.Read("003-reliability.sql"));
    protected override void Down(MigrationBuilder builder) => throw new NotSupportedException("Restore a reviewed backup; retained evidence and delivery outcomes cannot be dropped automatically.");
}
