-- ==============================================================================
-- Serving Store — Operational Helper Functions
-- ==============================================================================
-- Provides idempotent version lifecycle operations for the serving store.
-- Run once during bootstrap: psql -f serving-store-helpers.sql
-- ==============================================================================

-- Register a version as loaded into the serving store.
-- Idempotent: updates entity/evidence counts if re-loading.
CREATE OR REPLACE FUNCTION serving_upsert_version_state(
    p_version_id    VARCHAR(64),
    p_entity_count  INTEGER,
    p_evidence_count INTEGER
) RETURNS VOID AS $$
BEGIN
    INSERT INTO serving_version_state (version_id, loaded_at, entity_count, evidence_count, is_active)
    VALUES (p_version_id, NOW(), p_entity_count, p_evidence_count, false)
    ON CONFLICT (version_id)
    DO UPDATE SET
        loaded_at      = NOW(),
        entity_count   = EXCLUDED.entity_count,
        evidence_count = EXCLUDED.evidence_count,
        is_active      = false;
END;
$$ LANGUAGE plpgsql;

-- Activate a version in the serving store.
-- Deactivates any currently active version first.
CREATE OR REPLACE FUNCTION serving_activate_version(
    p_version_id VARCHAR(64)
) RETURNS VOID AS $$
DECLARE
    v_exists BOOLEAN;
BEGIN
    SELECT EXISTS(
        SELECT 1 FROM serving_version_state WHERE version_id = p_version_id
    ) INTO v_exists;

    IF NOT v_exists THEN
        RAISE EXCEPTION 'Version ''%'' has not been loaded into the serving store.', p_version_id;
    END IF;

    -- Deactivate current active
    UPDATE serving_version_state SET is_active = false WHERE is_active = true;

    -- Activate target
    UPDATE serving_version_state SET is_active = true WHERE version_id = p_version_id;
END;
$$ LANGUAGE plpgsql;

-- Rollback the serving store to a prior version.
CREATE OR REPLACE FUNCTION serving_rollback_version(
    p_version_id VARCHAR(64)
) RETURNS TABLE(previous_active VARCHAR(64), new_active VARCHAR(64)) AS $$
DECLARE
    v_previous VARCHAR(64);
BEGIN
    SELECT version_id INTO v_previous
    FROM serving_version_state WHERE is_active = true;

    -- Deactivate current
    UPDATE serving_version_state SET is_active = false WHERE is_active = true;

    -- Activate target
    UPDATE serving_version_state SET is_active = true WHERE version_id = p_version_id;

    previous_active := COALESCE(v_previous, '(none)');
    new_active := p_version_id;
    RETURN NEXT;
END;
$$ LANGUAGE plpgsql;

-- List all loaded versions in the serving store.
CREATE OR REPLACE FUNCTION serving_list_versions()
RETURNS TABLE(
    version_id     VARCHAR(64),
    loaded_at      TIMESTAMP WITH TIME ZONE,
    entity_count   INTEGER,
    evidence_count INTEGER,
    is_active      BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT svs.version_id, svs.loaded_at, svs.entity_count, svs.evidence_count, svs.is_active
    FROM serving_version_state svs
    ORDER BY svs.loaded_at DESC;
END;
$$ LANGUAGE plpgsql;

-- Mark version as active in the dataset_versions registry table.
CREATE OR REPLACE FUNCTION registry_activate_version(
    p_version_id VARCHAR(64)
) RETURNS VOID AS $$
DECLARE
    v_current VARCHAR(64);
BEGIN
    SELECT version_id INTO v_current
    FROM dataset_versions WHERE status = 'Active' LIMIT 1;

    -- Supersede current
    IF v_current IS NOT NULL THEN
        UPDATE dataset_versions
        SET status = 'Superseded',
            updated_at = NOW(),
            replaced_by_version_id = p_version_id
        WHERE version_id = v_current;
    END IF;

    -- Activate target
    UPDATE dataset_versions
    SET status = 'Active',
        updated_at = NOW(),
        previous_active_version_id = v_current
    WHERE version_id = p_version_id;
END;
$$ LANGUAGE plpgsql;
