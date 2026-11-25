-- ============================================================================
-- Per-piece factory stages (e.g. "Shirt × 2" → shirt 1 at Cutting, shirt 2 at Packing)
--
-- Run this on BOTH pos_malir and pos_bukhari, once each, after migration_item_stage.sql.
-- No existing rows are changed. A line with no rows here has every piece at
-- order_items.factory_stage, so no backfill is needed.
-- ============================================================================

CREATE TABLE IF NOT EXISTS factory_unit_stage (
    order_item_id   INT NOT NULL,
    unit_no         INT NOT NULL,
    stage           VARCHAR(30) NULL,
    updated_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (order_item_id, unit_no),
    CONSTRAINT fk_fus_item FOREIGN KEY (order_item_id) REFERENCES order_items(order_item_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

ALTER TABLE factory_item_status ADD COLUMN unit_no INT NULL AFTER order_item_id;
