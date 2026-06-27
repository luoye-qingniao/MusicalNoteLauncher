<?php
// ============================================
// MNL 启动器 - 白名单验证接口
// GET/POST /api/whitelist.php
//
// 参数:
//   username   - 用户名（必填）
//   client_id  - 客户端ID（可选）
//
// 返回:
//   { allowed: true/false, message: "..." }
// ============================================

require_once __DIR__ . '/db_config.php';

if (!in_array($_SERVER['REQUEST_METHOD'], ['GET', 'POST'])) {
    jsonResponse(['success' => false, 'message' => '不支持的请求方法'], 405);
}

// 从 GET 或 POST 获取参数
$username  = $_REQUEST['username']  ?? '';
$clientId  = $_REQUEST['client_id'] ?? '';

if (empty($username)) {
    jsonResponse(['allowed' => false, 'message' => '缺少用户名参数'], 400);
}

try {
    $db = getDB();

    // 查询白名单：匹配用户名（client_id 为空则仅匹配用户名，否则需要同时匹配）
    if (!empty($clientId)) {
        $stmt = $db->prepare(
            'SELECT id, username, remark FROM whitelist 
             WHERE is_enabled = 1 
               AND username = :username 
               AND (client_id = :client_id OR client_id = "")
             LIMIT 1'
        );
        $stmt->execute([':username' => $username, ':client_id' => $clientId]);
    } else {
        $stmt = $db->prepare(
            'SELECT id, username, remark FROM whitelist 
             WHERE is_enabled = 1 AND username = :username
             LIMIT 1'
        );
        $stmt->execute([':username' => $username]);
    }

    $row = $stmt->fetch();

    if ($row) {
        jsonResponse([
            'allowed'  => true,
            'message'  => '白名单验证通过',
            'username' => $row['username'],
        ]);
    } else {
        jsonResponse([
            'allowed'  => false,
            'message'  => "用户 '{$username}' 不在白名单中，请联系管理员",
        ]);
    }

} catch (Exception $e) {
    error_log("Whitelist API Error: " . $e->getMessage());
    jsonResponse(['allowed' => false, 'message' => '白名单验证服务暂时不可用'], 500);
}
