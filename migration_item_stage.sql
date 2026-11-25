-- ============================================================================
-- Item-level factory stage tracking
--
-- Run this on BOTH pos_malir and pos_bukhari.
--
-- This adds per-item stage tracking so each item in an order can be at a
-- different factory stage (e.g., Shirt #1 at "Cutting", Shirt #2 at "Sent to Shop").
-- The existing order-level factory_stage column is kept for backward compat
-- and will reflect the "earliest" stage across all items.
-- ============================================================================

-- New table: one row per stage change per item
CREATE TABLE IF NOT EXISTS factory_item_status (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    order_id        INT NOT NULL,
    order_item_id   INT NOT NULL,
    stage           VARCHAR(30) NOT NULL,
    changed_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    changed_by      VARCHAR(100) NULL,
    CONSTRAINT fk_fis_order FOREIGN KEY (order_id) REFERENCES orders(order_id) ON DELETE CASCADE,
    CONSTRAINT fk_fis_item  FOREIGN KEY (order_item_id) REFERENCES order_items(order_item_id) ON DELETE CASCADE,
    INDEX idx_fis_item (order_item_id),
    INDEX idx_fis_order (order_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Add a column on order_items to hold the current item-level stage (denormalized for fast reads)
ALTER TABLE order_items ADD COLUMN factory_stage VARCHAR(30) NULL;

-- Migrate existing order-level stages to items:
-- For every order that already has a factory_stage, set all its items to that same stage.
UPDATE order_items oi
INNER JOIN orders o ON oi.order_id = o.order_id
SET oi.factory_stage = o.factory_stage
WHERE o.factory_stage IS NOT NULL
  AND o.factory_stage <> ''
  AND oi.factory_stage IS NULL
  AND IFNULL(oi.item_status, '') <> 'Cancelled';

-- Seed factory_item_status history from existing factory_status_history:
-- Copy each order-level history entry to ALL items of that order.
INSERT INTO factory_item_status (order_id, order_item_id, stage, changed_at, changed_by)
SELECT fsh.order_id, oi.order_item_id, fsh.stage, fsh.changed_at, fsh.changed_by
FROM factory_status_history fsh
INNER JOIN order_items oi ON fsh.order_id = oi.order_id
WHERE IFNULL(oi.item_status, '') <> 'Cancelled'
  AND fsh.stage <> ''
ORDER BY fsh.changed_at, fsh.history_id;
