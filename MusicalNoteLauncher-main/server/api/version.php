<?php
// ============================================
// MNL 启动器 - 版本查询接口
// GET /api/version.php
//
// 可选参数:
//   current_version - 当前客户端版本号
//
// 返回最新活跃版本信息，包含:
//   version, download_url, file_hash, file_size,
//   release_notes, is_forced, need_update
// ============================================

require_once __DIR__ . '/db_config.php';

if (!in_array($_SERVER['REQUEST_METHOD'], ['GET', 'POST'])) {
    jsonResponse(['success' => false, 'message' => '不支持的请求方法'], 405);
}

$currentVersion = $_REQUEST['current_version'] ?? '0.0.0';

try {
    $db = getDB();

    // 查询最新活跃版本
    $stmt = $db->prepare(
        'SELECT version, version_code, download_url, file_hash, file_size, 
                release_notes, is_forced, min_launcher_version
         FROM launcher_versions 
         WHERE is_active = 1 
         ORDER BY version_code DESC 
         LIMIT 1'
    );
    $stmt->execute();
    $latest = $stmt->fetch();

    if (!$latest) {
        // 数据库无版本记录，返回静态默认版本（测试用）
        jsonResponse([
            'version'         => '1.0.0',
            'version_code'    => 1,
            'download_url'    => '',
            'file_hash'       => '',
            'file_size'       => 0,
            'release_notes'   => '暂无可用更新。',
            'is_forced'       => false,
            'need_update'     => false,
        ]);
        return;
    }

    // 比较版本号
    $needUpdate = version_compare($latest['version'], $currentVersion, '>');

    $response = [
        'version'         => $latest['version'],
        'version_code'    => (int)$latest['version_code'],
        'download_url'    => $latest['download_url'],
        'file_hash'       => $latest['file_hash'],
        'file_size'       => (int)$latest['file_size'],
        'release_notes'   => $latest['release_notes'],
        'is_forced'       => (bool)$latest['is_forced'],
        'need_update'     => $needUpdate,
    ];

    jsonResponse($response);

} catch (Exception $e) {
    error_log("Version API Error: " . $e->getMessage());
    // 数据库出错时返回静态版本（保证启动器不卡住）
    jsonResponse([
        'version'       => '1.0.0',
        'version_code'  => 1,
        'download_url'  => '',
        'file_hash'     => '',
        'file_size'     => 0,
        'release_notes' => '版本检查服务暂时不可用。',
        'is_forced'     => false,
        'need_update'   => false,
    ]);
}
