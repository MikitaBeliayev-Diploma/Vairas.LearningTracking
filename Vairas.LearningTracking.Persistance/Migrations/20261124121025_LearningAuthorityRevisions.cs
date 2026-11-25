using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vairas.LearningTracking.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class LearningAuthorityRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "school_source_state",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    last_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_source_state", x => x.id);
                });
            migrationBuilder.Sql(CanonicalSql.Read("004-authority-guards.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Restore a reviewed backup; observed authority revisions and retained evidence cannot be dropped automatically.");
        }
    }
}
