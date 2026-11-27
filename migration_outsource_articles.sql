-- ============================================================================
-- Outsourced article names (exact item-name match → Outsourcing page)
--
-- Run this on BOTH pos_malir and pos_bukhari, once each. Adds one new table;
-- no existing rows are changed. The Factory app writes names to both branches.
-- ============================================================================

CREATE TABLE IF NOT EXISTS outsource_articles (
    id              INT AUTO_INCREMENT PRIMARY KEY,
    article_name    VARCHAR(200) NOT NULL,
    is_active       TINYINT(1) NOT NULL DEFAULT 1,
    created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_outsource_article (article_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
