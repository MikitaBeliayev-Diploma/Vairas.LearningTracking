ALTER TABLE school_source_state ADD CONSTRAINT school_source_identity CHECK(id=school_id AND revision>0);
CREATE FUNCTION lt_program_identity_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,public AS $$
BEGIN
 IF (OLD.id,OLD.school_id) IS DISTINCT FROM (NEW.id,NEW.school_id) OR
  OLD.category_code IS DISTINCT FROM NEW.category_code AND EXISTS(SELECT 1 FROM training_program_version WHERE program_id=OLD.id AND status<>'Draft') THEN
  RAISE EXCEPTION 'Published programme identity is immutable' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER lt_program_identity BEFORE UPDATE ON training_program FOR EACH ROW EXECUTE FUNCTION lt_program_identity_guard();
