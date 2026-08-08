<?php
// ============================================
// MNL 启动器 - 聊天社区 API（公共频道）
// ============================================
// 功能：频道列表、发送消息、轮询消息、在线用户
// ============================================

require_once __DIR__ . '/db_config.php';

$action = $_GET['action'] ?? '';
$method = $_SERVER['REQUEST_METHOD'];

switch ($method) {
    case 'GET':
        handleGet($action);
        break;
    case 'POST':
        handlePost($action);
        break;
    default:
        jsonResponse(['error' => 'Method not allowed'], 405);
}

function handleGet(string $action): void {
    switch ($action) {
        case 'channels':
            listChannels();
            break;
        case 'messages':
            getMessages();
            break;
        case 'online':
            getOnlineUsers();
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

function handlePost(string $action): void {
    $body = json_decode(file_get_contents('php://input'), true) ?? [];
    switch ($action) {
        case 'send':
            sendMessage($body);
            break;
        case 'create_channel':
            createChannel($body);
            break;
        case 'clear_all':
            clearAll($body);
            break;
        case 'upload_avatar':
            uploadAvatar($body);
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

/**
 * 获取所有聊天频道
 * GET /api/community.php?action=channels
 */
function listChannels(): void {
    $pdo = getDB();
    $stmt = $pdo->query('
        SELECT id, name, description, icon_emoji
        FROM chat_channels
        WHERE is_active = 1
        ORDER BY sort_order DESC, id ASC
    ');
    $channels = $stmt->fetchAll();

    // 获取每个频道的最近一条消息和消息数
    foreach ($channels as &$ch) {
        $stmt2 = $pdo->prepare('
            SELECT COUNT(*) AS msg_count FROM chat_messages WHERE channel_id = ?
        ');
        $stmt2->execute([$ch['id']]);
        $ch['message_count'] = intval($stmt2->fetchColumn());

        $stmt3 = $pdo->prepare('
            SELECT content, sender_id, created_at
            FROM chat_messages WHERE channel_id = ?
            ORDER BY id DESC LIMIT 1
        ');
        $stmt3->execute([$ch['id']]);
        $lastMsg = $stmt3->fetch();
        $ch['last_message'] = $lastMsg ?: null;
    }

    jsonResponse(['channels' => $channels]);
}

/**
 * 获取频道消息
 * GET /api/community.php?action=messages&channel_id=1&since=0&limit=50
 */
function getMessages(): void {
    $channelId = intval($_GET['channel_id'] ?? 0);
    $since     = intval($_GET['since'] ?? 0);
    $limit     = min(100, max(1, intval($_GET['limit'] ?? 50)));

    if ($channelId <= 0) {
        jsonResponse(['error' => 'Missing channel_id'], 400);
    }

    $pdo = getDB();

    // 验证频道存在
    $stmt = $pdo->prepare('SELECT id, name FROM chat_channels WHERE id = ? AND is_active = 1');
    $stmt->execute([$channelId]);
    if (!$stmt->fetch()) {
        jsonResponse(['error' => '频道不存在'], 404);
    }

    if ($since > 0) {
        $stmt = $pdo->prepare('
            SELECT m.id, m.channel_id, m.sender_id, m.title, m.content, m.msg_type, m.created_at,
                   COALESCE(u.nickname, m.sender_id) AS sender_nickname,
                   u.avatar_url AS sender_avatar
            FROM chat_messages m
            LEFT JOIN users u ON m.sender_id = u.qingniao_id
            WHERE m.channel_id = ? AND m.id > ?
            ORDER BY m.id ASC
            LIMIT ?
        ');
        $stmt->execute([$channelId, $since, $limit]);
    } else {
        $stmt = $pdo->prepare('
            SELECT m.id, m.channel_id, m.sender_id, m.title, m.content, m.msg_type, m.created_at,
                   COALESCE(u.nickname, m.sender_id) AS sender_nickname,
                   u.avatar_url AS sender_avatar
            FROM chat_messages m
            LEFT JOIN users u ON m.sender_id = u.qingniao_id
            WHERE m.channel_id = ?
            ORDER BY m.id DESC
            LIMIT ?
        ');
        $stmt->execute([$channelId, $limit]);
        $messages = $stmt->fetchAll();
        $messages = array_reverse($messages); // 倒序变正序

        jsonResponse(['messages' => $messages, 'channel_id' => $channelId]);
        return;
    }

    jsonResponse([
        'messages' => $stmt->fetchAll(),
        'channel_id' => $channelId,
    ]);
}

/**
 * 发送消息到频道
 * POST /api/community.php?action=send
 * Body: { channel_id, sender_id, title, content, qingniao_token（可选: 登录凭证） }
 */
function sendMessage(array $body): void {
    checkRateLimit('community_send');
    $channelId = intval($body['channel_id'] ?? 0);
    $senderId  = trim($body['sender_id'] ?? '');
    $title     = trim($body['title'] ?? '');
    $content   = trim($body['content'] ?? '');

    if ($channelId <= 0 || empty($senderId)) {
        jsonResponse(['error' => 'Missing channel_id or sender_id'], 400);
    }
    if (empty($content)) {
        jsonResponse(['error' => '消息不能为空'], 400);
    }
    if (mb_strlen($content) > 2000) {
        jsonResponse(['error' => '消息长度不能超过2000字'], 400);
    }

    $pdo = getDB();

    // 验证用户是否存在
    $stmt = $pdo->prepare('SELECT qingniao_id, nickname, avatar_url FROM users WHERE qingniao_id = ?');
    $stmt->execute([$senderId]);
    $user = $stmt->fetch();
    if (!$user) {
        jsonResponse(['error' => '请先登录青鸟账号'], 401);
    }

    // 验证 token：要求发送者提供 Bearer token 且 token 对应的用户与 sender_id 匹配
    $authUser = authenticateRequest($pdo);
    if (!$authUser || $authUser['qingniao_id'] !== $senderId) {
        jsonResponse(['error' => '认证失败：请先登录青鸟账号并确保与发送者匹配'], 401);
    }

    // 验证频道存在
    $stmt = $pdo->prepare('SELECT id FROM chat_channels WHERE id = ? AND is_active = 1');
    $stmt->execute([$channelId]);
    if (!$stmt->fetch()) {
        jsonResponse(['error' => '频道不存在'], 404);
    }

    $stmt = $pdo->prepare('
        INSERT INTO chat_messages (channel_id, sender_id, title, content, msg_type)
        VALUES (?, ?, ?, ?, ?)
    ');
    $stmt->execute([$channelId, $senderId, $title, $content, 'Normal']);

    jsonResponse([
        'success' => true,
        'id' => intval($pdo->lastInsertId()),
        'created_at' => date('Y-m-d H:i:s'),
        'sender_nickname' => $user['nickname'] ?? $senderId,
        'sender_avatar' => $user['avatar_url'] ?? '',
    ]);
}

/**
 * 获取在线用户列表
 * GET /api/community.php?action=online
 */
function getOnlineUsers(): void {
    $pdo = getDB();
    $stmt = $pdo->query('
        SELECT uo.user_id, uo.last_heartbeat, uo.launcher_version
        FROM user_online uo
        WHERE uo.last_heartbeat > DATE_SUB(NOW(), INTERVAL 120 SECOND)
        ORDER BY uo.last_heartbeat DESC
        LIMIT 100
    ');
    $onlineUsers = $stmt->fetchAll();

    jsonResponse([
        'online_users' => $onlineUsers,
        'count' => count($onlineUsers),
    ]);
}

/**
 * 创建频道
 * POST /api/community.php?action=create_channel
 * Body: { name, description, icon_emoji, creator_id }
 */
function createChannel(array $body): void {
    checkRateLimit('community_create');
    $name        = trim($body['name'] ?? '');
    $description = trim($body['description'] ?? '');
    $iconEmoji   = trim($body['icon_emoji'] ?? '💬');
    $creatorId   = trim($body['creator_id'] ?? '');

    if (empty($name)) {
        jsonResponse(['error' => '频道名称不能为空'], 400);
    }
    if (mb_strlen($name) > 64) {
        jsonResponse(['error' => '频道名称不能超过64字'], 400);
    }
    if (empty($creatorId)) {
        jsonResponse(['error' => '请先登录青鸟账号后再创建频道'], 401);
    }

    $pdo = getDB();

    // 验证创建者 + token
    $stmt = $pdo->prepare('SELECT qingniao_id FROM users WHERE qingniao_id = ?');
    $stmt->execute([$creatorId]);
    if (!$stmt->fetch()) {
        jsonResponse(['error' => '请先登录青鸟账号'], 401);
    }

    $authUser = authenticateRequest($pdo);
    if (!$authUser || $authUser['qingniao_id'] !== $creatorId) {
        jsonResponse(['error' => '认证失败：请先登录青鸟账号'], 401);
    }

    // 检查名称是否重复
    $stmt = $pdo->prepare('SELECT id FROM chat_channels WHERE name = ?');
    $stmt->execute([$name]);
    if ($stmt->fetch()) {
        jsonResponse(['error' => '频道名称已存在'], 409);
    }

    $stmt = $pdo->prepare('
        INSERT INTO chat_channels (name, description, icon_emoji, sort_order)
        VALUES (?, ?, ?, 0)
    ');
    $stmt->execute([$name, $description, $iconEmoji]);

    jsonResponse([
        'success' => true,
        'id' => intval($pdo->lastInsertId()),
        'name' => $name,
    ]);
}

/**
 * 清空所有频道和帖子（需要管理密钥）
 * POST /api/community.php?action=clear_all
 * Body: { admin_key: "mnl_clear_2024" }
 */
function clearAll(array $body): void {
    checkRateLimit('community_clear');

    $pdo = getDB();

    // 改为 Bearer token + role 权限检测，替代硬编码密钥
    $authUser = authenticateRequest($pdo);
    if (!$authUser || ($authUser['role'] ?? 'user') !== 'admin') {
        jsonResponse(['error' => '无权限执行此操作（需要管理员权限）'], 403);
    }

    try {
        $pdo->beginTransaction();

        // 先删帖子（外键关联）
        $stmt = $pdo->prepare('DELETE FROM chat_messages');
        $stmt->execute();
        $msgCount = $stmt->rowCount();

        // 再删频道
        $stmt = $pdo->prepare('DELETE FROM chat_channels');
        $stmt->execute();
        $chCount = $stmt->rowCount();

        $pdo->commit();

        jsonResponse([
            'success' => true,
            'deleted_messages' => $msgCount,
            'deleted_channels' => $chCount,
        ]);
    } catch (Exception $e) {
        $pdo->rollBack();
        jsonResponse(['error' => '清空失败: ' . $e->getMessage()], 500);
    }
}

/**
 * 上传用户头像
 * POST /api/community.php?action=upload_avatar
 * Body: { qingniao_id: "xxx", avatar_base64: "base64_image_data" }
 */
function uploadAvatar(array $body): void {
    checkRateLimit('community_avatar');
    $qingniaoId   = trim($body['qingniao_id'] ?? '');
    $avatarBase64 = trim($body['avatar_base64'] ?? '');

    if (empty($qingniaoId)) {
        jsonResponse(['error' => '缺少用户ID'], 400);
    }
    if (empty($avatarBase64)) {
        jsonResponse(['error' => '缺少头像数据'], 400);
    }

    // 去掉 data:image/png;base64, 前缀（如果有）
    if (preg_match('/^data:image\/\w+;base64,/', $avatarBase64, $matches)) {
        $avatarBase64 = substr($avatarBase64, strlen($matches[0]));
    }

    // 验证 base64 长度（至少 4KB，最大 512KB）
    if (strlen($avatarBase64) < 4096 || strlen($avatarBase64) > 700000) {
        jsonResponse(['error' => '头像数据大小异常'], 400);
    }

    $pdo = getDB();

    // 验证用户是否存在
    $stmt = $pdo->prepare('SELECT qingniao_id FROM users WHERE qingniao_id = ?');
    $stmt->execute([$qingniaoId]);
    if (!$stmt->fetch()) {
        jsonResponse(['error' => '请先登录青鸟账号'], 401);
    }

    // 验证 token：只能上传自己的头像
    $authUser = authenticateRequest($pdo);
    if (!$authUser || $authUser['qingniao_id'] !== $qingniaoId) {
        jsonResponse(['error' => '认证失败：只能上传自己的头像'], 401);
    }

    $imageData = base64_decode($avatarBase64, true);
    if ($imageData === false) {
        jsonResponse(['error' => '头像数据解码失败'], 400);
    }

    // 验证图片内容：确保解码后是有效图片（防 webshell 上传）
    $imgInfo = @getimagesizefromstring($imageData);
    if (!$imgInfo || !in_array($imgInfo[2], [IMAGETYPE_PNG, IMAGETYPE_JPEG, IMAGETYPE_GIF, IMAGETYPE_WEBP])) {
        jsonResponse(['error' => '头像必须是有效的图片文件（PNG/JPEG/GIF/WebP）'], 400);
    }

    // 保存头像文件
    $avatarsDir = __DIR__ . '/../avatars';
    if (!is_dir($avatarsDir)) {
        mkdir($avatarsDir, 0755, true);
    }

    $fileName = $qingniaoId . '.png';
    $filePath = $avatarsDir . '/' . $fileName;
    if (file_put_contents($filePath, $imageData) === false) {
        jsonResponse(['error' => '头像文件保存失败'], 500);
    }

    // 更新数据库 avatar_url
    $avatarUrl = 'http://85.137.246.87/avatars/' . $fileName . '?t=' . time();
    $stmt = $pdo->prepare('UPDATE users SET avatar_url = ? WHERE qingniao_id = ?');
    $stmt->execute([$avatarUrl, $qingniaoId]);

    jsonResponse([
        'success' => true,
        'avatar_url' => $avatarUrl,
    ]);
}
