-- ============================================
-- MNL 启动器 - 测试服务器 数据库初始化脚本
-- ============================================

CREATE DATABASE IF NOT EXISTS `mnl_launcher` 
  DEFAULT CHARACTER SET utf8mb4 
  COLLATE utf8mb4_unicode_ci;

USE `mnl_launcher`;

-- 1. 启动日志表
CREATE TABLE IF NOT EXISTS `startup_logs` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `client_id` VARCHAR(64) NOT NULL COMMENT '客户端唯一标识（机器码/用户名hash）',
  `launcher_version` VARCHAR(32) NOT NULL DEFAULT '' COMMENT '启动器版本号',
  `os_version` VARCHAR(128) NOT NULL DEFAULT '' COMMENT '操作系统版本',
  `clr_version` VARCHAR(32) NOT NULL DEFAULT '' COMMENT '.NET CLR 版本',
  `startup_time` DATETIME NOT NULL COMMENT '启动时间',
  `is_success` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '启动是否成功',
  `error_message` TEXT NULL COMMENT '启动失败时的错误信息',
  `ip_address` VARCHAR(45) NOT NULL DEFAULT '' COMMENT '客户端IP',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_client_id` (`client_id`),
  INDEX `idx_startup_time` (`startup_time`),
  INDEX `idx_launcher_version` (`launcher_version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='启动器启动日志';

-- 2. 崩溃日志表
CREATE TABLE IF NOT EXISTS `crash_logs` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `client_id` VARCHAR(64) NOT NULL COMMENT '客户端唯一标识',
  `launcher_version` VARCHAR(32) NOT NULL DEFAULT '' COMMENT '启动器版本号',
  `crash_time` DATETIME NOT NULL COMMENT '崩溃时间',
  `exception_type` VARCHAR(256) NOT NULL DEFAULT '' COMMENT '异常类型',
  `exception_message` TEXT NOT NULL COMMENT '异常消息',
  `stack_trace` MEDIUMTEXT NOT NULL COMMENT '堆栈跟踪',
  `thread_name` VARCHAR(128) NOT NULL DEFAULT '' COMMENT '崩溃线程类型（UI线程/后台线程）',
  `is_terminating` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否导致进程终止',
  `ip_address` VARCHAR(45) NOT NULL DEFAULT '' COMMENT '客户端IP',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_client_id` (`client_id`),
  INDEX `idx_crash_time` (`crash_time`),
  INDEX `idx_exception_type` (`exception_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='启动器崩溃日志';

-- 3. 白名单表
CREATE TABLE IF NOT EXISTS `whitelist` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `username` VARCHAR(64) NOT NULL COMMENT '用户名',
  `client_id` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '客户端唯一标识（可选，留空则仅匹配用户名）',
  `is_enabled` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `remark` VARCHAR(256) NOT NULL DEFAULT '' COMMENT '备注',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_username_client` (`username`, `client_id`),
  INDEX `idx_is_enabled` (`is_enabled`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='白名单';

-- 插入测试白名单
INSERT INTO `whitelist` (`username`, `client_id`, `is_enabled`, `remark`) VALUES
  ('Player', '', 1, '默认离线模式用户'),
  ('TestUser', '', 1, '测试用户');

-- 4. 版本更新表
CREATE TABLE IF NOT EXISTS `launcher_versions` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `version` VARCHAR(32) NOT NULL COMMENT '版本号，如 1.0.1',
  `version_code` INT UNSIGNED NOT NULL DEFAULT 1 COMMENT '版本序号，用于比较',
  `download_url` VARCHAR(512) NOT NULL COMMENT '升级包下载地址',
  `file_hash` VARCHAR(128) NOT NULL DEFAULT '' COMMENT '升级包 SHA256 校验值',
  `file_size` BIGINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '升级包大小（字节）',
  `release_notes` TEXT NOT NULL COMMENT '更新日志（Markdown）',
  `is_forced` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否强制更新',
  `min_launcher_version` VARCHAR(32) NOT NULL DEFAULT '' COMMENT '最低可升级版本',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否当前活跃版本',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_version` (`version`),
  INDEX `idx_is_active` (`is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='启动器版本管理';
