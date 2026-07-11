-- ============================================
-- MNL 启动器 v4 数据库迁移
-- 添加青鸟账号系统
-- ============================================

USE `mnl_launcher`;

-- 10. 青鸟用户账号表
CREATE TABLE IF NOT EXISTS `users` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `qingniao_id` VARCHAR(64) NOT NULL COMMENT '青鸟ID（唯一用户名）',
  `nickname` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '显示昵称',
  `password_hash` VARCHAR(256) NOT NULL COMMENT '密码哈希（password_hash + bcrypt）',
  `email` VARCHAR(128) NOT NULL DEFAULT '' COMMENT '邮箱',
  `avatar_url` VARCHAR(512) NOT NULL DEFAULT '' COMMENT '头像URL',
  `signature` VARCHAR(256) NOT NULL DEFAULT '' COMMENT '个性签名',
  `role` VARCHAR(16) NOT NULL DEFAULT 'user' COMMENT '角色: user / admin',
  `is_banned` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否被封禁',
  `last_login_at` DATETIME NULL COMMENT '最后登录时间',
  `last_login_ip` VARCHAR(45) NOT NULL DEFAULT '' COMMENT '最后登录IP',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_qingniao_id` (`qingniao_id`),
  INDEX `idx_email` (`email`),
  INDEX `idx_role` (`role`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='青鸟用户账号';

-- 11. 登录令牌表（用于 token 认证）
CREATE TABLE IF NOT EXISTS `auth_tokens` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `user_id` BIGINT UNSIGNED NOT NULL COMMENT '关联 users.id',
  `token` VARCHAR(128) NOT NULL COMMENT '认证令牌',
  `client_id` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '客户端标识',
  `expires_at` DATETIME NOT NULL COMMENT '过期时间',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_token` (`token`),
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_expires_at` (`expires_at`),
  FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='认证令牌';

-- 12. 好友申请表（避免直接添加）
CREATE TABLE IF NOT EXISTS `friend_requests` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `from_user_id` VARCHAR(64) NOT NULL COMMENT '发送者青鸟ID',
  `to_user_id` VARCHAR(64) NOT NULL COMMENT '接收者青鸟ID',
  `message` VARCHAR(256) NOT NULL DEFAULT '' COMMENT '申请附言',
  `status` VARCHAR(16) NOT NULL DEFAULT 'pending' COMMENT 'pending / accepted / rejected',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_from_to` (`from_user_id`, `to_user_id`),
  INDEX `idx_to_user` (`to_user_id`, `status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='好友申请';

-- 13. 聊天社区 - 公共聊天频道
CREATE TABLE IF NOT EXISTS `chat_channels` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(64) NOT NULL COMMENT '频道名称',
  `description` VARCHAR(256) NOT NULL DEFAULT '' COMMENT '频道描述',
  `icon_emoji` VARCHAR(8) NOT NULL DEFAULT '' COMMENT '图标Emoji',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否启用',
  `sort_order` INT NOT NULL DEFAULT 0 COMMENT '排序权重',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='聊天频道';

-- 14. 公共聊天消息表
CREATE TABLE IF NOT EXISTS `chat_messages` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `channel_id` INT UNSIGNED NOT NULL COMMENT '频道ID',
  `sender_id` VARCHAR(64) NOT NULL COMMENT '发送者青鸟ID',
  `content` TEXT NOT NULL COMMENT '消息内容',
  `msg_type` VARCHAR(16) NOT NULL DEFAULT 'Normal' COMMENT '消息类型: Normal / System / Image',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_channel` (`channel_id`, `id`),
  INDEX `idx_sender` (`sender_id`),
  INDEX `idx_created_at` (`created_at`),
  FOREIGN KEY (`channel_id`) REFERENCES `chat_channels`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='公共聊天消息';

-- 插入默认频道
INSERT INTO `chat_channels` (`name`, `description`, `icon_emoji`, `sort_order`) VALUES
  ('综合交流', 'Minecraft 综合讨论区，随便聊聊', '💬', 100),
  ('模组讨论', '模组推荐、安装教程、兼容性问题', '🧩', 90),
  ('联机组队', '找人一起联机，发布联机信息', '🎮', 80),
  ('建筑分享', '分享你的建筑作品，交流建筑技巧', '🏗️', 70),
  ('技术求助', '遇到问题来这儿问，大家帮你解决', '❓', 60),
  ('启动器反馈', '对启动器的建议、Bug 反馈', '🔧', 50)
ON DUPLICATE KEY UPDATE `name` = VALUES(`name`);

-- 在 whitelist 中扩展字段兼容青鸟ID
ALTER TABLE `whitelist` 
  ADD COLUMN IF NOT EXISTS `qingniao_id` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '关联的青鸟ID' AFTER `client_id`,
  ADD INDEX IF NOT EXISTS `idx_qingniao_id` (`qingniao_id`);
