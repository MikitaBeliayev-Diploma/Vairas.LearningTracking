CREATE TYPE "lt_program_status" AS ENUM (
  'Draft',
  'Published',
  'Retired'
);

CREATE TYPE "lt_training_kind" AS ENUM (
  'Theory',
  'Practical'
);

CREATE TYPE "lt_assessment_stage" AS ENUM (
  'Midterm',
  'Final'
);

CREATE TYPE "lt_requirement_kind" AS ENUM (
  'MinimumTheoryMinutes',
  'MinimumPracticalMinutes',
  'AssessmentPassed',
  'MinimumMark'
);

CREATE TYPE "lt_validation" AS ENUM (
  'Incomplete',
  'Valid',
  'Invalid'
);

CREATE TYPE "lt_freshness" AS ENUM (
  'Current',
  'ReconciliationPending',
  'Unavailable'
);

CREATE TYPE "lt_response_role" AS ENUM (
  'Instructor',
  'Student'
);

CREATE TYPE "lt_response_kind" AS ENUM (
  'Confirm',
  'Dispute'
);

CREATE TYPE "lt_attendance" AS ENUM (
  'Present',
  'Absent'
);

CREATE TYPE "lt_formal_status" AS ENUM (
  'Incomplete',
  'Satisfied',
  'Unverified'
);

CREATE TYPE "lt_decision" AS ENUM (
  'Approved',
  'NotApproved'
);

CREATE TYPE "lt_result_status" AS ENUM (
  'Recorded',
  'ReviewRequired',
  'Annulled'
);

CREATE TYPE "lt_invalidation_scope" AS ENUM (
  'ProspectiveOnly',
  'HistoricalReviewRequired'
);

CREATE TYPE "lt_outcome" AS ENUM (
  'Passed',
  'Failed'
);

CREATE TYPE "lt_evidence_kind" AS ENUM (
  'Journal',
  'CompletionReport'
);

CREATE TYPE "lt_evidence_status" AS ENUM (
  'Ready',
  'Incomplete'
);

CREATE TYPE "lt_check_outcome" AS ENUM (
  'Satisfied',
  'Unsatisfied',
  'Unverified'
);

CREATE TYPE "lt_check_scope" AS ENUM (
  'Snapshot',
  'EvidenceItem'
);

CREATE TYPE "lt_channel" AS ENUM (
  'Web',
  'MCP',
  'Integration',
  'System'
);

CREATE TABLE "training_program" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "category_code" varchar(16) NOT NULL,
  "name" varchar(200) NOT NULL,
  "created_at" timestamptz NOT NULL,
  "revision" bigint NOT NULL,
  CONSTRAINT "program_revision_positive" CHECK (revision > 0)
);

CREATE TABLE "training_program_version" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "program_id" uuid NOT NULL,
  "version" int NOT NULL,
  "status" lt_program_status NOT NULL,
  "pathway_code" varchar(80) NOT NULL,
  "required_theory_minutes" int NOT NULL,
  "required_practical_minutes" int NOT NULL,
  "theory_minutes_per_unit" int NOT NULL,
  "practical_minutes_per_unit" int NOT NULL,
  "effective_from" date NOT NULL,
  "effective_to" date,
  "legal_basis_verified" boolean NOT NULL,
  "created_by" uuid NOT NULL,
  "created_at" timestamptz NOT NULL,
  "published_by" uuid,
  "published_at" timestamptz,
  "retired_at" timestamptz,
  "journal_checklist_version_id" uuid,
  CONSTRAINT "program_version_positive" CHECK (version > 0),
  CONSTRAINT "program_minutes_nonnegative" CHECK (required_theory_minutes >= 0 AND required_practical_minutes >= 0),
  CONSTRAINT "program_conversion_positive" CHECK (theory_minutes_per_unit > 0 AND practical_minutes_per_unit > 0),
  CONSTRAINT "program_effective_period" CHECK (effective_to IS NULL OR effective_to >= effective_from),
  CONSTRAINT "program_publication_shape" CHECK ((status = 'Draft' AND published_at IS NULL AND published_by IS NULL AND retired_at IS NULL) OR (status = 'Published' AND published_at IS NOT NULL AND published_by IS NOT NULL AND retired_at IS NULL AND legal_basis_verified) OR (status = 'Retired' AND published_at IS NOT NULL AND published_by IS NOT NULL AND retired_at IS NOT NULL AND retired_at >= published_at AND legal_basis_verified)),
  CONSTRAINT "published_checklist" CHECK (status = 'Draft' OR journal_checklist_version_id IS NOT NULL)
);

CREATE TABLE "program_rule_source" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "rule_code" varchar(80) NOT NULL,
  "source_uri" text NOT NULL,
  "source_edition" varchar(200) NOT NULL,
  "applicability" text NOT NULL,
  "verified_by" uuid,
  "verified_at" timestamptz,
  "effective_from" date NOT NULL,
  "effective_to" date,
  "unit_description" varchar(120) NOT NULL,
  CONSTRAINT "rule_verification_pair" CHECK ((verified_by IS NULL) = (verified_at IS NULL)),
  CONSTRAINT "rule_effective_period" CHECK (effective_to IS NULL OR effective_to >= effective_from)
);

CREATE TABLE "training_topic" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "topic_key" varchar(80) NOT NULL,
  "name" varchar(200) NOT NULL,
  "description" text
);

CREATE TABLE "assessment_definition" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "program_id" uuid NOT NULL,
  "code" varchar(80) NOT NULL
);

CREATE TABLE "assessment_definition_version" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "definition_id" uuid NOT NULL,
  "program_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "name" varchar(200) NOT NULL,
  "training_kind" lt_training_kind NOT NULL,
  "stage" lt_assessment_stage NOT NULL,
  "minimum_mark" int NOT NULL,
  "maximum_mark" int NOT NULL,
  "passing_mark" int NOT NULL,
  "required_for_completion" boolean NOT NULL,
  CONSTRAINT "assessment_scale" CHECK (minimum_mark < maximum_mark AND passing_mark >= minimum_mark AND passing_mark <= maximum_mark)
);

CREATE TABLE "assessment_topic" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "definition_version_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "topic_id" uuid NOT NULL
);

CREATE TABLE "admission_requirement" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "definition_version_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "code" varchar(80) NOT NULL,
  "kind" lt_requirement_kind NOT NULL,
  "minimum_minutes" int,
  "prerequisite_definition_version_id" uuid,
  "minimum_mark" int,
  CONSTRAINT "requirement_typed_shape" CHECK ((kind IN ('MinimumTheoryMinutes','MinimumPracticalMinutes') AND minimum_minutes IS NOT NULL AND minimum_minutes >= 0 AND prerequisite_definition_version_id IS NULL AND minimum_mark IS NULL) OR (kind = 'AssessmentPassed' AND minimum_minutes IS NULL AND prerequisite_definition_version_id IS NOT NULL AND minimum_mark IS NULL) OR (kind = 'MinimumMark' AND minimum_minutes IS NULL AND prerequisite_definition_version_id IS NOT NULL AND minimum_mark IS NOT NULL))
);

CREATE TABLE "student_training" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "student_id" uuid NOT NULL,
  "category_code" varchar(16) NOT NULL,
  "program_version_id" uuid,
  "program_assigned_by" uuid,
  "program_assigned_at" timestamptz,
  "dependency_revision" bigint NOT NULL,
  "created_at" timestamptz NOT NULL,
  CONSTRAINT "training_dependency_positive" CHECK (dependency_revision > 0),
  CONSTRAINT "program_initial_assignment_pair" CHECK ((program_version_id IS NULL AND program_assigned_by IS NULL AND program_assigned_at IS NULL) OR (program_version_id IS NOT NULL AND program_assigned_by IS NOT NULL AND program_assigned_at IS NOT NULL))
);

CREATE TABLE "lesson_source_snapshot" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "lesson_id" uuid NOT NULL,
  "lesson_revision" int NOT NULL,
  "kind" lt_training_kind NOT NULL,
  "instructor_id" uuid NOT NULL,
  "enrollment_id" uuid,
  "student_id" uuid,
  "vehicle_id" uuid,
  "group_id" uuid,
  "membership_revision" bigint,
  "actual_start" timestamptz,
  "actual_end" timestamptz,
  "physically_completed" boolean NOT NULL,
  "source_valid" boolean NOT NULL,
  "captured_at" timestamptz NOT NULL,
  "source_event_id" uuid,
  CONSTRAINT "source_revision_positive" CHECK (lesson_revision > 0),
  CONSTRAINT "source_actual_interval" CHECK ((actual_start IS NULL AND actual_end IS NULL) OR (actual_start IS NOT NULL AND actual_end IS NOT NULL AND actual_end > actual_start)),
  CONSTRAINT "completed_source_requires_actuals" CHECK (NOT (physically_completed AND source_valid) OR (actual_start IS NOT NULL AND actual_end IS NOT NULL)),
  CONSTRAINT "source_kind_shape" CHECK ((kind = 'Practical' AND enrollment_id IS NOT NULL AND student_id IS NOT NULL AND vehicle_id IS NOT NULL AND group_id IS NULL AND membership_revision IS NULL) OR (kind = 'Theory' AND enrollment_id IS NULL AND student_id IS NULL AND vehicle_id IS NULL AND group_id IS NOT NULL AND membership_revision IS NOT NULL AND membership_revision > 0))
);

CREATE TABLE "lesson_source_state" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "lesson_id" uuid NOT NULL,
  "current_snapshot_id" uuid NOT NULL,
  "freshness" lt_freshness NOT NULL,
  "last_verified_at" timestamptz NOT NULL,
  "reason" text
);

CREATE TABLE "practical_training_record" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "lesson_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "current_version_id" uuid,
  "revision" bigint NOT NULL,
  CONSTRAINT "practical_revision_positive" CHECK (revision > 0)
);

CREATE TABLE "practical_record_version" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "record_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "lesson_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "version" int NOT NULL,
  "source_snapshot_id" uuid NOT NULL,
  "training_minutes" int,
  "duration_explanation" text,
  "distance_km" numeric(10,3),
  "validation" lt_validation NOT NULL,
  "validation_reason" text,
  "created_by" uuid NOT NULL,
  "created_at" timestamptz NOT NULL,
  "correction_reason" text,
  CONSTRAINT "practical_version_positive" CHECK (version > 0),
  CONSTRAINT "practical_minutes" CHECK (training_minutes IS NULL OR training_minutes > 0),
  CONSTRAINT "practical_distance" CHECK (distance_km IS NULL OR distance_km >= 0),
  CONSTRAINT "practical_valid_shape" CHECK (validation <> 'Valid' OR (training_minutes IS NOT NULL AND distance_km IS NOT NULL)),
  CONSTRAINT "practical_correction_reason" CHECK (version = 1 OR (correction_reason IS NOT NULL AND length(trim(correction_reason)) > 0))
);

CREATE TABLE "practical_record_topic" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "record_version_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "topic_id" uuid NOT NULL
);

CREATE TABLE "practical_record_response" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "record_id" uuid NOT NULL,
  "record_version_id" uuid NOT NULL,
  "sequence" bigint NOT NULL,
  "actor_id" uuid NOT NULL,
  "actor_role" lt_response_role NOT NULL,
  "response" lt_response_kind NOT NULL,
  "responded_at" timestamptz NOT NULL,
  "reason" text,
  "command_id" uuid NOT NULL,
  CONSTRAINT "response_sequence" CHECK (sequence > 0),
  CONSTRAINT "dispute_reason" CHECK (response <> 'Dispute' OR (reason IS NOT NULL AND length(trim(reason)) > 0))
);

CREATE TABLE "theory_training_record" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "lesson_id" uuid NOT NULL,
  "current_version_id" uuid,
  "revision" bigint NOT NULL,
  CONSTRAINT "theory_revision_positive" CHECK (revision > 0)
);

CREATE TABLE "theory_record_version" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "record_id" uuid NOT NULL,
  "lesson_id" uuid NOT NULL,
  "version" int NOT NULL,
  "source_snapshot_id" uuid NOT NULL,
  "training_minutes" int NOT NULL,
  "duration_explanation" text,
  "validation" lt_validation NOT NULL,
  "created_by" uuid NOT NULL,
  "created_at" timestamptz NOT NULL,
  "confirmed_by" uuid,
  "confirmed_at" timestamptz,
  "correction_reason" text,
  CONSTRAINT "theory_positive" CHECK (version > 0 AND training_minutes > 0),
  CONSTRAINT "theory_confirmation_pair" CHECK ((confirmed_by IS NULL) = (confirmed_at IS NULL)),
  CONSTRAINT "theory_confirmed_valid" CHECK (confirmed_at IS NULL OR validation = 'Valid'),
  CONSTRAINT "theory_correction_reason" CHECK (version = 1 OR (correction_reason IS NOT NULL AND length(trim(correction_reason)) > 0))
);

CREATE TABLE "theory_attendance" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "record_version_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "student_id" uuid NOT NULL,
  "attendance" lt_attendance NOT NULL
);

CREATE TABLE "assessment_readiness" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "definition_version_id" uuid NOT NULL,
  "current_evaluation_id" uuid,
  "current_decision_id" uuid,
  "revision" bigint NOT NULL,
  CONSTRAINT "readiness_revision_positive" CHECK (revision > 0)
);

CREATE TABLE "readiness_evaluation" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "readiness_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "definition_version_id" uuid NOT NULL,
  "dependency_revision" bigint NOT NULL,
  "formal_status" lt_formal_status NOT NULL,
  "evaluated_at" timestamptz NOT NULL,
  "evaluated_by" uuid NOT NULL,
  "explanation" text NOT NULL,
  CONSTRAINT "evaluation_dependency_positive" CHECK (dependency_revision > 0)
);

CREATE TABLE "readiness_requirement_result" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "evaluation_id" uuid NOT NULL,
  "definition_version_id" uuid NOT NULL,
  "requirement_id" uuid NOT NULL,
  "status" lt_formal_status NOT NULL,
  "observed_minutes" int,
  "observed_mark" int,
  "explanation" text NOT NULL,
  CONSTRAINT "observed_minutes_nonnegative" CHECK (observed_minutes IS NULL OR observed_minutes >= 0)
);

CREATE TABLE "readiness_evidence_basis" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "evaluation_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "lesson_id" uuid,
  "practical_record_version_id" uuid,
  "theory_attendance_id" uuid,
  "result_version_id" uuid,
  "observed_minutes" int,
  CONSTRAINT "readiness_evidence_source_xor" CHECK (((practical_record_version_id IS NOT NULL)::int + (theory_attendance_id IS NOT NULL)::int + (result_version_id IS NOT NULL)::int) = 1),
  CONSTRAINT "readiness_evidence_typed_shape" CHECK (((practical_record_version_id IS NOT NULL OR theory_attendance_id IS NOT NULL) AND lesson_id IS NOT NULL AND observed_minutes IS NOT NULL AND observed_minutes > 0) OR (result_version_id IS NOT NULL AND lesson_id IS NULL AND observed_minutes IS NULL))
);

CREATE TABLE "readiness_decision" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "readiness_id" uuid NOT NULL,
  "evaluation_id" uuid NOT NULL,
  "actor_id" uuid NOT NULL,
  "decided_at" timestamptz NOT NULL,
  "decision" lt_decision NOT NULL,
  "reason" text NOT NULL,
  "authority_basis" text NOT NULL,
  "command_id" uuid NOT NULL,
  "invalidated_at" timestamptz,
  "invalidated_by" uuid,
  "invalidation_reason" text,
  "invalidation_dependency_revision" bigint,
  "invalidation_scope" lt_invalidation_scope,
  "invalidation_effective_at" timestamptz,
  "enrollment_id" uuid NOT NULL,
  CONSTRAINT "decline_reason" CHECK (decision <> 'NotApproved' OR length(trim(reason)) > 0),
  CONSTRAINT "decision_terminal_invalidation_shape" CHECK ((invalidated_at IS NULL AND invalidated_by IS NULL AND invalidation_reason IS NULL AND invalidation_dependency_revision IS NULL AND invalidation_scope IS NULL AND invalidation_effective_at IS NULL) OR (decision = 'Approved' AND invalidated_at IS NOT NULL AND invalidated_by IS NOT NULL AND invalidation_reason IS NOT NULL AND length(trim(invalidation_reason)) > 0 AND invalidation_dependency_revision IS NOT NULL AND invalidation_dependency_revision > 0 AND invalidated_at >= decided_at AND invalidation_scope IS NOT NULL AND (invalidation_scope = 'HistoricalReviewRequired' OR (invalidation_scope = 'ProspectiveOnly' AND invalidation_effective_at IS NOT NULL AND invalidation_effective_at >= decided_at))))
);

CREATE TABLE "assessment" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "definition_version_id" uuid NOT NULL,
  "attempt_number" int NOT NULL,
  "appointment_id" uuid,
  "revision" bigint NOT NULL,
  CONSTRAINT "assessment_attempt_positive" CHECK (attempt_number > 0 AND revision > 0)
);

CREATE TABLE "assessment_result_version" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "assessment_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "definition_version_id" uuid NOT NULL,
  "version" int NOT NULL,
  "command_id" uuid NOT NULL,
  "status" lt_result_status NOT NULL,
  "assessor_id" uuid NOT NULL,
  "assessed_at" timestamptz NOT NULL,
  "recorded_by" uuid NOT NULL,
  "recorded_at" timestamptz NOT NULL,
  "mark" int NOT NULL,
  "outcome" lt_outcome NOT NULL,
  "readiness_id" uuid,
  "readiness_decision_id" uuid,
  "comment" text,
  "correction_reason" text,
  "reviewed_invalidation_at" timestamptz,
  CONSTRAINT "assessment_result_version_positive" CHECK (version > 0),
  CONSTRAINT "assessment_result_time" CHECK (recorded_at >= assessed_at),
  CONSTRAINT "assessment_result_reason" CHECK ((version = 1 AND status = 'Recorded' AND correction_reason IS NULL) OR (version > 1 AND correction_reason IS NOT NULL AND length(trim(correction_reason)) > 0)),
  CONSTRAINT "result_readiness_pair" CHECK ((readiness_id IS NULL) = (readiness_decision_id IS NULL)),
  CONSTRAINT "result_review_pending_shape" CHECK ((status = 'ReviewRequired' AND version > 1 AND reviewed_invalidation_at IS NOT NULL) OR (status <> 'ReviewRequired')),
  CONSTRAINT "result_review_timestamp" CHECK (reviewed_invalidation_at IS NULL OR (version > 1 AND recorded_at >= reviewed_invalidation_at)),
  CONSTRAINT "result_review_requires_decision" CHECK (reviewed_invalidation_at IS NULL OR readiness_decision_id IS NOT NULL)
);

CREATE TABLE "assessment_topic_result" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "result_version_id" uuid NOT NULL,
  "definition_version_id" uuid NOT NULL,
  "assessment_topic_id" uuid NOT NULL,
  "mark" int NOT NULL,
  "comment" text
);

CREATE TABLE "training_evidence_snapshot" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "program_version_id" uuid NOT NULL,
  "kind" lt_evidence_kind NOT NULL,
  "status" lt_evidence_status NOT NULL,
  "checklist_version_id" uuid,
  "dependency_revision" bigint NOT NULL,
  "prepared_by" uuid NOT NULL,
  "prepared_at" timestamptz NOT NULL,
  "source_verified_at" timestamptz,
  CONSTRAINT "evidence_dependency_positive" CHECK (dependency_revision > 0),
  CONSTRAINT "ready_evidence_basis" CHECK (status <> 'Ready' OR (checklist_version_id IS NOT NULL AND source_verified_at IS NOT NULL))
);

CREATE TABLE "training_evidence_item" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "snapshot_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "lesson_id" uuid,
  "practical_record_version_id" uuid,
  "theory_attendance_id" uuid,
  "result_version_id" uuid,
  "readiness_decision_id" uuid,
  "record_response_sequence" bigint,
  "credited_minutes_at_snapshot" int,
  "observed_state" varchar(80) NOT NULL,
  "explanation" text NOT NULL,
  CONSTRAINT "evidence_item_one_source" CHECK ((practical_record_version_id IS NOT NULL)::int + (theory_attendance_id IS NOT NULL)::int + (result_version_id IS NOT NULL)::int + (readiness_decision_id IS NOT NULL)::int = 1),
  CONSTRAINT "evidence_item_lesson_shape" CHECK (((practical_record_version_id IS NOT NULL OR theory_attendance_id IS NOT NULL) AND lesson_id IS NOT NULL AND credited_minutes_at_snapshot IS NOT NULL) OR ((practical_record_version_id IS NULL AND theory_attendance_id IS NULL) AND lesson_id IS NULL AND credited_minutes_at_snapshot IS NULL)),
  CONSTRAINT "evidence_item_minutes_nonnegative" CHECK (credited_minutes_at_snapshot IS NULL OR credited_minutes_at_snapshot >= 0),
  CONSTRAINT "evidence_response_shape" CHECK (record_response_sequence IS NULL OR (practical_record_version_id IS NOT NULL AND record_response_sequence >= 0))
);

CREATE TABLE "training_evidence_check" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "snapshot_id" uuid NOT NULL,
  "enrollment_id" uuid NOT NULL,
  "check_code" varchar(100) NOT NULL,
  "check_scope" lt_check_scope NOT NULL,
  "evidence_item_id" uuid,
  "field_code" varchar(100) NOT NULL,
  "outcome" lt_check_outcome NOT NULL,
  "explanation" text NOT NULL,
  CONSTRAINT "evidence_check_scope_shape" CHECK ((check_scope = 'Snapshot' AND evidence_item_id IS NULL) OR (check_scope = 'EvidenceItem' AND evidence_item_id IS NOT NULL)),
  CONSTRAINT "evidence_check_codes_nonblank" CHECK (length(trim(check_code)) > 0 AND length(trim(field_code)) > 0),
  CONSTRAINT "evidence_check_explanation_nonblank" CHECK (length(trim(explanation)) > 0)
);

CREATE TABLE "reporting_declaration" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "snapshot_id" uuid NOT NULL,
  "actor_id" uuid NOT NULL,
  "declared_at" timestamptz NOT NULL,
  "statement" text NOT NULL,
  "command_id" uuid NOT NULL
);

CREATE TABLE "learning_audit_event" (
  "id" uuid PRIMARY KEY,
  "school_id" uuid NOT NULL,
  "actor_id" uuid NOT NULL,
  "occurred_at" timestamptz NOT NULL,
  "channel" lt_channel NOT NULL,
  "action" varchar(100) NOT NULL,
  "target_type" varchar(80) NOT NULL,
  "target_id" uuid NOT NULL,
  "target_version" bigint,
  "outcome" varchar(80) NOT NULL,
  "correlation_id" uuid NOT NULL,
  "command_id" uuid,
  "reason" text,
  "source_snapshot_id" uuid,
  "dependency_revision" bigint
);

CREATE UNIQUE INDEX ON "training_program" ("id", "school_id");

CREATE UNIQUE INDEX ON "training_program" ("id", "school_id", "category_code");

CREATE UNIQUE INDEX ON "training_program_version" ("id", "school_id");

CREATE UNIQUE INDEX ON "training_program_version" ("program_id", "version", "school_id");

CREATE UNIQUE INDEX ON "training_program_version" ("id", "school_id", "program_id");

CREATE UNIQUE INDEX ON "program_rule_source" ("id", "school_id");

CREATE UNIQUE INDEX ON "training_topic" ("id", "school_id");

CREATE UNIQUE INDEX ON "training_topic" ("program_version_id", "topic_key", "school_id");

CREATE UNIQUE INDEX ON "training_topic" ("id", "school_id", "program_version_id");

CREATE UNIQUE INDEX ON "assessment_definition" ("id", "school_id");

CREATE UNIQUE INDEX ON "assessment_definition" ("program_id", "code", "school_id");

CREATE UNIQUE INDEX ON "assessment_definition" ("id", "school_id", "program_id");

CREATE UNIQUE INDEX ON "assessment_definition_version" ("id", "school_id");

CREATE UNIQUE INDEX ON "assessment_definition_version" ("definition_id", "program_version_id", "school_id");

CREATE UNIQUE INDEX ON "assessment_definition_version" ("id", "school_id", "program_version_id");

CREATE UNIQUE INDEX ON "assessment_definition_version" ("id", "school_id", "definition_id");

CREATE UNIQUE INDEX ON "assessment_topic" ("id", "school_id");

CREATE UNIQUE INDEX ON "assessment_topic" ("definition_version_id", "topic_id", "school_id");

CREATE UNIQUE INDEX ON "assessment_topic" ("id", "school_id", "definition_version_id");

CREATE UNIQUE INDEX ON "admission_requirement" ("id", "school_id");

CREATE UNIQUE INDEX ON "admission_requirement" ("definition_version_id", "code", "school_id");

CREATE UNIQUE INDEX ON "admission_requirement" ("id", "school_id", "definition_version_id");

CREATE UNIQUE INDEX ON "student_training" ("id", "school_id");

CREATE UNIQUE INDEX ON "student_training" ("id", "school_id", "category_code");

CREATE UNIQUE INDEX ON "student_training" ("id", "school_id", "student_id");

CREATE UNIQUE INDEX ON "student_training" ("id", "school_id", "program_version_id");

CREATE UNIQUE INDEX ON "lesson_source_snapshot" ("id", "school_id");

CREATE UNIQUE INDEX ON "lesson_source_snapshot" ("lesson_id", "lesson_revision", "school_id");

CREATE UNIQUE INDEX ON "lesson_source_snapshot" ("id", "school_id", "lesson_id");

CREATE UNIQUE INDEX ON "lesson_source_state" ("id", "school_id");

CREATE UNIQUE INDEX ON "lesson_source_state" ("lesson_id", "school_id");

CREATE UNIQUE INDEX ON "practical_training_record" ("id", "school_id");

CREATE UNIQUE INDEX ON "practical_training_record" ("lesson_id", "enrollment_id", "school_id");

CREATE UNIQUE INDEX ON "practical_training_record" ("id", "school_id", "enrollment_id", "lesson_id");

CREATE INDEX "idx_practical_by_enrollment" ON "practical_training_record" ("school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "practical_record_version" ("id", "school_id");

CREATE UNIQUE INDEX ON "practical_record_version" ("record_id", "version", "school_id");

CREATE UNIQUE INDEX ON "practical_record_version" ("id", "school_id", "record_id");

CREATE UNIQUE INDEX ON "practical_record_version" ("id", "school_id", "program_version_id");

CREATE UNIQUE INDEX ON "practical_record_version" ("id", "school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "practical_record_topic" ("id", "school_id");

CREATE UNIQUE INDEX ON "practical_record_topic" ("record_version_id", "topic_id", "school_id");

CREATE UNIQUE INDEX ON "practical_record_response" ("id", "school_id");

CREATE UNIQUE INDEX ON "practical_record_response" ("record_id", "sequence", "school_id");

CREATE UNIQUE INDEX ON "practical_record_response" ("command_id", "school_id");

CREATE UNIQUE INDEX ON "practical_record_response" ("id", "school_id", "record_version_id");

CREATE INDEX "idx_practical_response_eligibility" ON "practical_record_response" ("record_version_id", "actor_role", "response");

CREATE UNIQUE INDEX ON "theory_training_record" ("id", "school_id");

CREATE UNIQUE INDEX ON "theory_training_record" ("lesson_id", "school_id");

CREATE UNIQUE INDEX ON "theory_training_record" ("id", "school_id", "lesson_id");

CREATE UNIQUE INDEX ON "theory_record_version" ("id", "school_id");

CREATE UNIQUE INDEX ON "theory_record_version" ("record_id", "version", "school_id");

CREATE UNIQUE INDEX ON "theory_record_version" ("id", "school_id", "record_id");

CREATE UNIQUE INDEX ON "theory_record_version" ("id", "school_id", "lesson_id");

CREATE UNIQUE INDEX ON "theory_attendance" ("id", "school_id");

CREATE UNIQUE INDEX ON "theory_attendance" ("id", "school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "theory_attendance" ("record_version_id", "enrollment_id", "school_id");

CREATE INDEX "idx_theory_attendance_by_enrollment" ON "theory_attendance" ("school_id", "enrollment_id", "record_version_id");

CREATE UNIQUE INDEX ON "assessment_readiness" ("id", "school_id");

CREATE UNIQUE INDEX ON "assessment_readiness" ("enrollment_id", "program_version_id", "definition_version_id", "school_id");

CREATE UNIQUE INDEX ON "assessment_readiness" ("id", "school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "assessment_readiness" ("id", "school_id", "definition_version_id");

CREATE UNIQUE INDEX ON "assessment_readiness" ("id", "school_id", "enrollment_id", "definition_version_id");

CREATE UNIQUE INDEX ON "readiness_evaluation" ("id", "school_id");

CREATE UNIQUE INDEX ON "readiness_evaluation" ("id", "school_id", "readiness_id");

CREATE UNIQUE INDEX ON "readiness_evaluation" ("id", "school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "readiness_evaluation" ("id", "school_id", "definition_version_id");

CREATE UNIQUE INDEX ON "readiness_requirement_result" ("id", "school_id");

CREATE UNIQUE INDEX ON "readiness_requirement_result" ("evaluation_id", "requirement_id", "school_id");

CREATE UNIQUE INDEX ON "readiness_evidence_basis" ("id", "school_id");

CREATE UNIQUE INDEX ON "readiness_evidence_basis" ("evaluation_id", "lesson_id", "school_id");

CREATE UNIQUE INDEX ON "readiness_evidence_basis" ("evaluation_id", "result_version_id", "school_id");

CREATE INDEX "idx_readiness_evidence_scope" ON "readiness_evidence_basis" ("school_id", "enrollment_id", "evaluation_id");

CREATE UNIQUE INDEX ON "readiness_decision" ("id", "school_id");

CREATE UNIQUE INDEX ON "readiness_decision" ("command_id", "school_id");

CREATE UNIQUE INDEX ON "readiness_decision" ("id", "school_id", "readiness_id");

CREATE UNIQUE INDEX ON "readiness_decision" ("id", "school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "assessment" ("id", "school_id");

CREATE UNIQUE INDEX ON "assessment" ("enrollment_id", "definition_version_id", "attempt_number", "school_id");

CREATE UNIQUE INDEX ON "assessment" ("id", "school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "assessment" ("id", "school_id", "definition_version_id");

CREATE UNIQUE INDEX ON "assessment" ("id", "school_id", "enrollment_id", "definition_version_id");

CREATE UNIQUE INDEX ON "assessment_result_version" ("id", "school_id");

CREATE UNIQUE INDEX ON "assessment_result_version" ("assessment_id", "version", "school_id");

CREATE UNIQUE INDEX ON "assessment_result_version" ("command_id", "school_id");

CREATE UNIQUE INDEX ON "assessment_result_version" ("id", "school_id", "assessment_id");

CREATE UNIQUE INDEX ON "assessment_result_version" ("id", "school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "assessment_result_version" ("id", "school_id", "definition_version_id");

CREATE UNIQUE INDEX ON "assessment_topic_result" ("id", "school_id");

CREATE UNIQUE INDEX ON "assessment_topic_result" ("result_version_id", "assessment_topic_id", "school_id");

CREATE UNIQUE INDEX ON "training_evidence_snapshot" ("id", "school_id");

CREATE UNIQUE INDEX ON "training_evidence_snapshot" ("id", "school_id", "enrollment_id");

CREATE UNIQUE INDEX ON "training_evidence_item" ("id", "school_id");

CREATE UNIQUE INDEX ON "training_evidence_item" ("id", "school_id", "snapshot_id");

CREATE UNIQUE INDEX ON "training_evidence_item" ("snapshot_id", "lesson_id", "school_id");

CREATE UNIQUE INDEX ON "training_evidence_check" ("id", "school_id");

CREATE INDEX "idx_check_snapshot_tenant" ON "training_evidence_check" ("snapshot_id", "school_id", "enrollment_id");

CREATE INDEX "idx_check_item_target" ON "training_evidence_check" ("evidence_item_id", "school_id", "snapshot_id");

CREATE UNIQUE INDEX ON "reporting_declaration" ("id", "school_id");

CREATE UNIQUE INDEX ON "reporting_declaration" ("command_id", "school_id");

CREATE UNIQUE INDEX ON "learning_audit_event" ("id", "school_id");



































































ALTER TABLE "training_program_version" ADD FOREIGN KEY ("program_id", "school_id") REFERENCES "training_program" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "program_rule_source" ADD FOREIGN KEY ("program_version_id", "school_id") REFERENCES "training_program_version" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_topic" ADD FOREIGN KEY ("program_version_id", "school_id") REFERENCES "training_program_version" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_definition" ADD FOREIGN KEY ("program_id", "school_id") REFERENCES "training_program" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_definition_version" ADD FOREIGN KEY ("definition_id", "school_id", "program_id") REFERENCES "assessment_definition" ("id", "school_id", "program_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_definition_version" ADD FOREIGN KEY ("program_version_id", "school_id", "program_id") REFERENCES "training_program_version" ("id", "school_id", "program_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_topic" ADD FOREIGN KEY ("definition_version_id", "school_id", "program_version_id") REFERENCES "assessment_definition_version" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_topic" ADD FOREIGN KEY ("topic_id", "school_id", "program_version_id") REFERENCES "training_topic" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "admission_requirement" ADD FOREIGN KEY ("definition_version_id", "school_id", "program_version_id") REFERENCES "assessment_definition_version" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "admission_requirement" ADD FOREIGN KEY ("prerequisite_definition_version_id", "school_id", "program_version_id") REFERENCES "assessment_definition_version" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "student_training" ADD FOREIGN KEY ("program_version_id", "school_id") REFERENCES "training_program_version" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "lesson_source_state" ADD FOREIGN KEY ("current_snapshot_id", "school_id", "lesson_id") REFERENCES "lesson_source_snapshot" ("id", "school_id", "lesson_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "practical_training_record" ADD FOREIGN KEY ("enrollment_id", "school_id") REFERENCES "student_training" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "practical_record_version" ADD FOREIGN KEY ("record_id", "school_id", "enrollment_id", "lesson_id") REFERENCES "practical_training_record" ("id", "school_id", "enrollment_id", "lesson_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "practical_record_version" ADD FOREIGN KEY ("enrollment_id", "school_id", "program_version_id") REFERENCES "student_training" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "practical_record_version" ADD FOREIGN KEY ("source_snapshot_id", "school_id", "lesson_id") REFERENCES "lesson_source_snapshot" ("id", "school_id", "lesson_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "practical_training_record" ADD FOREIGN KEY ("current_version_id", "school_id", "id") REFERENCES "practical_record_version" ("id", "school_id", "record_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "practical_record_topic" ADD FOREIGN KEY ("record_version_id", "school_id", "program_version_id") REFERENCES "practical_record_version" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "practical_record_topic" ADD FOREIGN KEY ("topic_id", "school_id", "program_version_id") REFERENCES "training_topic" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "practical_record_response" ADD FOREIGN KEY ("record_version_id", "school_id", "record_id") REFERENCES "practical_record_version" ("id", "school_id", "record_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "theory_record_version" ADD FOREIGN KEY ("record_id", "school_id", "lesson_id") REFERENCES "theory_training_record" ("id", "school_id", "lesson_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "theory_record_version" ADD FOREIGN KEY ("source_snapshot_id", "school_id", "lesson_id") REFERENCES "lesson_source_snapshot" ("id", "school_id", "lesson_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "theory_training_record" ADD FOREIGN KEY ("current_version_id", "school_id", "id") REFERENCES "theory_record_version" ("id", "school_id", "record_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "theory_attendance" ADD FOREIGN KEY ("record_version_id", "school_id") REFERENCES "theory_record_version" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "theory_attendance" ADD FOREIGN KEY ("enrollment_id", "school_id", "student_id") REFERENCES "student_training" ("id", "school_id", "student_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_readiness" ADD FOREIGN KEY ("enrollment_id", "school_id", "program_version_id") REFERENCES "student_training" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_readiness" ADD FOREIGN KEY ("definition_version_id", "school_id", "program_version_id") REFERENCES "assessment_definition_version" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_evaluation" ADD FOREIGN KEY ("readiness_id", "school_id", "enrollment_id") REFERENCES "assessment_readiness" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_evaluation" ADD FOREIGN KEY ("readiness_id", "school_id", "definition_version_id") REFERENCES "assessment_readiness" ("id", "school_id", "definition_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_readiness" ADD FOREIGN KEY ("current_evaluation_id", "school_id", "id") REFERENCES "readiness_evaluation" ("id", "school_id", "readiness_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_requirement_result" ADD FOREIGN KEY ("evaluation_id", "school_id", "definition_version_id") REFERENCES "readiness_evaluation" ("id", "school_id", "definition_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_requirement_result" ADD FOREIGN KEY ("requirement_id", "school_id", "definition_version_id") REFERENCES "admission_requirement" ("id", "school_id", "definition_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_evidence_basis" ADD FOREIGN KEY ("evaluation_id", "school_id", "enrollment_id") REFERENCES "readiness_evaluation" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_evidence_basis" ADD FOREIGN KEY ("practical_record_version_id", "school_id", "enrollment_id") REFERENCES "practical_record_version" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_evidence_basis" ADD FOREIGN KEY ("theory_attendance_id", "school_id", "enrollment_id") REFERENCES "theory_attendance" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_evidence_basis" ADD FOREIGN KEY ("result_version_id", "school_id", "enrollment_id") REFERENCES "assessment_result_version" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_decision" ADD FOREIGN KEY ("evaluation_id", "school_id", "readiness_id") REFERENCES "readiness_evaluation" ("id", "school_id", "readiness_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "readiness_decision" ADD FOREIGN KEY ("evaluation_id", "school_id", "enrollment_id") REFERENCES "readiness_evaluation" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_readiness" ADD FOREIGN KEY ("current_decision_id", "school_id", "id") REFERENCES "readiness_decision" ("id", "school_id", "readiness_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment" ADD FOREIGN KEY ("enrollment_id", "school_id", "program_version_id") REFERENCES "student_training" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment" ADD FOREIGN KEY ("definition_version_id", "school_id", "program_version_id") REFERENCES "assessment_definition_version" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_result_version" ADD FOREIGN KEY ("assessment_id", "school_id", "enrollment_id", "definition_version_id") REFERENCES "assessment" ("id", "school_id", "enrollment_id", "definition_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_result_version" ADD FOREIGN KEY ("readiness_id", "school_id", "enrollment_id", "definition_version_id") REFERENCES "assessment_readiness" ("id", "school_id", "enrollment_id", "definition_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_result_version" ADD FOREIGN KEY ("readiness_decision_id", "school_id", "readiness_id") REFERENCES "readiness_decision" ("id", "school_id", "readiness_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_topic_result" ADD FOREIGN KEY ("result_version_id", "school_id", "definition_version_id") REFERENCES "assessment_result_version" ("id", "school_id", "definition_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "assessment_topic_result" ADD FOREIGN KEY ("assessment_topic_id", "school_id", "definition_version_id") REFERENCES "assessment_topic" ("id", "school_id", "definition_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_evidence_snapshot" ADD FOREIGN KEY ("enrollment_id", "school_id", "program_version_id") REFERENCES "student_training" ("id", "school_id", "program_version_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_evidence_item" ADD FOREIGN KEY ("snapshot_id", "school_id", "enrollment_id") REFERENCES "training_evidence_snapshot" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_evidence_item" ADD FOREIGN KEY ("practical_record_version_id", "school_id", "enrollment_id") REFERENCES "practical_record_version" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_evidence_item" ADD FOREIGN KEY ("result_version_id", "school_id", "enrollment_id") REFERENCES "assessment_result_version" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_evidence_item" ADD FOREIGN KEY ("theory_attendance_id", "school_id", "enrollment_id") REFERENCES "theory_attendance" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_evidence_item" ADD FOREIGN KEY ("readiness_decision_id", "school_id", "enrollment_id") REFERENCES "readiness_decision" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_evidence_check" ADD FOREIGN KEY ("snapshot_id", "school_id", "enrollment_id") REFERENCES "training_evidence_snapshot" ("id", "school_id", "enrollment_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "training_evidence_check" ADD FOREIGN KEY ("evidence_item_id", "school_id", "snapshot_id") REFERENCES "training_evidence_item" ("id", "school_id", "snapshot_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "reporting_declaration" ADD FOREIGN KEY ("snapshot_id", "school_id") REFERENCES "training_evidence_snapshot" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;

ALTER TABLE "learning_audit_event" ADD FOREIGN KEY ("source_snapshot_id", "school_id") REFERENCES "lesson_source_snapshot" ("id", "school_id") DEFERRABLE INITIALLY IMMEDIATE;
