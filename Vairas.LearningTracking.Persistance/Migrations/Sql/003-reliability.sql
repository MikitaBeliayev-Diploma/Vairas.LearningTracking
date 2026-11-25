CREATE TABLE lesson_planning_policy (
 id uuid PRIMARY KEY, school_id uuid NOT NULL, program_version_id uuid NOT NULL,
 kind lt_training_kind NOT NULL, lesson_minutes integer NOT NULL CHECK(lesson_minutes>0),
 allow_short_final_lesson boolean NOT NULL, minimum_final_lesson_minutes integer NOT NULL,
 CHECK(minimum_final_lesson_minutes>0 AND minimum_final_lesson_minutes<=lesson_minutes),
 UNIQUE(school_id,program_version_id,kind),
 FOREIGN KEY(program_version_id,school_id) REFERENCES training_program_version(id,school_id) DEFERRABLE
);
CREATE TABLE learning_operation (
 id uuid NOT NULL, school_id uuid NOT NULL, actor_id uuid NOT NULL, fingerprint text NOT NULL,
 command text NOT NULL, result_json text NOT NULL, result_id uuid NOT NULL, applied_at timestamptz NOT NULL,
 PRIMARY KEY(school_id,id)
);
CREATE TABLE outbox_message (
 id uuid PRIMARY KEY, school_id uuid NOT NULL, aggregate_id uuid NOT NULL, aggregate_version bigint NOT NULL CHECK(aggregate_version>0),
 event_type text NOT NULL, payload text NOT NULL, correlation_id text NOT NULL, occurred_at timestamptz NOT NULL,
 sent_at timestamptz, lease_id uuid, lease_until timestamptz, attempts integer NOT NULL CHECK(attempts>=0), next_attempt_at timestamptz NOT NULL,
 CHECK((lease_id IS NULL)=(lease_until IS NULL))
);
CREATE INDEX ix_outbox_message_sent_at_next_attempt_at ON outbox_message(sent_at,next_attempt_at);
CREATE TABLE inbox_message (
 id uuid PRIMARY KEY, school_id uuid NOT NULL, producer text NOT NULL, event_type text NOT NULL,
 aggregate_id uuid NOT NULL, aggregate_version bigint NOT NULL CHECK(aggregate_version>0), fingerprint text NOT NULL, processed_at timestamptz NOT NULL
);
CREATE TABLE enrollment_source_state (
 id uuid PRIMARY KEY, school_id uuid NOT NULL, revision bigint NOT NULL CHECK(revision>0), status text NOT NULL, verified_at timestamptz NOT NULL
);
CREATE TABLE policy_attestation (
 id uuid NOT NULL, school_id uuid NOT NULL, checksum text NOT NULL, attested_at timestamptz NOT NULL,
 PRIMARY KEY(school_id,id)
);
CREATE FUNCTION lt_draft_content_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public AS $$
DECLARE version_id uuid; current_status lt_program_status;
BEGIN
 version_id := CASE WHEN TG_OP='DELETE' THEN OLD.program_version_id ELSE NEW.program_version_id END;
 SELECT status INTO current_status FROM training_program_version WHERE id=version_id FOR SHARE;
 IF current_status IS DISTINCT FROM 'Draft'::lt_program_status THEN RAISE EXCEPTION 'Published curriculum is immutable' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND (OLD.school_id,OLD.program_version_id) IS DISTINCT FROM (NEW.school_id,NEW.program_version_id) THEN
  RAISE EXCEPTION 'Curriculum ownership is immutable' USING ERRCODE='23514';
 END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF;
 RETURN NEW;
END $$;
DO $$ DECLARE t text; BEGIN
 FOREACH t IN ARRAY ARRAY['program_rule_source','training_topic','assessment_definition_version','assessment_topic','admission_requirement','lesson_planning_policy'] LOOP
  EXECUTE format('CREATE TRIGGER lt_frozen_curriculum BEFORE INSERT OR UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION lt_draft_content_guard()',t);
 END LOOP;
END $$;
CREATE FUNCTION lt_program_version_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public AS $$
BEGIN
 IF TG_OP='DELETE' OR OLD.status='Retired' OR OLD.status='Published' AND
    (NEW.status<>'Retired' OR (to_jsonb(OLD)-ARRAY['status','retired_at']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['status','retired_at'])) THEN
  RAISE EXCEPTION 'Published programme is immutable' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER lt_program_version_frozen BEFORE UPDATE OR DELETE ON training_program_version FOR EACH ROW EXECUTE FUNCTION lt_program_version_guard();
CREATE FUNCTION lt_training_binding_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public AS $$
BEGIN
 IF (OLD.id,OLD.school_id,OLD.student_id,OLD.category_code,OLD.created_at) IS DISTINCT FROM (NEW.id,NEW.school_id,NEW.student_id,NEW.category_code,NEW.created_at)
 OR OLD.program_version_id IS NOT NULL AND (OLD.program_version_id,OLD.program_assigned_by,OLD.program_assigned_at) IS DISTINCT FROM (NEW.program_version_id,NEW.program_assigned_by,NEW.program_assigned_at)
 OR NEW.dependency_revision<OLD.dependency_revision THEN
  RAISE EXCEPTION 'Training binding is immutable' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER lt_training_binding BEFORE UPDATE ON student_training FOR EACH ROW EXECUTE FUNCTION lt_training_binding_guard();
CREATE FUNCTION lt_theory_confirmation_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public AS $$
BEGIN
 IF OLD.confirmed_at IS NOT NULL OR NEW.confirmed_at IS NULL OR NEW.confirmed_by IS NULL OR
 (to_jsonb(OLD)-ARRAY['confirmed_by','confirmed_at']) IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['confirmed_by','confirmed_at']) THEN
  RAISE EXCEPTION 'Only first exact theory confirmation is permitted' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER lt_theory_confirmation BEFORE UPDATE ON theory_record_version FOR EACH ROW EXECUTE FUNCTION lt_theory_confirmation_guard();
CREATE FUNCTION lt_evidence_ready_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public AS $$
BEGIN
 IF NEW.status='Ready' AND (
  NOT EXISTS(SELECT 1 FROM training_evidence_check WHERE snapshot_id=NEW.id AND check_code='POLICY' AND outcome='Satisfied') OR
  NOT EXISTS(SELECT 1 FROM training_evidence_check WHERE snapshot_id=NEW.id AND check_code='SOURCES' AND outcome='Satisfied') OR
  EXISTS(SELECT 1 FROM training_evidence_check WHERE snapshot_id=NEW.id AND outcome<>'Satisfied') OR
  NEW.kind='CompletionReport' AND NOT EXISTS(SELECT 1 FROM training_evidence_check WHERE snapshot_id=NEW.id AND check_code='COMPLETION_ASSESSMENTS' AND outcome='Satisfied')
 ) THEN RAISE EXCEPTION 'Ready evidence lacks satisfied checks' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE CONSTRAINT TRIGGER lt_evidence_ready AFTER INSERT ON training_evidence_snapshot DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION lt_evidence_ready_guard();
