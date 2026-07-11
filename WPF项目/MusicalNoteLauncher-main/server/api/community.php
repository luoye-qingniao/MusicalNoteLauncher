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
            SELECT m.id, m.channel_id, m.sender_id, m.content, m.msg_type, m.created_at
            FROM chat_messages m
            WHERE m.channel_id = ? AND m.id > ?
            ORDER BY m.id ASC
            LIMIT ?
        ');
        $stmt->execute([$channelId, $since, $limit]);
    } else {
        $stmt = $pdo->prepare('
            SELECT m.id, m.channel_id, m.sender_id, m.content, m.msg_type, m.created_at
            FROM chat_messages m
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
 * Body: { channel_id, sender_id, content }
 */
function sendMessage(array $body): void {
    $channelId = intval($body['channel_id'] ?? 0);
    $senderId  = trim($body['sender_id'] ?? '');
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

    // 验证频道存在
    $stmt = $pdo->prepare('SELECT id FROM chat_channels WHERE id = ? AND is_active = 1');
    $stmt->execute([$channelId]);
    if (!$stmt->fetch()) {
        jsonResponse(['error' => '频道不存在'], 404);
    }

    $stmt = $pdo->prepare('
        INSERT INTO chat_messages (channel_id, sender_id, content, msg_type)
        VALUES (?, ?, ?, ?)
    ');
    $stmt->execute([$channelId, $senderId, $content, 'Normal']);

    jsonResponse([
        'success' => true,
        'id' => intval($pdo->lastInsertId()),
        'created_at' => date('Y-m-d H:i:s'),
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
 * Body: { name, description, icon_emoji }
 */
function createChannel(array $body): void {
    $name        = trim($body['name'] ?? '');
    $description = trim($body['description'] ?? '');
    $iconEmoji   = trim($body['icon_emoji'] ?? '💬');

    if (empty($name)) {
        jsonResponse(['error' => '频道名称不能为空'], 400);
    }
    if (mb_strlen($name) > 64) {
        jsonResponse(['error' => '频道名称不能超过64字'], 400);
    }

    $pdo = getDB();

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
