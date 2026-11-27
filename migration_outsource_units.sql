-- ============================================================================
-- Per-piece outsourcing status (e.g. "OTM - Trouser x 2": trouser 1 sent to shop,
-- trouser 2 still in progress). The line's item_status stays the shop-facing value
-- and becomes the least-progressed piece; piece history stays factory-side.
--
-- Run on BOTH pos_malir and pos_bukhari, once each, BEFORE deploying the Factory update.
-- Adds two new tables; no existing rows change.
-- ============================================================================

CREATE TABLE IF NOT EXISTS outsource_unit_status (
    order_item_id   INT NOT NULL,
    unit_no         INT NOT NULL,
    status          VARCHAR(30) NOT NULL,
    updated_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (order_item_id, unit_no),
    CONSTRAINT fk_ous_item FOREIGN KEY (order_item_id) REFERENCES order_items(order_item_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS outsource_unit_history (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    order_id        INT NOT NULL,
    order_item_id   INT NOT NULL,
    unit_no         INT NOT NULL,
    status          VARCHAR(30) NOT NULL,
    changed_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    changed_by      VARCHAR(100) NULL,
    INDEX idx_ouh_item (order_item_id),
    CONSTRAINT fk_ouh_item FOREIGN KEY (order_item_id) REFERENCES order_items(order_item_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Outsourcing sizing-book number, per order line. Separate from orders.book_no (the
-- in-house sizing book), so editing one never changes the other. Run once per branch.
ALTER TABLE order_items ADD COLUMN outsource_book_no VARCHAR(30) NULL;
