<?php
// ============================================
// MNL 启动器 - 统一安全防护模块
// 提供：频率限制 / 客户端签名 / Token 认证 / 黑名单 / 输入净化
// ============================================

// ═══════════════════════════════════════════
// 配置常量
// ═══════════════════════════════════════════

// HMAC 签名密钥（客户端必须用相同密钥签名）
define('MNL_HMAC_SECRET', 'MNL@2024#Launch!SecKey');

// 签名时间戳有效窗口（秒）
define('MNL_SIGN_WINDOW', 30);

// 存储目录
define('MNL_RATELIMIT_DIR', __DIR__ . '/../data/rate_limits');
define('MNL_BLACKLIST_DIR', __DIR__ . '/../data/blacklist');

// ═══════════════════════════════════════════
// 频率限制规则（每个端点：[最大请求数, 时间窗口秒]）
// ═══════════════════════════════════════════

function getRateLimitRules(): array {
    return [
        // auth
        'auth_login'    => [5, 60],       // 登录：60 秒内 5 次
        'auth_register' => [3, 3600],     // 注册：每小时 3 次
        'auth_code'     => [2, 120],      // 发送验证码：2 分钟 2 次
        'auth_profile'  => [20, 60],
        'auth_password' => [3, 300],

        // community
        'community_send'        => [20, 60],     // 发消息：60 秒 20 次
        'community_create'      => [5, 300],     // 创建频道：5 分钟 5 次
        'community_avatar'      => [5, 300],     // 上传头像：5 分钟 5 次
        'community_clear'       => [1, 60],

        // friends
        'friends_send'   => [15, 60],       // 私信：60 秒 15 次
        'friends_add'    => [10, 60],       // 加好友：60 秒 10 次
        'friends_remove' => [10, 60],
        'friends_heartbeat' => [60, 60],
        'friends_invite' => [10, 60],

        // backgrounds
        'bg_upload'  => [5, 3600],      // 上传背景：每小时 5 次
        'bg_delete'  => [10, 300],      // 删除背景：5 分钟 10 次
        'bg_increment' => [30, 60],

        // general
        'general'     => [120, 60],      // 默认：60 秒 120 次
    ];
}

// ═══════════════════════════════════════════
// 频率限制核心
// ═══════════════════════════════════════════

/**
 * 检查 IP + 端点的频率限制。超限返回 429 并退出。
 *
 * @param string $endpoint 端点标识（如 'auth_login'）
 * @param int|null $maxRequests 覆盖默认值
 * @param int|null $windowSeconds 覆盖默认值
 */
function checkRateLimit(string $endpoint, ?int $maxRequests = null, ?int $windowSeconds = null): void {
    $rules = getRateLimitRules();

    if ($maxRequests === null && isset($rules[$endpoint])) {
        $maxRequests = $rules[$endpoint][0];
        $windowSeconds = $rules[$endpoint][1];
    }
    if ($maxRequests === null) {
        $maxRequests = $rules['general'][0];
        $windowSeconds = $rules['general'][1];
    }

    $ip = getClientIP();
    $ipHash = md5($ip);

    if (!is_dir(MNL_RATELIMIT_DIR)) {
        mkdir(MNL_RATELIMIT_DIR, 0700, true);
    }

    $key = "{$endpoint}:{$ipHash}";
    $file = MNL_RATELIMIT_DIR . '/' . md5($key) . '.rl';
    $now = time();

    $hits = [];
    if (file_exists($file)) {
        $data = @json_decode(@file_get_contents($file), true);
        $hits = (is_array($data) && isset($data['hits'])) ? $data['hits'] : [];
    }

    // 清理过期记录
    $hits = array_values(array_filter($hits, fn($t) => $t > ($now - $windowSeconds)));

    // 检测陡增攻击：如果短时间内请求量是窗口上限的 5 倍，临时封禁
    $surgeThreshold = $maxRequests * 5;
    if (count($hits) >= $surgeThreshold) {
        banIp($ip, 1800, "{$endpoint}: surge attack ({$surgeThreshold} req/{$windowSeconds}s)");
        http_response_code(429);
        echo json_encode(['success' => false, 'error' => '检测到异常流量，IP 已被临时封禁 30 分钟']);
        exit;
    }

    if (count($hits) >= $maxRequests) {
        autoBanOnRepeat($ip, $endpoint, count($hits));
        http_response_code(429);
        header('Retry-After: ' . $windowSeconds);
        echo json_encode([
            'success' => false,
            'error' => '请求过于频繁，请 ' . $windowSeconds . ' 秒后再试',
            'retry_after' => $windowSeconds,
        ]);
        exit;
    }

    $hits[] = $now;
    file_put_contents($file, json_encode(['hits' => $hits, 'ts' => $now]), LOCK_EX);
}

// ═══════════════════════════════════════════
// IP 黑名单
// ═══════════════════════════════════════════

/**
 * 检查当前 IP 是否被封禁。已封禁返回 403 并退出。
 */
function checkBlacklist(): void {
    $ip = getClientIP();
    if (isIpBlacklisted($ip)) {
        http_response_code(403);
        echo json_encode(['success' => false, 'error' => '您的 IP 已被临时封禁，请稍后再试']);
        exit;
    }
}

function isIpBlacklisted(string $ip): bool {
    $file = MNL_BLACKLIST_DIR . '/' . md5($ip) . '.ban';
    if (!file_exists($file)) return false;

    $data = @json_decode(@file_get_contents($file), true);
    if (!$data) return false;

    $expires = $data['expires'] ?? 0;
    if ($expires > time()) return true;

    @unlink($file);
    return false;
}

function banIp(string $ip, int $durationSeconds, string $reason = ''): void {
    if (!is_dir(MNL_BLACKLIST_DIR)) {
        mkdir(MNL_BLACKLIST_DIR, 0700, true);
    }
    file_put_contents(
        MNL_BLACKLIST_DIR . '/' . md5($ip) . '.ban',
        json_encode([
            'ip'     => $ip,
            'reason' => $reason,
            'at'     => time(),
            'expires' => time() + $durationSeconds,
        ]),
        LOCK_EX
    );
}

/**
 * 连续触达频率上限 3 次后自动封禁 IP
 */
function autoBanOnRepeat(string $ip, string $endpoint, int $hitCount): void {
    $file = MNL_RATELIMIT_DIR . '/' . md5("_block:{$ip}") . '.bl';
    $data = @json_decode(@file_get_contents($file), true) ?: ['count' => 0, 'last' => 0];
    $now = time();

    // 30 分钟内连续超限
    if ($now - $data['last'] < 1800) {
        $data['count']++;
    } else {
        $data['count'] = 1;
    }
    $data['last'] = $now;

    if ($data['count'] >= 3) {
        banIp($ip, 3600, "重复触发频率限制: {$endpoint}");
        $data['count'] = 0;
    }

    file_put_contents($file, json_encode($data), LOCK_EX);
}

// ═══════════════════════════════════════════
// 客户端 HMAC 签名验证
// ═══════════════════════════════════════════

/**
 * 验证客户端 HMAC 签名。
 * 客户端在请求头中发送 X-MNL-Sign 和 X-MNL-TS。
 * 签名字符串：timestamp + ":" + method + ":" + path
 * 不强制要求（兼容旧客户端），但验证失败则 403。
 */
function verifyClientSignature(): bool {
    $sign = $_SERVER['HTTP_X_MNL_SIGN'] ?? '';
    $ts   = $_SERVER['HTTP_X_MNL_TS']   ?? '';

    // 无签名头 → 兼容旧客户端，仅标记
    if ($sign === '' || $ts === '') {
        return true;
    }

    // 防重放：时间戳窗口
    $now = time();
    if (abs($now - (int)$ts) > MNL_SIGN_WINDOW) {
        http_response_code(403);
        echo json_encode(['success' => false, 'error' => '请求时间戳已过期']);
        exit;
    }

    $method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
    $path   = parse_url($_SERVER['REQUEST_URI'] ?? '/', PHP_URL_PATH);
    $data   = "{$ts}:{$method}:{$path}";

    $expected = hash_hmac('sha256', $data, MNL_HMAC_SECRET);
    if (!hash_equals($expected, $sign)) {
        http_response_code(403);
        echo json_encode(['success' => false, 'error' => '签名验证失败']);
        exit;
    }

    return true;
}

// ═══════════════════════════════════════════
// Token 认证
// ═══════════════════════════════════════════

/**
 * 从 Authorization: Bearer 头验证 token，返回用户数组或 null
 */
function authenticateRequest(PDO $pdo): ?array {
    $header = $_SERVER['HTTP_AUTHORIZATION'] ?? '';
    if ($header === '' || !preg_match('/^Bearer\s+(.+)$/i', $header, $m)) {
        return null;
    }

    $token = $m[1];
    $stmt = $pdo->prepare("
        SELECT u.qingniao_id, u.nickname, u.email, u.avatar_url, u.signature,
               u.is_banned
        FROM auth_tokens t
        JOIN users u ON u.qingniao_id = t.qingniao_id
        WHERE t.token = ? AND t.expires_at > NOW()
    ");
    $stmt->execute([$token]);
    $user = $stmt->fetch(PDO::FETCH_ASSOC);

    if (!$user || $user['is_banned']) return null;
    return $user;
}

/**
 * 要求认证。未认证返回 401 并退出，已认证返回用户数组。
 */
function requireAuth(PDO $pdo): array {
    $user = authenticateRequest($pdo);
    if (!$user) {
        http_response_code(401);
        echo json_encode(['success' => false, 'error' => '未授权：请先登录']);
        exit;
    }
    return $user;
}

// ═══════════════════════════════════════════
// 输入净化
// ═══════════════════════════════════════════

/**
 * 递归净化输入：去除首尾空白和空字节（防止 null byte injection）
 */
function sanitize($data) {
    if (is_array($data)) return array_map('sanitize', $data);
    if (is_string($data)) return trim(str_replace("\0", '', $data));
    return $data;
}

/**
 * 净化 HTTP 响应头中的值（防止 CRLF 头注入）
 */
function cleanHeaderValue(string $value): string {
    return str_replace(["\r", "\n", "\0"], '', $value);
}

// ═══════════════════════════════════════════
// 安全响应头
// ═══════════════════════════════════════════

function setSecurityHeaders(): void {
    header('X-Content-Type-Options: nosniff');
    header('X-Frame-Options: DENY');
    header('X-XSS-Protection: 1; mode=block');
    header('Referrer-Policy: strict-origin-when-cross-origin');
}
