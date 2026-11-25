using Microsoft.EntityFrameworkCore.Migrations;
namespace Vairas.LearningTracking.Persistance.Migrations;
// Scalar EF metadata only. Schema and deferred integrity are installed by the preceding SQL-owned migration.
public partial class DocumentReleaseMapping : Migration { protected override void Up(MigrationBuilder b) { } protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Retained evidence requires a reviewed restore."); }
