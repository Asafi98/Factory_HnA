-- ============================================================================
-- Husna Aijaz Factory app — database migration
--
-- PART 1: run on BOTH pos_malir and pos_bukhari (adds factory tracking to
--         each branch's own orders, right next to the shop-side status).
--
-- PART 2: run ONCE, against a NEW database called factory_hub (create it
--         first — same DB cluster as pos_malir/pos_bukhari, just a third
--         database on it). Holds factory staff login accounts, completely
--         separate from the POS's own `user` table.
-- ============================================================================


-- ───────────────────────────────────────────────────────────────────────────
-- PART 1 — run this block on pos_malir, then again on pos_bukhari
-- ───────────────────────────────────────────────────────────────────────────

-- If this errors with "Duplicate column name" you've already run it — skip and continue.
ALTER TABLE orders ADD COLUMN factory_stage VARCHAR(30) NULL AFTER order_status;

CREATE TABLE IF NOT EXISTS factory_status_history (
    history_id  INT AUTO_INCREMENT PRIMARY KEY,
    order_id    INT NOT NULL,
    stage       VARCHAR(30) NOT NULL,
    changed_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    changed_by  VARCHAR(100) NULL,
    CONSTRAINT fk_factory_history_order FOREIGN KEY (order_id) REFERENCES orders(order_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- ───────────────────────────────────────────────────────────────────────────
-- PART 2 — create the new factory_hub database, then run this block on it.
--
-- In the DigitalOcean console, first run just this one line by itself, then
-- switch the console's database selector to "factory_hub" before running
-- the rest of this block:
--
--   CREATE DATABASE IF NOT EXISTS factory_hub;
--
-- ───────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS factory_users (
    factory_user_id INT AUTO_INCREMENT PRIMARY KEY,
    username        VARCHAR(50) NOT NULL UNIQUE,
    password        VARCHAR(100) NOT NULL,
    full_name       VARCHAR(100) NULL,
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Seed one starter account so you can log in on day one.
-- CHANGE THIS PASSWORD before giving factory staff the URL — this is only
-- a starting point, plain-text, exactly like the POS app's existing login.
INSERT INTO factory_users (username, password, full_name)
SELECT 'factory', 'ChangeMe123!', 'Factory Staff'
WHERE NOT EXISTS (SELECT 1 FROM factory_users WHERE username = 'factory');
