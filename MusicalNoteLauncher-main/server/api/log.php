<?php
// ============================================
// MNL 启动器 - 日志上报接口
// POST /api/log.php
// 
// 支持两种日志类型:
//   type=startup  → 启动日志
//   type=crash    → 崩溃日志
// ============================================

require_once __DIR__ . '/db_config.php';

// 仅接受 POST
if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    jsonResponse(['success' => false, 'message' => '仅支持 POST 请求'], 405);
}

// 读取 JSON body
$rawBody = file_get_contents('php://input');
$data = json_decode($rawBody, true);

if (!$data || !isset($data['type'])) {
    jsonResponse(['success' => false, 'message' => '无效的请求数据，缺少 type 字段'], 400);
}

$type = $data['type'];
$clientIP = getClientIP();

try {
    $db = getDB();

    switch ($type) {
        case 'startup':
            handleStartupLog($db, $data, $clientIP);
            break;
        case 'crash':
            handleCrashLog($db, $data, $clientIP);
            break;
        default:
            jsonResponse(['success' => false, 'message' => "未知的日志类型: {$type}"], 400);
    }
} catch (Exception $e) {
    error_log("Log API Error: " . $e->getMessage());
    jsonResponse(['success' => false, 'message' => '服务器内部错误'], 500);
}

/**
 * 处理启动日志
 */
function handleStartupLog(PDO $db, array $data, string $ip): void {
    $required = ['client_id', 'launcher_version'];
    foreach ($required as $field) {
        if (empty($data[$field])) {
            jsonResponse(['success' => false, 'message' => "缺少必填字段: {$field}"], 400);
        }
    }

    $stmt = $db->prepare(
        'INSERT INTO startup_logs 
         (client_id, launcher_version, os_version, clr_version, startup_time, is_success, error_message, ip_address)
         VALUES (:client_id, :launcher_version, :os_version, :clr_version, :startup_time, :is_success, :error_message, :ip_address)'
    );

    $stmt->execute([
        ':client_id'        => $data['client_id'],
        ':launcher_version' => $data['launcher_version'],
        ':os_version'       => $data['os_version'] ?? '',
        ':clr_version'      => $data['clr_version'] ?? '',
        ':startup_time'     => $data['startup_time'] ?? date('Y-m-d H:i:s'),
        ':is_success'       => isset($data['is_success']) ? (int)$data['is_success'] : 1,
        ':error_message'    => $data['error_message'] ?? null,
        ':ip_address'       => $ip,
    ]);

    jsonResponse(['success' => true, 'message' => '启动日志已记录', 'id' => (int)$db->lastInsertId()]);
}

/**
 * 处理崩溃日志
 */
function handleCrashLog(PDO $db, array $data, string $ip): void {
    $required = ['client_id', 'launcher_version', 'exception_type', 'exception_message', 'stack_trace'];
    foreach ($required as $field) {
        if (empty($data[$field])) {
            jsonResponse(['success' => false, 'message' => "缺少必填字段: {$field}"], 400);
        }
    }

    $stmt = $db->prepare(
        'INSERT INTO crash_logs 
         (client_id, launcher_version, crash_time, exception_type, exception_message, stack_trace, thread_name, is_terminating, ip_address)
         VALUES (:client_id, :launcher_version, :crash_time, :exception_type, :exception_message, :stack_trace, :thread_name, :is_terminating, :ip_address)'
    );

    $stmt->execute([
        ':client_id'         => $data['client_id'],
        ':launcher_version'  => $data['launcher_version'],
        ':crash_time'        => $data['crash_time'] ?? date('Y-m-d H:i:s'),
        ':exception_type'    => $data['exception_type'],
        ':exception_message' => $data['exception_message'],
        ':stack_trace'       => $data['stack_trace'],
        ':thread_name'       => $data['thread_name'] ?? '',
        ':is_terminating'    => isset($data['is_terminating']) ? (int)$data['is_terminating'] : 0,
        ':ip_address'        => $ip,
    ]);

    jsonResponse(['success' => true, 'message' => '崩溃日志已记录', 'id' => (int)$db->lastInsertId()]);
}
