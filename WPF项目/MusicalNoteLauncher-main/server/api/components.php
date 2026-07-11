<?php
// ============================================
// MNL 启动器 - 组件商店 API
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
            listComponents();
            break;
        case 'search':
            searchComponents();
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

function handlePost(string $action): void {
    $body = json_decode(file_get_contents('php://input'), true) ?? [];
    switch ($action) {
        case 'download':
            trackDownload($body);
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

/**
 * 获取组件列表
 */
function listComponents(): void {
    $page = max(1, intval($_GET['page'] ?? 1));
    $pageSize = min(50, max(1, intval($_GET['page_size'] ?? 20)));
    $category = $_GET['category'] ?? '';

    $pdo = getDB();
    $where = "WHERE is_active = 1";
    $params = [];

    if (!empty($category) && $category !== 'all') {
        $where .= " AND category = ?";
        $params[] = $category;
    }

    // 总数
    $countStmt = $pdo->prepare("SELECT COUNT(*) FROM components $where");
    $countStmt->execute($params);
    $total = $countStmt->fetchColumn();

    // 分页
    $offset = ($page - 1) * $pageSize;
    $stmt = $pdo->prepare("
        SELECT id, name, category, description, icon_emoji, author,
               download_url, rating, download_count, mc_version, file_size
        FROM components $where
        ORDER BY sort_order DESC, id ASC
        LIMIT $pageSize OFFSET $offset
    ");
    $stmt->execute($params);
    $components = $stmt->fetchAll();

    jsonResponse([
        'components' => $components,
        'total' => intval($total),
        'page' => $page,
        'page_size' => $pageSize
    ]);
}

/**
 * 搜索组件
 */
function searchComponents(): void {
    $q = $_GET['q'] ?? '';
    if (strlen($q) < 1) {
        jsonResponse(['components' => [], 'total' => 0]);
        return;
    }

    $pdo = getDB();
    $stmt = $pdo->prepare("
        SELECT id, name, category, description, icon_emoji, author,
               download_url, rating, download_count, mc_version, file_size
        FROM components
        WHERE is_active = 1 AND (name LIKE ? OR description LIKE ?)
        ORDER BY sort_order DESC, id ASC
    ");
    $like = "%$q%";
    $stmt->execute([$like, $like]);
    $components = $stmt->fetchAll();

    jsonResponse([
        'components' => $components,
        'total' => count($components)
    ]);
}

/**
 * 下载计数
 */
function trackDownload(array $body): void {
    $componentId = intval($body['component_id'] ?? 0);
    if ($componentId <= 0) {
        jsonResponse(['error' => 'Invalid component_id'], 400);
    }

    $pdo = getDB();
    $stmt = $pdo->prepare('UPDATE components SET download_count = download_count + 1 WHERE id = ?');
    $stmt->execute([$componentId]);

    jsonResponse(['success' => true]);
}
