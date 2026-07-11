-- Combined fix: drop and recreate v2-v4 tables
USE mnl_launcher;

DROP TABLE IF EXISTS chat_messages;
DROP TABLE IF EXISTS chat_channels;
DROP TABLE IF EXISTS friend_requests;
DROP TABLE IF EXISTS auth_tokens;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS backgrounds;
DROP TABLE IF EXISTS components;
DROP TABLE IF EXISTS user_online;
DROP TABLE IF EXISTS friend_messages;
DROP TABLE IF EXISTS friends;

-- components
CREATE TABLE IF NOT EXISTS `components` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(128) NOT NULL,
  `category` VARCHAR(32) NOT NULL DEFAULT 'mod',
  `description` TEXT NOT NULL,
  `icon_emoji` VARCHAR(8) NOT NULL DEFAULT '',
  `author` VARCHAR(64) NOT NULL DEFAULT '',
  `download_url` VARCHAR(512) NOT NULL DEFAULT '',
  `rating` DECIMAL(3,1) NOT NULL DEFAULT 0.0,
  `download_count` INT UNSIGNED NOT NULL DEFAULT 0,
  `mc_version` VARCHAR(32) NOT NULL DEFAULT '',
  `file_size` BIGINT UNSIGNED NOT NULL DEFAULT 0,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `sort_order` INT NOT NULL DEFAULT 0,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_category` (`category`),
  INDEX `idx_is_active` (`is_active`),
  INDEX `idx_sort` (`sort_order`, `id`),
  FULLTEXT INDEX `ft_search` (`name`, `description`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `components` (`name`, `category`, `description`, `icon_emoji`, `author`, `rating`, `download_count`, `mc_version`, `sort_order`) VALUES
  ('OptiFine HD Ultra', 'mod', 'Minecraft优化模组', '', 'sp614x', 4.8, 1250000, '1.20.1', 100),
  ('Sodium', 'mod', '现代化渲染优化模组', '', 'jellysquid3', 4.9, 890000, '1.20.4', 99),
  ('Iris Shaders', 'mod', 'Fabric光影加载器', '', 'coderbot', 4.7, 670000, '1.20.4', 98),
  ('JourneyMap', 'mod', '实时地图模组', '', 'techbrew', 4.6, 980000, '1.20.1', 95),
  ('Just Enough Items', 'mod', '物品配方查看模组', '', 'mezz', 4.9, 2100000, '1.20.4', 97),
  ('Better MC', 'modpack', '大型整合包', '', 'LunaPixel', 4.5, 320000, '1.20.1', 90),
  ('All The Mods 9', 'modpack', '400+精选模组', '', 'ATMTeam', 4.6, 280000, '1.20.1', 89),
  ('SEUS PTGI HRR', 'shader', '顶级路径追踪光影', '', 'Sonic Ether', 4.9, 450000, '1.20.4', 85),
  ('Complementary Shaders', 'shader', '综合光影包', '', 'EminGT', 4.8, 520000, '1.20.4', 84),
  ('Faithful 64x', 'texture', '经典高清材质包', '', 'Faithful Team', 4.7, 750000, '1.20.4', 80),
  ('Bare Bones', 'texture', '简约卡通风格材质', '', 'RobotPantaloons', 4.4, 180000, '1.20.4', 78),
  ('Greenfield v0.5', 'map', '1:1还原洛杉矶', '', 'Greenfield Team', 4.9, 210000, '1.20.1', 70),
  ('The Uncensored Library', 'map', '无界图书馆', '', 'ReportersWF', 4.5, 95000, '1.19.4', 68),
  ('SkyFactory 5', 'modpack', '经典空岛生存', '', 'Darkosto', 4.7, 410000, '1.20.1', 88);

-- friends
CREATE TABLE IF NOT EXISTS `friends` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `user_id` VARCHAR(64) NOT NULL,
  `friend_id` VARCHAR(64) NOT NULL,
  `friend_nickname` VARCHAR(64) NOT NULL DEFAULT '',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_user_friend` (`user_id`, `friend_id`),
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_friend_id` (`friend_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `friend_messages` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `sender_id` VARCHAR(64) NOT NULL,
  `receiver_id` VARCHAR(64) NOT NULL,
  `content` TEXT NOT NULL,
  `msg_type` VARCHAR(16) NOT NULL DEFAULT 'Normal',
  `invite_network_name` VARCHAR(128) NOT NULL DEFAULT '',
  `invite_network_secret` VARCHAR(128) NOT NULL DEFAULT '',
  `invite_game_version` VARCHAR(64) NOT NULL DEFAULT '',
  `invite_accepted` TINYINT(1) NOT NULL DEFAULT 0,
  `is_read` TINYINT(1) NOT NULL DEFAULT 0,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_sender` (`sender_id`),
  INDEX `idx_receiver` (`receiver_id`),
  INDEX `idx_receiver_unread` (`receiver_id`, `is_read`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `user_online` (
  `user_id` VARCHAR(64) NOT NULL,
  `last_heartbeat` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `launcher_version` VARCHAR(32) NOT NULL DEFAULT '',
  PRIMARY KEY (`user_id`),
  INDEX `idx_last_heartbeat` (`last_heartbeat`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- backgrounds
CREATE TABLE IF NOT EXISTS `backgrounds` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(128) NOT NULL,
  `type` VARCHAR(16) NOT NULL DEFAULT 'Image',
  `file_name` VARCHAR(256) NOT NULL,
  `file_size` BIGINT UNSIGNED NOT NULL DEFAULT 0,
  `uploader` VARCHAR(64) NOT NULL DEFAULT '',
  `download_count` INT UNSIGNED NOT NULL DEFAULT 0,
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_type` (`type`),
  INDEX `idx_is_active` (`is_active`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- users
CREATE TABLE IF NOT EXISTS `users` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `qingniao_id` VARCHAR(64) NOT NULL,
  `nickname` VARCHAR(64) NOT NULL DEFAULT '',
  `password_hash` VARCHAR(256) NOT NULL,
  `email` VARCHAR(128) NOT NULL DEFAULT '',
  `avatar_url` VARCHAR(512) NOT NULL DEFAULT '',
  `signature` VARCHAR(256) NOT NULL DEFAULT '',
  `role` VARCHAR(16) NOT NULL DEFAULT 'user',
  `is_banned` TINYINT(1) NOT NULL DEFAULT 0,
  `last_login_at` DATETIME NULL,
  `last_login_ip` VARCHAR(45) NOT NULL DEFAULT '',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_qingniao_id` (`qingniao_id`),
  INDEX `idx_email` (`email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `auth_tokens` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `user_id` BIGINT UNSIGNED NOT NULL,
  `token` VARCHAR(128) NOT NULL,
  `client_id` VARCHAR(64) NOT NULL DEFAULT '',
  `expires_at` DATETIME NOT NULL,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_token` (`token`),
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_expires_at` (`expires_at`),
  FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `friend_requests` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `from_user_id` VARCHAR(64) NOT NULL,
  `to_user_id` VARCHAR(64) NOT NULL,
  `message` VARCHAR(256) NOT NULL DEFAULT '',
  `status` VARCHAR(16) NOT NULL DEFAULT 'pending',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_from_to` (`from_user_id`, `to_user_id`),
  INDEX `idx_to_user` (`to_user_id`, `status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- chat_channels
CREATE TABLE IF NOT EXISTS `chat_channels` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(64) NOT NULL,
  `description` VARCHAR(256) NOT NULL DEFAULT '',
  `icon_emoji` VARCHAR(8) NOT NULL DEFAULT '',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1,
  `sort_order` INT NOT NULL DEFAULT 0,
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `chat_channels` (`name`, `description`, `icon_emoji`, `sort_order`) VALUES
  ('综合交流', 'Minecraft 综合讨论区', '', 100),
  ('模组讨论', '模组推荐和使用教程', '', 90),
  ('联机组队', '找人一起联机', '', 80),
  ('建筑分享', '分享你的建筑作品', '', 70),
  ('技术求助', '遇到问题来这儿问', '', 60),
  ('启动器反馈', '对启动器的建议和Bug反馈', '', 50);

CREATE TABLE IF NOT EXISTS `chat_messages` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `channel_id` INT UNSIGNED NOT NULL,
  `sender_id` VARCHAR(64) NOT NULL,
  `content` TEXT NOT NULL,
  `msg_type` VARCHAR(16) NOT NULL DEFAULT 'Normal',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_channel` (`channel_id`, `id`),
  INDEX `idx_sender` (`sender_id`),
  INDEX `idx_created_at` (`created_at`),
  FOREIGN KEY (`channel_id`) REFERENCES `chat_channels`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `whitelist` ADD COLUMN IF NOT EXISTS `qingniao_id` VARCHAR(64) NOT NULL DEFAULT '';

SELECT 'ALL_DONE' AS result;
