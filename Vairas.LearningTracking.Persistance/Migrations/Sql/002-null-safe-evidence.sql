CREATE UNIQUE INDEX ux_training_evidence_check_snapshot
 ON training_evidence_check(snapshot_id,school_id,check_code,field_code)
 WHERE evidence_item_id IS NULL;
CREATE UNIQUE INDEX ux_training_evidence_check_item
 ON training_evidence_check(snapshot_id,school_id,check_code,field_code,evidence_item_id)
 WHERE evidence_item_id IS NOT NULL;
