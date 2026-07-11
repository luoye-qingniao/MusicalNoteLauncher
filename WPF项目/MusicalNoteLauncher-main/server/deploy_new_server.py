#!/usr/bin/env python3
# ============================================
# MNL 启动器 - 新服务器一键部署脚本
# 目标：85.137.246.87 (root)
# 功能：安装 LEMP 环境 + 部署全部 API + 初始化数据库
# ============================================
import subprocess
import os
import sys
import time

# ── 配置 ──
SERVER = "85.137.246.87"
USER = "root"
PASSWORD = "5V1T28sB8PmZ"
WEB_ROOT = "/var/www/html"
API_DIR = f"{WEB_ROOT}/api"
UPLOADS_DIR = f"{WEB_ROOT}/uploads/backgrounds"
DB_NAME = "mnl_launcher"
DB_USER = "mnl_user"
# 生成随机密码（部署后会在服务器上创建此用户）
DB_PASS = "MNL@2024#QingNiao!Db"
MYSQL_ROOT_PASS = "5V1T28sB8PmZ"  # 与服务器 root 密码相同，便于管理

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
API_SOURCE = os.path.join(SCRIPT_DIR, "api")
SQL_FILES = [
    os.path.join(SCRIPT_DIR, "init_db.sql"),
    os.path.join(SCRIPT_DIR, "migration_v2.sql"),
    os.path.join(SCRIPT_DIR, "migration_v3.sql"),
    os.path.join(SCRIPT_DIR, "migration_v4.sql"),
]

def run_ssh(cmd, check=True, timeout=60):
    """通过 SSH 执行远程命令"""
    full_cmd = (
        f'ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null '
        f'-o PreferredAuthentications=password -o PubkeyAuthentication=no '
        f'-o ConnectTimeout=10 '
        f'{USER}@{SERVER} "{cmd}"'
    )
    proc = subprocess.Popen(
        full_cmd, shell=True, stdin=subprocess.PIPE,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )
    try:
        stdout, stderr = proc.communicate(input=f"{PASSWORD}\n".encode(), timeout=timeout)
    except subprocess.TimeoutExpired:
        proc.kill()
        print(f"[TIMEOUT] 命令超时 ({timeout}s)")
        return False, "timeout"
    output = stdout.decode('utf-8', errors='replace') + stderr.decode('utf-8', errors='replace')
    if check and proc.returncode != 0:
        print(f"[FAIL] (exit={proc.returncode})")
        print(output[-500:])
        return False, output
    return True, output

def run_scp(local_path, remote_path):
    """通过 SCP 复制文件到远程服务器"""
    full_cmd = (
        f'scp -r -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null '
        f'-o PreferredAuthentications=password -o PubkeyAuthentication=no '
        f'"{local_path}" {USER}@{SERVER}:"{remote_path}"'
    )
    proc = subprocess.Popen(
        full_cmd, shell=True, stdin=subprocess.PIPE,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )
    try:
        stdout, stderr = proc.communicate(input=f"{PASSWORD}\n".encode(), timeout=120)
    except subprocess.TimeoutExpired:
        proc.kill()
        print(f"[TIMEOUT] SCP 超时")
        return False
    if proc.returncode != 0:
        output = stderr.decode('utf-8', errors='replace')
        print(f"[FAIL] SCP 上传失败: {output[-300:]}")
        return False
    return True

def step(msg, num, total):
    print(f"\n{'='*60}")
    print(f"[{num}/{total}] {msg}")
    print(f"{'='*60}")

def main():
    total_steps = 7
    print("=" * 60)
    print("MNL 启动器 - 新服务器一键部署")
    print(f"目标: {USER}@{SERVER}")
    print(f"时间: {time.strftime('%Y-%m-%d %H:%M:%S')}")
    print("=" * 60)

    # ── 验证本地文件 ──
    if not os.path.isdir(API_SOURCE):
        print(f"[ERROR] 找不到 API 目录: {API_SOURCE}")
        sys.exit(1)
    for sql_file in SQL_FILES:
        if not os.path.exists(sql_file):
            print(f"[WARN] SQL 文件不存在: {sql_file}")

    # ── 1. 测试 SSH 连接 ──
    step("测试 SSH 连接", 1, total_steps)
    ok, output = run_ssh("whoami && cat /etc/os-release | head -3 && uname -m")
    if not ok:
        print("[FATAL] SSH 连接失败，请检查 IP、用户名、密码和网络")
        sys.exit(1)
    print(f"[OK] 连接成功")
    for line in output.strip().split('\n')[:5]:
        if line.strip() and 'PASSWORD' not in line.upper():
            print(f"     {line.strip()}")

    # ── 2. 安装 LEMP 环境 ──
    step("安装 Nginx + PHP-FPM + MySQL", 2, total_steps)
    install_script = f"""
export DEBIAN_FRONTEND=noninteractive

# 检测系统
if command -v apt-get &> /dev/null; then
    PKG_MGR="apt-get"
elif command -v yum &> /dev/null; then
    PKG_MGR="yum"
elif command -v dnf &> /dev/null; then
    PKG_MGR="dnf"
else
    echo "UNKNOWN_PKG_MGR"
    exit 1
fi

echo "Package manager: $PKG_MGR"

if [ "$PKG_MGR" = "apt-get" ]; then
    apt-get update -qq
    apt-get install -y -qq nginx php-fpm php-mysql php-cli php-json php-mbstring php-xml php-gd php-curl mariadb-server mariadb-client
elif [ "$PKG_MGR" = "yum" ] || [ "$PKG_MGR" = "dnf" ]; then
    $PKG_MGR install -y nginx php-fpm php-mysqlnd php-cli php-json php-mbstring php-xml php-gd php-curl mariadb-server mariadb
fi

# 启动服务
systemctl enable nginx 2>/dev/null || true
systemctl enable php*-fpm 2>/dev/null || true
systemctl enable mariadb 2>/dev/null || true
systemctl enable mysql 2>/dev/null || true

systemctl start mariadb 2>/dev/null || systemctl start mysql 2>/dev/null || true
systemctl start php*-fpm 2>/dev/null || true
systemctl start nginx 2>/dev/null || true

echo "INSTALL_OK"
"""
    ok, output = run_ssh(install_script, timeout=300)
    if ok and "INSTALL_OK" in output:
        print("[OK] LEMP 环境安装完成")
    else:
        print("[WARN] 安装可能部分失败，继续尝试...")

    # ── 3. 配置 MySQL ──
    step("配置 MySQL 数据库", 3, total_steps)
    mysql_setup = f"""
# 确保 MySQL 运行
systemctl start mariadb 2>/dev/null || systemctl start mysql 2>/dev/null || true
sleep 2

# 使用 root 连接并创建数据库和用户
mysql -u root <<'EOSQL' 2>&1
CREATE DATABASE IF NOT EXISTS {DB_NAME} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '{DB_USER}'@'localhost' IDENTIFIED BY '{DB_PASS}';
CREATE USER IF NOT EXISTS '{DB_USER}'@'127.0.0.1' IDENTIFIED BY '{DB_PASS}';
GRANT ALL PRIVILEGES ON {DB_NAME}.* TO '{DB_USER}'@'localhost';
GRANT ALL PRIVILEGES ON {DB_NAME}.* TO '{DB_USER}'@'127.0.0.1';
FLUSH PRIVILEGES;
SELECT 'DB_SETUP_OK' AS status;
EOSQL
"""
    ok, output = run_ssh(mysql_setup, timeout=30)
    if ok and "DB_SETUP_OK" in output:
        print("[OK] 数据库和用户创建成功")
    else:
        # 尝试设置 root 密码后再试
        print("[WARN] 首次尝试失败，尝试设置 root 密码...")
        run_ssh(f'mysqladmin -u root password "{MYSQL_ROOT_PASS}" 2>/dev/null || true', check=False)
        # 用 root 密码重试
        mysql_setup2 = f"""
mysql -u root -p'{MYSQL_ROOT_PASS}' <<'EOSQL' 2>&1
CREATE DATABASE IF NOT EXISTS {DB_NAME} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '{DB_USER}'@'localhost' IDENTIFIED BY '{DB_PASS}';
CREATE USER IF NOT EXISTS '{DB_USER}'@'127.0.0.1' IDENTIFIED BY '{DB_PASS}';
GRANT ALL PRIVILEGES ON {DB_NAME}.* TO '{DB_USER}'@'localhost';
GRANT ALL PRIVILEGES ON {DB_NAME}.* TO '{DB_USER}'@'127.0.0.1';
FLUSH PRIVILEGES;
SELECT 'DB_SETUP_OK' AS status;
EOSQL
"""
        ok, output = run_ssh(mysql_setup2, timeout=30)
        if ok and "DB_SETUP_OK" in output:
            print("[OK] 数据库和用户创建成功（使用 root 密码）")
        else:
            print(f"[WARN] 数据库配置可能失败，请手动检查")
            print(output[-500:])

    # ── 4. 上传 API 文件 ──
    step("上传 PHP API 文件", 4, total_steps)
    
    # 创建目录
    run_ssh(f"mkdir -p {API_DIR} && mkdir -p {UPLOADS_DIR} && chmod 755 {UPLOADS_DIR}", check=False)
    
    # 列出 API 文件
    api_files = [f for f in os.listdir(API_SOURCE) if f.endswith('.php')]
    print(f"找到 {len(api_files)} 个 API 文件: {', '.join(api_files)}")
    
    # 逐个上传
    for api_file in api_files:
        local_path = os.path.join(API_SOURCE, api_file)
        remote_path = f"{API_DIR}/{api_file}"
        if run_scp(local_path, remote_path):
            print(f"  [OK] {api_file}")
        else:
            print(f"  [FAIL] {api_file}")
    
    # 上传 SQL 文件到临时目录
    for sql_file in SQL_FILES:
        if os.path.exists(sql_file):
            run_scp(sql_file, f"/tmp/{os.path.basename(sql_file)}")

    # ── 5. 更新 db_config.php ──
    step("配置服务器端数据库连接", 5, total_steps)
    
    # 直接替换数据库配置
    update_config = f"""
cat > {API_DIR}/db_config.php <<'EOPHP'
<?php
// ============================================
// MNL 启动器 - 数据库配置
// 服务器: {SERVER}
// ============================================

define('DB_HOST', 'localhost');
define('DB_PORT', 3306);
define('DB_NAME', '{DB_NAME}');
define('DB_USER', '{DB_USER}');
define('DB_PASS', '{DB_PASS}');
define('DB_CHARSET', 'utf8mb4');

/**
 * 获取 PDO 数据库连接
 */
function getDB(): PDO {{
    static $pdo = null;
    if ($pdo === null) {{
        $dsn = sprintf('mysql:host=%s;port=%d;dbname=%s;charset=%s',
            DB_HOST, DB_PORT, DB_NAME, DB_CHARSET);
        $pdo = new PDO($dsn, DB_USER, DB_PASS, [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_EMULATE_PREPARES => false,
        ]);
    }}
    return $pdo;
}}

/**
 * 返回 JSON 响应
 */
function jsonResponse(array $data, int $httpCode = 200): void {{
    http_response_code($httpCode);
    header('Content-Type: application/json; charset=utf-8');
    header('Access-Control-Allow-Origin: *');
    header('Access-Control-Allow-Methods: POST, GET, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type, Authorization');
    echo json_encode($data, JSON_UNESCAPED_UNICODE);
    exit;
}}

/**
 * 获取客户端真实 IP
 */
function getClientIP(): string {{
    if (!empty($_SERVER['HTTP_X_FORWARDED_FOR'])) {{
        $ips = explode(',', $_SERVER['HTTP_X_FORWARDED_FOR']);
        return trim($ips[0]);
    }}
    if (!empty($_SERVER['HTTP_X_REAL_IP'])) {{
        return $_SERVER['HTTP_X_REAL_IP'];
    }}
    return $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';
}}

// 处理 OPTIONS 预检请求
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {{
    header('Access-Control-Allow-Origin: *');
    header('Access-Control-Allow-Methods: POST, GET, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type, Authorization');
    http_response_code(204);
    exit;
}}
EOPHP
echo "CONFIG_UPDATED"
"""
    ok, output = run_ssh(update_config, timeout=15)
    if ok and "CONFIG_UPDATED" in output:
        print("[OK] db_config.php 已更新")
    else:
        print("[WARN] db_config.php 更新可能失败")

    # ── 6. 执行数据库迁移 ──
    step("执行数据库迁移", 6, total_steps)
    
    for sql_file in SQL_FILES:
        if not os.path.exists(sql_file):
            continue
        basename = os.path.basename(sql_file)
        run_sql = f"mysql -u {DB_USER} -p'{DB_PASS}' {DB_NAME} < /tmp/{basename} 2>&1 && echo 'SQL_OK_{basename}'"
        ok, output = run_ssh(run_sql, timeout=30)
        if ok:
            print(f"  [OK] {basename}")
        else:
            # 尝试用 root 执行
            run_sql2 = f"mysql -u root -p'{MYSQL_ROOT_PASS}' {DB_NAME} < /tmp/{basename} 2>&1 && echo 'SQL_OK_{basename}'"
            ok2, output2 = run_ssh(run_sql2, timeout=30)
            if ok2:
                print(f"  [OK] {basename} (via root)")
            else:
                print(f"  [WARN] {basename} 可能部分失败（表可能已存在）")
    
    # 清理临时 SQL 文件
    run_ssh("rm -f /tmp/init_db.sql /tmp/migration_v2.sql /tmp/migration_v3.sql", check=False)

    # ── 7. 配置 Nginx ──
    step("配置 Nginx 站点", 7, total_steps)
    
    nginx_config = f"""
cat > /etc/nginx/sites-available/mnl-api <<'EONGINX'
server {{
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name _;
    root /var/www/html;
    index index.php index.html index.htm;

    # 上传限制 500MB
    client_max_body_size 500M;

    # API 路由
    location /api/ {{
        try_files $uri =404;
        fastcgi_pass unix:/var/run/php/php*-fpm.sock;
        fastcgi_index index.php;
        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
        include fastcgi_params;
    }}

    # PHP 处理（兜底）
    location ~ \.php$ {{
        try_files $uri =404;
        fastcgi_pass unix:/var/run/php/php*-fpm.sock;
        fastcgi_index index.php;
        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
        include fastcgi_params;
    }}

    # 静态文件
    location /uploads/ {{
        alias /var/www/html/uploads/;
        expires 30d;
        add_header Cache-Control "public, immutable";
    }}

    # 禁止访问隐藏文件
    location ~ /\. {{
        deny all;
        access_log off;
        log_not_found off;
    }}
}}
EONGINX

# 修复 fastcgi_pass socket 路径（适配不同 PHP 版本）
PHP_SOCK=$(find /var/run/php/ -name "*.sock" 2>/dev/null | head -1)
if [ -n "$PHP_SOCK" ]; then
    sed -i "s|unix:/var/run/php/php\\*-fpm.sock|unix:$PHP_SOCK|g" /etc/nginx/sites-available/mnl-api
fi

# 启用站点
rm -f /etc/nginx/sites-enabled/default 2>/dev/null
ln -sf /etc/nginx/sites-available/mnl-api /etc/nginx/sites-enabled/mnl-api 2>/dev/null

# 测试配置并重载
nginx -t 2>&1 && systemctl reload nginx 2>&1 || systemctl restart nginx 2>&1
echo "NGINX_CONFIGURED"
"""
    ok, output = run_ssh(nginx_config, timeout=30)
    if ok and "NGINX_CONFIGURED" in output:
        print("[OK] Nginx 配置完成")
    else:
        print("[WARN] Nginx 配置可能失败")
        print(output[-300:])

    # ── 验证 ──
    print(f"\n{'='*60}")
    print("验证部署结果")
    print(f"{'='*60}")
    
    # 测试 API
    test_urls = [
        f"http://{SERVER}/api/version.php",
        f"http://{SERVER}/api/whitelist.php?username=Player",
        f"http://{SERVER}/api/components.php?action=list",
    ]
    for url in test_urls:
        ok, output = run_ssh(f"curl -s -o /dev/null -w '%{{http_code}}' {url} 2>&1", timeout=10)
        print(f"  {url} -> HTTP {output.strip() if ok else 'FAIL'}")

    print(f"\n{'='*60}")
    print("部署完成！")
    print(f"{'='*60}")
    print(f"服务器地址: http://{SERVER}")
    print(f"API 基础路径: http://{SERVER}/api/")
    print(f"数据库名: {DB_NAME}")
    print(f"数据库用户: {DB_USER}")
    print(f"")
    print(f"可用 API 端点:")
    print(f"  - 青鸟账号: http://{SERVER}/api/auth.php?action=register|login|profile")
    print(f"  - 聊天社区: http://{SERVER}/api/community.php?action=channels|messages|send")
    print(f"  - 版本检查: http://{SERVER}/api/version.php")
    print(f"  - 白名单验证: http://{SERVER}/api/whitelist.php")
    print(f"  - 好友系统: http://{SERVER}/api/friends.php")
    print(f"  - 聊天消息: http://{SERVER}/api/friends.php?action=poll")
    print(f"  - 组件商店: http://{SERVER}/api/components.php")
    print(f"  - 日志上报: http://{SERVER}/api/log.php")
    print(f"  - 背景素材: http://{SERVER}/api/backgrounds.php")
    print(f"{'='*60}")

if __name__ == "__main__":
    main()
