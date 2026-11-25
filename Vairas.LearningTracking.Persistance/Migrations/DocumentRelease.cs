using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Vairas.LearningTracking.Persistance.Migrations;

[DbContext(typeof(LearningDbContext)), Migration("20261124170000_DocumentRelease")]
public sealed class DocumentRelease : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE document_release_decision (
          id uuid PRIMARY KEY, school_id uuid NOT NULL, enrollment_id uuid NOT NULL, actor_id uuid NOT NULL,
          fingerprint text NOT NULL, status text NOT NULL CHECK(status IN ('Accepted','Rejected')),
          evaluated_revision bigint NOT NULL CHECK(evaluated_revision>=0), decided_at timestamptz NOT NULL, reason text NOT NULL
        );
        ALTER TABLE document_release_decision ENABLE ROW LEVEL SECURITY;
        ALTER TABLE document_release_decision FORCE ROW LEVEL SECURITY;
        CREATE POLICY document_release_scope ON document_release_decision TO PUBLIC
          USING(school_id=nullif(current_setting('vairas.school_id',true),'')::uuid)
          WITH CHECK(school_id=nullif(current_setting('vairas.school_id',true),'')::uuid);
        CREATE FUNCTION prevent_document_release_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Document release decisions are immutable'; END $$;
        CREATE TRIGGER document_release_immutable BEFORE UPDATE OR DELETE ON document_release_decision FOR EACH ROW EXECUTE FUNCTION prevent_document_release_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Retained publication decisions require a reviewed retention migration.");
}
