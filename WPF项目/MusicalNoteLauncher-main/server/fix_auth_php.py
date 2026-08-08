#!/usr/bin/env python3
"""Fix auth.php: add error handling to updateProfile"""
import paramiko

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect('85.137.246.87', username='root', password='5V1T28sB8PmZ', timeout=10)

# Read current auth.php
stdin, stdout, stderr = ssh.exec_command('cat /var/www/html/api/auth.php')
content = stdout.read().decode()

# Find and replace updateProfile to add try-catch
old_code = '''function updateProfile(array $body): void {
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
}'''

new_code = '''function updateProfile(array $body): void {
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

    try {
        $pdo = getDB();
        $pdo->prepare($sql)->execute($params);
        jsonResponse(['success' => true]);
    } catch (PDOException $e) {
        error_log('updateProfile SQL error: ' . $e->getMessage());
        jsonResponse(['error' => '更新失败，服务器内部错误'], 500);
    }
}'''

if old_code in content:
    content = content.replace(old_code, new_code)
    # Write back
    sftp = ssh.open_sftp()
    with sftp.file('/var/www/html/api/auth.php', 'w') as f:
        f.write(content)
    sftp.close()
    print("SUCCESS: auth.php updated with error handling")
else:
    print("FAILED: Could not find updateProfile in auth.php")
    print("Auth.php content length:", len(content))

ssh.close()
