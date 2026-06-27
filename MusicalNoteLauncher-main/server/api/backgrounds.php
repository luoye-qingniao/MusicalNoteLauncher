<?php
// ============================================
// MNL 启动器 - 背景素材库 API
// ============================================

require_once __DIR__ . '/db_config.php';

// 上传文件存储目录（相对于 api 目录）
define('UPLOAD_DIR', __DIR__ . '/../uploads/backgrounds/');
define('MAX_FILE_SIZE', 500 * 1024 * 1024); // 500MB

// 确保上传目录存在
if (!is_dir(UPLOAD_DIR)) {
    mkdir(UPLOAD_DIR, 0755, true);
}

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
            listBackgrounds();
            break;
        case 'file':
            serveFile();
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

function handlePost(string $action): void {
    switch ($action) {
        case 'upload':
            uploadBackground();
            break;
        case 'delete':
            $body = json_decode(file_get_contents('php://input'), true) ?? [];
            deleteBackground($body);
            break;
        default:
            jsonResponse(['error' => 'Unknown action'], 400);
    }
}

/**
 * 获取背景素材列表
 */
function listBackgrounds(): void {
    $page = max(1, intval($_GET['page'] ?? 1));
    $pageSize = min(50, max(1, intval($_GET['page_size'] ?? 20)));
    $type = $_GET['type'] ?? '';

    $pdo = getDB();
    $where = "WHERE is_active = 1";
    $params = [];

    if (!empty($type) && ($type === 'Image' || $type === 'Video')) {
        $where .= " AND type = ?";
        $params[] = $type;
    }

    // 总数
    $countStmt = $pdo->prepare("SELECT COUNT(*) FROM backgrounds $where");
    $countStmt->execute($params);
    $total = $countStmt->fetchColumn();

    // 分页
    $offset = ($page - 1) * $pageSize;
    $stmt = $pdo->prepare("
        SELECT id, name, type, file_name, file_size, uploader,
               download_count, created_at
        FROM backgrounds $where
        ORDER BY id DESC
        LIMIT $pageSize OFFSET $offset
    ");
    $stmt->execute($params);
    $backgrounds = $stmt->fetchAll();

    // 为每个背景添加下载 URL
    $baseUrl = getBaseUrl();
    foreach ($backgrounds as &$bg) {
        $bg['download_url'] = $baseUrl . '/api/backgrounds.php?action=file&id=' . $bg['id'];
    }

    jsonResponse([
        'backgrounds' => $backgrounds,
        'total' => intval($total),
        'page' => $page,
        'page_size' => $pageSize
    ]);
}

/**
 * 上传背景素材
 */
function uploadBackground(): void {
    // 验证文件
    if (!isset($_FILES['file']) || $_FILES['file']['error'] !== UPLOAD_ERR_OK) {
        $errorCode = $_FILES['file']['error'] ?? -1;
        $errorMsg = match($errorCode) {
            UPLOAD_ERR_INI_SIZE, UPLOAD_ERR_FORM_SIZE => '文件大小超出限制',
            UPLOAD_ERR_NO_FILE => '未选择文件',
            default => '文件上传失败'
        };
        jsonResponse(['error' => $errorMsg], 400);
    }

    $file = $_FILES['file'];

    // 大小限制
    if ($file['size'] > MAX_FILE_SIZE) {
        jsonResponse(['error' => '文件大小不能超过 500MB'], 400);
    }

    // 格式验证
    $ext = strtolower(pathinfo($file['name'], PATHINFO_EXTENSION));
    $allowedImageExts = ['jpg', 'jpeg', 'png', 'webp'];
    $allowedVideoExts = ['mp4'];

    if (in_array($ext, $allowedImageExts)) {
        $type = 'Image';
    } elseif (in_array($ext, $allowedVideoExts)) {
        $type = 'Video';
    } else {
        jsonResponse(['error' => '不支持的格式，仅支持 JPG、PNG、WEBP、MP4'], 400);
    }

    // 生成唯一文件名
    $uniqueId = bin2hex(random_bytes(8));
    $fileName = $uniqueId . '.' . $ext;
    $filePath = UPLOAD_DIR . $fileName;

    // 移动文件
    if (!move_uploaded_file($file['tmp_name'], $filePath)) {
        jsonResponse(['error' => '文件保存失败'], 500);
    }

    // 获取参数
    $name = $_POST['name'] ?? pathinfo($file['name'], PATHINFO_FILENAME);
    $uploader = $_POST['uploader'] ?? '';

    // 写入数据库
    try {
        $pdo = getDB();
        $stmt = $pdo->prepare("
            INSERT INTO backgrounds (name, type, file_name, file_size, uploader)
            VALUES (?, ?, ?, ?, ?)
        ");
        $stmt->execute([$name, $type, $fileName, $file['size'], $uploader]);

        $newId = $pdo->lastInsertId();
        $baseUrl = getBaseUrl();

        jsonResponse([
            'success' => true,
            'id' => intval($newId),
            'name' => $name,
            'type' => $type,
            'file_size' => $file['size'],
            'download_url' => $baseUrl . '/api/backgrounds.php?action=file&id=' . $newId,
        ]);
    } catch (Exception $e) {
        // 入库失败，删除已上传文件
        if (file_exists($filePath)) {
            unlink($filePath);
        }
        jsonResponse(['error' => '数据库写入失败: ' . $e->getMessage()], 500);
    }
}

/**
 * 删除背景素材
 */
function deleteBackground(array $body): void {
    $id = intval($body['id'] ?? 0);
    if ($id <= 0) {
        jsonResponse(['error' => 'Invalid id'], 400);
    }

    $pdo = getDB();

    // 查询文件信息
    $stmt = $pdo->prepare("SELECT file_name FROM backgrounds WHERE id = ?");
    $stmt->execute([$id]);
    $bg = $stmt->fetch();

    if (!$bg) {
        jsonResponse(['error' => '背景不存在'], 404);
    }

    // 删除数据库记录（软删除）
    $stmt = $pdo->prepare("UPDATE backgrounds SET is_active = 0 WHERE id = ?");
    $stmt->execute([$id]);

    // 删除文件
    $filePath = UPLOAD_DIR . $bg['file_name'];
    if (file_exists($filePath)) {
        unlink($filePath);
    }

    jsonResponse(['success' => true]);
}

/**
 * 提供文件下载（并计数）
 */
function serveFile(): void {
    $id = intval($_GET['id'] ?? 0);
    if ($id <= 0) {
        jsonResponse(['error' => 'Invalid id'], 400);
    }

    $pdo = getDB();

    // 查询文件信息
    $stmt = $pdo->prepare("SELECT file_name, type, name FROM backgrounds WHERE id = ? AND is_active = 1");
    $stmt->execute([$id]);
    $bg = $stmt->fetch();

    if (!$bg) {
        jsonResponse(['error' => '背景不存在'], 404);
    }

    $filePath = UPLOAD_DIR . $bg['file_name'];
    if (!file_exists($filePath)) {
        jsonResponse(['error' => '文件不存在'], 404);
    }

    // 更新下载计数
    $stmt = $pdo->prepare("UPDATE backgrounds SET download_count = download_count + 1 WHERE id = ?");
    $stmt->execute([$id]);

    // 设置 Content-Type
    $ext = strtolower(pathinfo($bg['file_name'], PATHINFO_EXTENSION));
    $mimeTypes = [
        'jpg' => 'image/jpeg',
        'jpeg' => 'image/jpeg',
        'png' => 'image/png',
        'webp' => 'image/webp',
        'mp4' => 'video/mp4',
    ];
    $contentType = $mimeTypes[$ext] ?? 'application/octet-stream';

    // 输出文件
    header('Content-Type: ' . $contentType);
    header('Content-Length: ' . filesize($filePath));
    header('Content-Disposition: inline; filename="' . $bg['name'] . '.' . $ext . '"');
    header('Cache-Control: public, max-age=86400');
    readfile($filePath);
    exit;
}

/**
 * 获取当前请求的基础 URL
 */
function getBaseUrl(): string {
    $protocol = isset($_SERVER['HTTPS']) && $_SERVER['HTTPS'] === 'on' ? 'https' : 'http';
    $host = $_SERVER['HTTP_HOST'] ?? 'localhost';
    return $protocol . '://' . $host;
}
