using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Persistance;

public static class LearningDatabase
{
    private static readonly NpgsqlNullNameTranslator Names = new();
    public static DbContextOptionsBuilder<LearningDbContext> UseLearning(this DbContextOptionsBuilder<LearningDbContext> options, string connection)
    { UseLearning((DbContextOptionsBuilder)options, connection); return options; }
    public static DbContextOptionsBuilder UseLearning(this DbContextOptionsBuilder options, string connection) =>
        options.UseNpgsql(connection, pg =>
        {
            var labels = Names;
            pg.MapEnum<ProgramStatus>("lt_program_status", nameTranslator: labels);
            pg.MapEnum<TrainingKind>("lt_training_kind", nameTranslator: labels);
            pg.MapEnum<AssessmentStage>("lt_assessment_stage", nameTranslator: labels);
            pg.MapEnum<RequirementKind>("lt_requirement_kind", nameTranslator: labels);
            pg.MapEnum<Validation>("lt_validation", nameTranslator: labels);
            pg.MapEnum<Freshness>("lt_freshness", nameTranslator: labels);
            pg.MapEnum<ResponseRole>("lt_response_role", nameTranslator: labels);
            pg.MapEnum<ResponseKind>("lt_response_kind", nameTranslator: labels);
            pg.MapEnum<Attendance>("lt_attendance", nameTranslator: labels);
            pg.MapEnum<FormalStatus>("lt_formal_status", nameTranslator: labels);
            pg.MapEnum<Decision>("lt_decision", nameTranslator: labels);
            pg.MapEnum<ResultStatus>("lt_result_status", nameTranslator: labels);
            pg.MapEnum<InvalidationScope>("lt_invalidation_scope", nameTranslator: labels);
            pg.MapEnum<Outcome>("lt_outcome", nameTranslator: labels);
            pg.MapEnum<EvidenceKind>("lt_evidence_kind", nameTranslator: labels);
            pg.MapEnum<EvidenceStatus>("lt_evidence_status", nameTranslator: labels);
            pg.MapEnum<CheckOutcome>("lt_check_outcome", nameTranslator: labels);
            pg.MapEnum<CheckScope>("lt_check_scope", nameTranslator: labels);
            pg.MapEnum<Channel>("lt_channel", nameTranslator: labels);
        });
}
