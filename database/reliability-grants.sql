\set ON_ERROR_STOP on
GRANT SELECT,INSERT ON lesson_planning_policy,learning_operation,outbox_message,inbox_message,enrollment_source_state,policy_attestation TO v_learning_tracking_runtime;
GRANT SELECT,INSERT ON document_release_decision TO v_learning_tracking_runtime;
GRANT UPDATE ON enrollment_source_state TO v_learning_tracking_runtime;
GRANT SELECT,INSERT,UPDATE ON school_source_state TO v_learning_tracking_runtime;
GRANT DELETE ON program_rule_source,training_topic,assessment_definition_version,assessment_topic,admission_requirement,lesson_planning_policy TO v_learning_tracking_runtime;
GRANT UPDATE(confirmed_by,confirmed_at) ON theory_record_version TO v_learning_tracking_runtime;
GRANT SELECT ON "__EFMigrationsHistory" TO v_learning_tracking_runtime;
DO $$ DECLARE t text; BEGIN
 FOREACH t IN ARRAY ARRAY['lesson_planning_policy','learning_operation','outbox_message','inbox_message','enrollment_source_state','policy_attestation','school_source_state'] LOOP
  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY',t);
  EXECUTE format('CREATE POLICY tenant_scope ON %I TO v_learning_tracking_runtime USING(school_id=nullif(current_setting(''vairas.school_id'',true),'''')::uuid) WITH CHECK(school_id=nullif(current_setting(''vairas.school_id'',true),'''')::uuid)',t);
 END LOOP;
END $$;
GRANT USAGE ON SCHEMA public TO v_learning_tracking_delivery;
DO $$ BEGIN EXECUTE format('GRANT CONNECT ON DATABASE %I TO v_learning_tracking_delivery',current_database()); END $$;
GRANT SELECT,UPDATE ON outbox_message TO v_learning_tracking_delivery;
CREATE POLICY delivery_scope ON outbox_message TO v_learning_tracking_delivery USING(true) WITH CHECK(true);
GRANT SELECT(school_id,lesson_id,last_verified_at) ON lesson_source_state TO v_learning_tracking_delivery;
CREATE POLICY reconciliation_sources ON lesson_source_state FOR SELECT TO v_learning_tracking_delivery USING(true);
GRANT SELECT(id,school_id) ON student_training TO v_learning_tracking_delivery;
CREATE POLICY reconciliation_enrollments ON student_training FOR SELECT TO v_learning_tracking_delivery USING(true);
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
