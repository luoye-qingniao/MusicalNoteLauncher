-- ============================================
-- MNL 启动器 v2 数据库迁移
-- 添加好友系统和组件商店表
-- ============================================

USE `mnl_launcher`;

-- 5. 好友关系表
CREATE TABLE IF NOT EXISTS `friends` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `user_id` VARCHAR(64) NOT NULL COMMENT '用户青鸟ID',
  `friend_id` VARCHAR(64) NOT NULL COMMENT '好友青鸟ID',
  `friend_nickname` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '好友备注名',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE INDEX `idx_user_friend` (`user_id`, `friend_id`),
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_friend_id` (`friend_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='好友关系';

-- 6. 好友消息表
CREATE TABLE IF NOT EXISTS `friend_messages` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `sender_id` VARCHAR(64) NOT NULL COMMENT '发送者青鸟ID',
  `receiver_id` VARCHAR(64) NOT NULL COMMENT '接收者青鸟ID',
  `content` TEXT NOT NULL COMMENT '消息内容',
  `msg_type` VARCHAR(16) NOT NULL DEFAULT 'Normal' COMMENT '消息类型: Normal/Invite',
  `invite_network_name` VARCHAR(128) NOT NULL DEFAULT '' COMMENT '邀请网络名',
  `invite_network_secret` VARCHAR(128) NOT NULL DEFAULT '' COMMENT '邀请密钥',
  `invite_game_version` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '邀请游戏版本',
  `invite_accepted` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '邀请是否被接受',
  `is_read` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否已读',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_sender` (`sender_id`),
  INDEX `idx_receiver` (`receiver_id`),
  INDEX `idx_receiver_unread` (`receiver_id`, `is_read`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='好友聊天消息';

-- 7. 用户在线状态表
CREATE TABLE IF NOT EXISTS `user_online` (
  `user_id` VARCHAR(64) NOT NULL COMMENT '用户青鸟ID',
  `last_heartbeat` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '最后心跳时间',
  `launcher_version` VARCHAR(32) NOT NULL DEFAULT '' COMMENT '启动器版本',
  PRIMARY KEY (`user_id`),
  INDEX `idx_last_heartbeat` (`last_heartbeat`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='用户在线状态';

-- 8. 组件商店表
CREATE TABLE IF NOT EXISTS `components` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(128) NOT NULL COMMENT '组件名称',
  `category` VARCHAR(32) NOT NULL DEFAULT 'mod' COMMENT '分类: mod/modpack/shader/texture/map',
  `description` TEXT NOT NULL COMMENT '组件描述',
  `icon_emoji` VARCHAR(8) NOT NULL DEFAULT '🧩' COMMENT '图标Emoji',
  `author` VARCHAR(64) NOT NULL DEFAULT '' COMMENT '作者',
  `download_url` VARCHAR(512) NOT NULL DEFAULT '' COMMENT '下载地址',
  `rating` DECIMAL(3,1) NOT NULL DEFAULT 0.0 COMMENT '评分',
  `download_count` INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '下载次数',
  `mc_version` VARCHAR(32) NOT NULL DEFAULT '' COMMENT '适用MC版本',
  `file_size` BIGINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '文件大小(字节)',
  `is_active` TINYINT(1) NOT NULL DEFAULT 1 COMMENT '是否上架',
  `sort_order` INT NOT NULL DEFAULT 0 COMMENT '排序权重',
  `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_category` (`category`),
  INDEX `idx_is_active` (`is_active`),
  INDEX `idx_sort` (`sort_order`, `id`),
  FULLTEXT INDEX `ft_search` (`name`, `description`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  COMMENT='组件商店';

-- 插入测试组件数据
INSERT INTO `components` (`name`, `category`, `description`, `icon_emoji`, `author`, `rating`, `download_count`, `mc_version`, `sort_order`) VALUES
  ('OptiFine HD Ultra', 'mod', '经典Minecraft画质优化模组，支持光影、高清材质和性能调优', '🎨', 'sp614x', 4.8, 1250000, '1.20.1', 100),
  ('Sodium', 'mod', '现代化渲染优化模组，大幅提升帧率，支持Fabric', '⚡', 'jellysquid3', 4.9, 890000, '1.20.4', 99),
  ('Iris Shaders', 'mod', 'Fabric光影加载器，兼容OptiFine光影包', '🌈', 'coderbot', 4.7, 670000, '1.20.4', 98),
  ('JourneyMap', 'mod', '实时地图模组，支持小地图和全屏地图，可标记路径点', '🗺️', 'techbrew', 4.6, 980000, '1.20.1', 95),
  ('Just Enough Items', 'mod', '物品配方查看模组，支持物品列表搜索和合成指南', '📖', 'mezz', 4.9, 2100000, '1.20.4', 97),
  ('Better MC', 'modpack', '经典大型整合包，包含200+模组，完整任务系统和优化配置', '📦', 'LunaPixel', 4.5, 320000, '1.20.1', 90),
  ('All The Mods 9', 'modpack', '全模组整合包最新版，400+精选模组，适合长期游玩', '🎮', 'ATMTeam', 4.6, 280000, '1.20.1', 89),
  ('SEUS PTGI HRR', 'shader', '顶级路径追踪光影，真实光照和反射效果', '✨', 'Sonic Ether', 4.9, 450000, '1.20.4', 85),
  ('Complementary Shaders', 'shader', '综合光影包，画面精美性能友好，支持多种配置预设', '🌅', 'EminGT', 4.8, 520000, '1.20.4', 84),
  ('Faithful 64x', 'texture', '经典高清材质包，保持原版风格提升分辨率至64x', '🖌️', 'Faithful Team', 4.7, 750000, '1.20.4', 80),
  ('Bare Bones', 'texture', '简约卡通风格材质包，清爽干净的视觉体验', '🎯', 'RobotPantaloons', 4.4, 180000, '1.20.4', 78),
  ('Greenfield v0.5', 'map', '1:1还原洛杉矶市建筑地图，精细到每个房间', '🏙️', 'Greenfield Team', 4.9, 210000, '1.20.1', 70),
  ('The Uncensored Library', 'map', 'Reporter无界图书馆，虚拟新闻自由纪念碑', '📚', 'ReportersWF', 4.5, 95000, '1.19.4', 68),
  ('SkyFactory 5', 'modpack', '经典空岛生存整合包，从无到有建设你的天空世界', '🏝️', 'Darkosto', 4.7, 410000, '1.20.1', 88);
