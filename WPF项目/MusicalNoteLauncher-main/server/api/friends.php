<?php
// ============================================
// MNL 启动器 - 好友系统 API
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
        case 'list':
            listFriends();
            break;
        case 'poll':
            pollMessages();
            break;
        case 'search':
            searchUsers();
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

function handlePost(string $action): void {
    $body = json_decode(file_get_contents('php://input'), true) ?? [];
    switch ($action) {
        case 'add':
            addFriend($body);
            break;
        case 'remove':
            removeFriend($body);
            break;
        case 'send':
            sendMessage($body);
            break;
        case 'invite':
            sendInvite($body);
            break;
        case 'heartbeat':
            heartbeat($body);
            break;
        case 'accept_invite':
            acceptInvite($body);
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

/**
 * 获取好友列表
 */
function listFriends(): void {
    $userId = $_GET['user_id'] ?? '';
    if (empty($userId)) {
        jsonResponse(['error' => 'Missing user_id'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('
        SELECT f.friend_id, f.friend_nickname,
               uo.last_heartbeat,
               CASE WHEN uo.last_heartbeat > DATE_SUB(NOW(), INTERVAL 120 SECOND) THEN 1 ELSE 0 END AS is_online
        FROM friends f
        LEFT JOIN user_online uo ON uo.user_id = f.friend_id
        WHERE f.user_id = ?
        ORDER BY is_online DESC, f.created_at DESC
    ');
    $stmt->execute([$userId]);
    $friends = $stmt->fetchAll();

    jsonResponse(['friends' => $friends]);
}

/**
 * 搜索用户（通过青鸟ID添加好友前查询）
 */
function searchUsers(): void {
    $q = $_GET['q'] ?? '';
    if (strlen($q) < 2) {
        jsonResponse(['users' => []]);
        return;
    }

    // 目前简化为直接返回查询结果，后续可扩展为用户表
    jsonResponse(['users' => [['id' => $q, 'name' => $q]]]);
}

/**
 * 添加好友
 */
function addFriend(array $body): void {
    $userId = $body['user_id'] ?? '';
    $friendId = $body['friend_id'] ?? '';
    $friendNickname = $body['friend_nickname'] ?? $friendId;

    if (empty($userId) || empty($friendId)) {
        jsonResponse(['error' => 'Missing user_id or friend_id'], 400);
    }
    if ($userId === $friendId) {
        jsonResponse(['error' => 'Cannot add yourself'], 400);
    }

    $pdo = getDB();
    try {
        $stmt = $pdo->prepare('INSERT INTO friends (user_id, friend_id, friend_nickname) VALUES (?, ?, ?)');
        $stmt->execute([$userId, $friendId, $friendNickname]);

        // 双向添加也插入反向关系（方便双方看到对方）
        try {
            $stmt2 = $pdo->prepare('INSERT IGNORE INTO friends (user_id, friend_id, friend_nickname) VALUES (?, ?, ?)');
            $stmt2->execute([$friendId, $userId, $userId]);
        } catch (Exception $e) { /* 忽略重复 */ }

        jsonResponse(['success' => true]);
    } catch (PDOException $e) {
        if ($e->getCode() == 23000) {
            jsonResponse(['error' => 'Already friends'], 409);
        }
        jsonResponse(['error' => 'Database error'], 500);
    }
}

/**
 * 删除好友
 */
function removeFriend(array $body): void {
    $userId = $body['user_id'] ?? '';
    $friendId = $body['friend_id'] ?? '';

    if (empty($userId) || empty($friendId)) {
        jsonResponse(['error' => 'Missing user_id or friend_id'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('DELETE FROM friends WHERE user_id = ? AND friend_id = ?');
    $stmt->execute([$userId, $friendId]);

    // 同时删除反向关系
    $stmt2 = $pdo->prepare('DELETE FROM friends WHERE user_id = ? AND friend_id = ?');
    $stmt2->execute([$friendId, $userId]);

    jsonResponse(['success' => true]);
}

/**
 * 发送聊天消息
 */
function sendMessage(array $body): void {
    $senderId = $body['sender_id'] ?? '';
    $receiverId = $body['receiver_id'] ?? '';
    $content = $body['content'] ?? '';

    if (empty($senderId) || empty($receiverId)) {
        jsonResponse(['error' => 'Missing sender_id or receiver_id'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('INSERT INTO friend_messages (sender_id, receiver_id, content, msg_type) VALUES (?, ?, ?, ?)');
    $stmt->execute([$senderId, $receiverId, $content, 'Normal']);

    jsonResponse(['success' => true, 'id' => $pdo->lastInsertId()]);
}

/**
 * 发送联机邀请
 */
function sendInvite(array $body): void {
    $senderId = $body['sender_id'] ?? '';
    $receiverId = $body['receiver_id'] ?? '';
    $networkName = $body['network_name'] ?? '';
    $networkSecret = $body['network_secret'] ?? '';
    $gameVersion = $body['game_version'] ?? '';

    if (empty($senderId) || empty($receiverId)) {
        jsonResponse(['error' => 'Missing sender_id or receiver_id'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('
        INSERT INTO friend_messages (sender_id, receiver_id, content, msg_type,
                                     invite_network_name, invite_network_secret, invite_game_version)
        VALUES (?, ?, ?, ?, ?, ?, ?)
    ');
    $stmt->execute([
        $senderId, $receiverId,
        "🎮 联机邀请\n网络名：$networkName",
        'Invite',
        $networkName, $networkSecret, $gameVersion
    ]);

    jsonResponse(['success' => true, 'id' => $pdo->lastInsertId()]);
}

/**
 * 接受联机邀请
 */
function acceptInvite(array $body): void {
    $messageId = $body['message_id'] ?? 0;
    if (empty($messageId)) {
        jsonResponse(['error' => 'Missing message_id'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('UPDATE friend_messages SET invite_accepted = 1 WHERE id = ?');
    $stmt->execute([$messageId]);

    jsonResponse(['success' => true]);
}

/**
 * 轮询新消息
 */
function pollMessages(): void {
    $userId = $_GET['user_id'] ?? '';
    $since = intval($_GET['since'] ?? 0);

    if (empty($userId)) {
        jsonResponse(['error' => 'Missing user_id'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('
        SELECT fm.*, IFNULL(uo.last_heartbeat > DATE_SUB(NOW(), INTERVAL 120 SECOND), 0) AS sender_online
        FROM friend_messages fm
        LEFT JOIN user_online uo ON uo.user_id = fm.sender_id
        WHERE fm.receiver_id = ? AND fm.id > ?
        ORDER BY fm.created_at ASC
        LIMIT 50
    ');
    $stmt->execute([$userId, $since]);
    $messages = $stmt->fetchAll();

    // 标记已读
    if (!empty($messages)) {
        $ids = array_column($messages, 'id');
        $placeholders = implode(',', array_fill(0, count($ids), '?'));
        $pdo->prepare("UPDATE friend_messages SET is_read = 1 WHERE id IN ($placeholders)")->execute($ids);
    }

    jsonResponse(['messages' => $messages]);
}

/**
 * 心跳上报
 */
function heartbeat(array $body): void {
    $userId = $body['user_id'] ?? '';
    $version = $body['launcher_version'] ?? '';

    if (empty($userId)) {
        jsonResponse(['error' => 'Missing user_id'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('
        INSERT INTO user_online (user_id, last_heartbeat, launcher_version)
        VALUES (?, NOW(), ?)
        ON DUPLICATE KEY UPDATE last_heartbeat = NOW(), launcher_version = VALUES(launcher_version)
    ');
    $stmt->execute([$userId, $version]);

    // 同时检查好友在线状态
    $stmt2 = $pdo->prepare('
        SELECT friend_id FROM friends WHERE user_id = ?
    ');
    $stmt2->execute([$userId]);
    $friendIds = $stmt2->fetchAll(PDO::FETCH_COLUMN);

    $onlineFriends = [];
    if (!empty($friendIds)) {
        $placeholders = implode(',', array_fill(0, count($friendIds), '?'));
        $stmt3 = $pdo->query("
            SELECT user_id FROM user_online
            WHERE user_id IN ($placeholders)
            AND last_heartbeat > DATE_SUB(NOW(), INTERVAL 120 SECOND)
        ");
        $onlineFriends = $stmt3 ? $stmt3->fetchAll(PDO::FETCH_COLUMN) : [];
    }

    jsonResponse(['success' => true, 'online_friends' => $onlineFriends]);
}
