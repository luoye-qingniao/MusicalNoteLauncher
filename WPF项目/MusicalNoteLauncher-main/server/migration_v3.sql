-- ============================================
-- MNL 启动器 v3 数据库迁移
-- 添加背景素材库表
-- ============================================

USE `mnl_launcher`;

-- 9. 背景素材表
CREATE TABLE IF NOT EXISTS `backgrounds` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(128) NOT NULL COMMENT '背景名称',
  `type` VARCHAR(16) NOT NULL DEFAULT 'Image' COMMENT '类型: Image / Video',
  `file_name` VARCHAR(256) NOT NULL COMMENT '服务器存储的文件名',
  `file_size` BIGINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '文件大小(字节)',
  `uploader` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '上传者',
  `download_count` INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '下载次数',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否上架',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_type` (`type`),
  INDEX `idx_is_active` (`is_active`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='背景素材库';
