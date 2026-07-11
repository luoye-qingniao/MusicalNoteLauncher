<?php
// ============================================
// MNL 启动器 - 青鸟账号系统 API
// ============================================
// 功能：注册、登录、Token 认证、个人资料、用户搜索
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
        case 'profile':
            getProfile();
            break;
        case 'search':
            searchUsers();
            break;
        case 'ping':
            jsonResponse(['pong' => true, 'server_time' => date('Y-m-d H:i:s')]);
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

function handlePost(string $action): void {
    $body = json_decode(file_get_contents('php://input'), true) ?? [];
    switch ($action) {
        case 'register':
            register($body);
            break;
        case 'login':
            login($body);
            break;
        case 'send_verification':
            sendVerificationCode($body);
            break;
        case 'refresh_token':
            refreshToken($body);
            break;
        case 'logout':
            logout($body);
            break;
        case 'update_profile':
            updateProfile($body);
            break;
        case 'change_password':
            changePassword($body);
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

/**
 * 验证认证令牌，返回用户信息
 */
function authenticate(): array {
    $authHeader = $_SERVER['HTTP_AUTHORIZATION'] ?? '';
    if (empty($authHeader) || !str_starts_with($authHeader, 'Bearer ')) {
        jsonResponse(['error' => '未提供认证令牌'], 401);
    }
    $token = substr($authHeader, 7);

    $pdo = getDB();
    $stmt = $pdo->prepare('
        SELECT u.id, u.qingniao_id, u.nickname, u.role, u.is_banned,
               t.expires_at
        FROM auth_tokens t
        JOIN users u ON u.id = t.user_id
        WHERE t.token = ?
    ');
    $stmt->execute([$token]);
    $row = $stmt->fetch();

    if (!$row) {
        jsonResponse(['error' => '令牌无效'], 401);
    }
    if (strtotime($row['expires_at']) < time()) {
        jsonResponse(['error' => '令牌已过期，请重新登录'], 401);
    }
    if ($row['is_banned']) {
        jsonResponse(['error' => '账号已被封禁'], 403);
    }

    return $row;
}

/**
 * 用户注册
 * POST /api/auth.php?action=register
 * Body: { qingniao_id, password, nickname?, email? }
 */
function register(array $body): void {
    $password       = $body['password'] ?? '';
    $nickname       = trim($body['nickname'] ?? '');
    $email          = trim($body['email'] ?? '');
    $verificationCode = trim($body['verification_code'] ?? '');

    // 验证输入
    if (strlen($password) < 6 || strlen($password) > 64) {
        jsonResponse(['error' => '密码长度需在6-64个字符之间'], 400);
    }
    if (empty($email)) {
        jsonResponse(['error' => '邮箱不能为空'], 400);
    }
    if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
        jsonResponse(['error' => '邮箱格式不正确'], 400);
    }

    // 检查该邮箱是否已被注册
    $pdo = getDB();
    $stmt = $pdo->prepare('SELECT id FROM users WHERE email = ?');
    $stmt->execute([$email]);
    if ($stmt->fetch()) {
        jsonResponse(['error' => '该邮箱已被注册'], 409);
    }

    // 验证验证码
    if (empty($verificationCode)) {
        jsonResponse(['error' => '请输入邮件验证码'], 400);
    }

    // 确保 verification_codes 表存在
    $pdo->exec('
        CREATE TABLE IF NOT EXISTS verification_codes (
            id INT AUTO_INCREMENT PRIMARY KEY,
            email VARCHAR(255) NOT NULL,
            code VARCHAR(8) NOT NULL,
            expires_at DATETIME NOT NULL,
            used TINYINT(1) NOT NULL DEFAULT 0,
            created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX idx_email_code (email, code),
            INDEX idx_expires (expires_at)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    ');

    $stmt = $pdo->prepare('
        SELECT id FROM verification_codes
        WHERE email = ? AND code = ? AND expires_at > NOW() AND used = 0
        ORDER BY created_at DESC LIMIT 1
    ');
    $stmt->execute([$email, $verificationCode]);
    $vc = $stmt->fetch();

    if (!$vc) {
        jsonResponse(['error' => '验证码错误或已过期'], 400);
    }

    // 标记验证码为已使用
    $stmt = $pdo->prepare('UPDATE verification_codes SET used = 1 WHERE id = ?');
    $stmt->execute([$vc['id']]);

    // 生成青鸟ID
    $qingniaoId = 'QN' . strtoupper(substr(md5($email . time() . uniqid()), 0, 8));
    if (empty($nickname)) {
        $nickname = $qingniaoId;
    }

    // 创建用户
    $passwordHash = password_hash($password, PASSWORD_BCRYPT, ['cost' => 12]);
    $stmt = $pdo->prepare('
        INSERT INTO users (qingniao_id, nickname, password_hash, email)
        VALUES (?, ?, ?, ?)
    ');
    $stmt->execute([$qingniaoId, $nickname, $passwordHash, $email]);
    $userId = $pdo->lastInsertId();

    // 生成 token
    $token = bin2hex(random_bytes(32));
    $expiresAt = date('Y-m-d H:i:s', time() + 86400 * 30);

    $stmt = $pdo->prepare('
        INSERT INTO auth_tokens (user_id, token, expires_at) VALUES (?, ?, ?)
    ');
    $stmt->execute([$userId, $token, $expiresAt]);

    // 加入白名单
    try {
        $stmt = $pdo->prepare('
            INSERT IGNORE INTO whitelist (username, qingniao_id, is_enabled, remark)
            VALUES (?, ?, 1, ?)
        ');
        $stmt->execute([$qingniaoId, $qingniaoId, '青鸟注册用户']);
    } catch (Exception $e) {}

    jsonResponse([
        'success' => true,
        'user' => [
            'id' => intval($userId),
            'qingniao_id' => $qingniaoId,
            'nickname' => $nickname,
            'role' => 'user',
        ],
        'token' => $token,
        'expires_at' => $expiresAt,
    ]);
}

/**
 * 发送邮件验证码
 * POST /api/auth.php?action=send_verification
 * Body: { email }
 */
function sendVerificationCode(array $body): void {
    $email = trim($body['email'] ?? '');

    if (empty($email)) {
        jsonResponse(['error' => '邮箱不能为空'], 400);
    }
    if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
        jsonResponse(['error' => '邮箱格式不正确'], 400);
    }

    $pdo = getDB();

    // 确保表存在
    $pdo->exec('
        CREATE TABLE IF NOT EXISTS verification_codes (
            id INT AUTO_INCREMENT PRIMARY KEY,
            email VARCHAR(255) NOT NULL,
            code VARCHAR(8) NOT NULL,
            expires_at DATETIME NOT NULL,
            used TINYINT(1) NOT NULL DEFAULT 0,
            created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            INDEX idx_email_code (email, code),
            INDEX idx_expires (expires_at)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
    ');

    // 检查最近60秒是否已发送过（防刷）
    $stmt = $pdo->prepare('
        SELECT id FROM verification_codes
        WHERE email = ? AND created_at > DATE_SUB(NOW(), INTERVAL 60 SECOND)
    ');
    $stmt->execute([$email]);
    if ($stmt->fetch()) {
        jsonResponse(['error' => '请等待60秒后重新发送'], 429);
    }

    // 生成6位验证码
    $code = str_pad(rand(0, 999999), 6, '0', STR_PAD_LEFT);
    $expiresAt = date('Y-m-d H:i:s', time() + 600); // 10分钟有效

    $stmt = $pdo->prepare('
        INSERT INTO verification_codes (email, code, expires_at) VALUES (?, ?, ?)
    ');
    $stmt->execute([$email, $code, $expiresAt]);

    // 发送邮件
    $subject = '=?UTF-8?B?' . base64_encode('MusicalNote Launcher - 邮箱验证码') . '?=';
    $mailBody = "您好！\n\n"
        . "您的验证码是：{$code}\n"
        . "验证码10分钟内有效，请勿泄露。\n\n"
        . "如非本人操作，请忽略此邮件。\n\n"
        . "-- MusicalNote Launcher";

    $headers = [
        'MIME-Version: 1.0',
        'Content-Type: text/plain; charset=UTF-8',
        'From: MusicalNote Launcher <noreply@mnl.launcher>',
        'X-Mailer: PHP/' . phpversion(),
    ];

    $sent = mail($email, $subject, $mailBody, implode("\r\n", $headers));

    if (!$sent) {
        // 邮件发送失败但仍记录验证码（可通过数据库查看）
        // 在生产环境中应配置 SMTP
    }

    jsonResponse([
        'success' => true,
        'message' => '验证码已发送到 ' . $email,
    ]);
}

/**
 * 用户登录
 * POST /api/auth.php?action=login
 * Body: { qingniao_id, password, client_id? }
 */
function login(array $body): void {
    $qingniaoId = trim($body['qingniao_id'] ?? '');
    $password   = $body['password'] ?? '';
    $clientId   = trim($body['client_id'] ?? '');

    if (empty($qingniaoId) || empty($password)) {
        jsonResponse(['error' => '青鸟ID和密码不能为空'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('
        SELECT id, qingniao_id, nickname, email, avatar_url, signature,
               password_hash, role, is_banned
        FROM users WHERE qingniao_id = ?
    ');
    $stmt->execute([$qingniaoId]);
    $user = $stmt->fetch();

    if (!$user || !password_verify($password, $user['password_hash'])) {
        jsonResponse(['error' => '青鸟ID或密码错误'], 401);
    }

    if ($user['is_banned']) {
        jsonResponse(['error' => '账号已被封禁，请联系管理员'], 403);
    }

    // 生成 token（30天有效）
    $token = bin2hex(random_bytes(32));
    $expiresAt = date('Y-m-d H:i:s', time() + 86400 * 30);

    $stmt = $pdo->prepare('
        INSERT INTO auth_tokens (user_id, token, client_id, expires_at) VALUES (?, ?, ?, ?)
    ');
    $stmt->execute([$user['id'], $token, $clientId, $expiresAt]);

    // 更新最后登录信息
    $ip = getClientIP();
    $stmt = $pdo->prepare('
        UPDATE users SET last_login_at = NOW(), last_login_ip = ? WHERE id = ?
    ');
    $stmt->execute([$ip, $user['id']]);

    // 同时确保白名单存在
    try {
        $stmt = $pdo->prepare('
            INSERT IGNORE INTO whitelist (username, qingniao_id, is_enabled, remark)
            VALUES (?, ?, 1, ?)
        ');
        $stmt->execute([$qingniaoId, $qingniaoId, '青鸟注册用户']);
    } catch (Exception $e) {}

    jsonResponse([
        'success' => true,
        'user' => [
            'id' => intval($user['id']),
            'qingniao_id' => $user['qingniao_id'],
            'nickname' => $user['nickname'],
            'email' => $user['email'],
            'avatar_url' => $user['avatar_url'],
            'signature' => $user['signature'],
            'role' => $user['role'],
        ],
        'token' => $token,
        'expires_at' => $expiresAt,
    ]);
}

/**
 * 刷新令牌
 * POST /api/auth.php?action=refresh_token
 * Body: { token }
 */
function refreshToken(array $body): void {
    $oldToken = $body['token'] ?? '';

    if (empty($oldToken)) {
        jsonResponse(['error' => '缺少令牌'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('
        SELECT user_id, client_id FROM auth_tokens WHERE token = ? AND expires_at > NOW()
    ');
    $stmt->execute([$oldToken]);
    $row = $stmt->fetch();

    if (!$row) {
        jsonResponse(['error' => '令牌无效或已过期'], 401);
    }

    // 删除旧令牌，生成新令牌
    $pdo->prepare('DELETE FROM auth_tokens WHERE token = ?')->execute([$oldToken]);

    $newToken = bin2hex(random_bytes(32));
    $expiresAt = date('Y-m-d H:i:s', time() + 86400 * 30);

    $stmt = $pdo->prepare('
        INSERT INTO auth_tokens (user_id, token, client_id, expires_at) VALUES (?, ?, ?, ?)
    ');
    $stmt->execute([$row['user_id'], $newToken, $row['client_id'], $expiresAt]);

    jsonResponse([
        'success' => true,
        'token' => $newToken,
        'expires_at' => $expiresAt,
    ]);
}

/**
 * 退出登录
 * POST /api/auth.php?action=logout
 * Body: { token }
 */
function logout(array $body): void {
    $token = $body['token'] ?? '';

    if (empty($token)) {
        jsonResponse(['error' => '缺少令牌'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('DELETE FROM auth_tokens WHERE token = ?');
    $stmt->execute([$token]);

    jsonResponse(['success' => true]);
}

/**
 * 获取个人资料
 * GET /api/auth.php?action=profile&qingniao_id=xxx
 * 或使用 Authorization header 获取自己的资料
 */
function getProfile(): void {
    $qingniaoId = $_GET['qingniao_id'] ?? '';

    // 如果没有指定ID，则从认证令牌获取
    if (empty($qingniaoId)) {
        try {
            $auth = authenticate();
            $qingniaoId = $auth['qingniao_id'];
        } catch (Exception $e) {
            jsonResponse(['error' => '请提供青鸟ID或登录令牌'], 400);
        }
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('
        SELECT qingniao_id, nickname, email, avatar_url, signature,
               role, last_login_at, created_at
        FROM users WHERE qingniao_id = ?
    ');
    $stmt->execute([$qingniaoId]);
    $user = $stmt->fetch();

    if (!$user) {
        jsonResponse(['error' => '用户不存在'], 404);
    }

    // 获取在线状态
    $stmt2 = $pdo->prepare('
        SELECT last_heartbeat FROM user_online WHERE user_id = ?
    ');
    $stmt2->execute([$qingniaoId]);
    $online = $stmt2->fetch();

    $isOnline = $online && (time() - strtotime($online['last_heartbeat'])) < 120;

    jsonResponse([
        'user' => array_merge($user, [
            'is_online' => $isOnline,
        ]),
    ]);
}

/**
 * 更新个人资料
 * POST /api/auth.php?action=update_profile
 * Header: Authorization: Bearer <token>
 * Body: { nickname?, avatar_url?, signature? }
 */
function updateProfile(array $body): void {
    $auth = authenticate();

    $fields = [];
    $params = [];

    if (isset($body['nickname']) && !empty(trim($body['nickname']))) {
        $fields[] = 'nickname = ?';
        $params[] = trim($body['nickname']);
    }
    if (isset($body['avatar_url'])) {
        $fields[] = 'avatar_url = ?';
        $params[] = $body['avatar_url'];
    }
    if (isset($body['signature'])) {
        $fields[] = 'signature = ?';
        $params[] = $body['signature'];
    }

    if (empty($fields)) {
        jsonResponse(['error' => '没有需要更新的字段'], 400);
    }

    $params[] = $auth['id'];
    $sql = 'UPDATE users SET ' . implode(', ', $fields) . ' WHERE id = ?';

    $pdo = getDB();
    $pdo->prepare($sql)->execute($params);

    jsonResponse(['success' => true]);
}

/**
 * 修改密码
 * POST /api/auth.php?action=change_password
 * Header: Authorization: Bearer <token>
 * Body: { old_password, new_password }
 */
function changePassword(array $body): void {
    $auth = authenticate();
    $oldPassword = $body['old_password'] ?? '';
    $newPassword = $body['new_password'] ?? '';

    if (empty($oldPassword) || empty($newPassword)) {
        jsonResponse(['error' => '新旧密码不能为空'], 400);
    }
    if (strlen($newPassword) < 6 || strlen($newPassword) > 64) {
        jsonResponse(['error' => '新密码长度需在6-64个字符之间'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('SELECT password_hash FROM users WHERE id = ?');
    $stmt->execute([$auth['id']]);
    $user = $stmt->fetch();

    if (!password_verify($oldPassword, $user['password_hash'])) {
        jsonResponse(['error' => '原密码错误'], 400);
    }

    $newHash = password_hash($newPassword, PASSWORD_BCRYPT, ['cost' => 12]);
    $pdo->prepare('UPDATE users SET password_hash = ? WHERE id = ?')->execute([$newHash, $auth['id']]);

    // 清除所有旧令牌（强制重新登录）
    $pdo->prepare('DELETE FROM auth_tokens WHERE user_id = ?')->execute([$auth['id']]);

    jsonResponse(['success' => true, 'message' => '密码已修改，请重新登录']);
}

/**
 * 搜索用户
 * GET /api/auth.php?action=search&q=xxx
 */
function searchUsers(): void {
    $q = trim($_GET['q'] ?? '');
    if (strlen($q) < 1) {
        jsonResponse(['users' => []]);
        return;
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('
        SELECT qingniao_id, nickname, avatar_url, signature, role
        FROM users
        WHERE qingniao_id LIKE ? OR nickname LIKE ?
          AND is_banned = 0
        ORDER BY
          CASE WHEN qingniao_id = ? THEN 0 ELSE 1 END,
          qingniao_id ASC
        LIMIT 20
    ');
    $like = "%$q%";
    $stmt->execute([$like, $like, $q]);

    jsonResponse(['users' => $stmt->fetchAll()]);
}
