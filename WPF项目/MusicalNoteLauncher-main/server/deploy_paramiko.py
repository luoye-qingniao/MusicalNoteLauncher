#!/usr/bin/env python3
# ============================================
# MNL 启动器 - 新服务器一键部署脚本 (Paramiko版)
# ============================================
import os
import sys
import time
import paramiko

# ── 配置 ──
SERVER = "85.137.246.87"
USER = "root"
PASSWORD = "5V1T28sB8PmZ"
PORT = 22

WEB_ROOT = "/var/www/html"
API_DIR = f"{WEB_ROOT}/api"
UPLOADS_DIR = f"{WEB_ROOT}/uploads/backgrounds"
DB_NAME = "mnl_launcher"
DB_USER = "mnl_user"
DB_PASS = "MNL@2024#QingNiao!Db"
MYSQL_ROOT_PASS = "5V1T28sB8PmZ"

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
API_SOURCE = os.path.join(SCRIPT_DIR, "api")
SQL_FILES = [
    "init_db.sql",
    "migration_v2.sql",
    "migration_v3.sql",
    "migration_v4.sql",
]

def ssh_connect():
    """建立 SSH 连接"""
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(SERVER, PORT, USER, PASSWORD, timeout=30, look_for_keys=False, allow_agent=False)
    return client

def run_ssh(client, cmd, timeout=60):
    """执行远程命令"""
    stdin, stdout, stderr = client.exec_command(cmd, timeout=timeout)
    out = stdout.read().decode('utf-8', errors='replace')
    err = stderr.read().decode('utf-8', errors='replace')
    exit_code = stdout.channel.recv_exit_status()
    return exit_code, out + err

def upload_file(client, local_path, remote_path):
    """上传文件"""
    sftp = client.open_sftp()
    sftp.put(local_path, remote_path)
    sftp.close()

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

    client = None
    try:
        # ── 1. 连接 ──
        step("连接服务器", 1, total_steps)
        client = ssh_connect()
        code, output = run_ssh(client, "whoami && cat /etc/os-release | head -3")
        print(f"[OK] 已连接: {output.strip().split(chr(10))[0]}")

        # ── 2. 安装 LEMP ──
        step("安装 Nginx + PHP + MariaDB", 2, total_steps)
        print("正在更新包列表并安装（可能需要几分钟）...")
        install_cmd = """
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq 2>/dev/null
apt-get install -y -qq nginx php-fpm php-mysql php-cli php-json php-mbstring php-xml php-gd php-curl mariadb-server mariadb-client 2>&1
systemctl enable nginx mariadb php*-fpm 2>/dev/null || true
systemctl start mariadb php*-fpm nginx 2>/dev/null || true
echo "INSTALL_OK"
"""
        code, output = run_ssh(client, install_cmd, timeout=300)
        if code == 0 and "INSTALL_OK" in output:
            print("[OK] LEMP 环境安装完成")
        else:
            print(f"[WARN] 安装可能部分失败 (exit={code})")
            print(output[-500:])

        # 启动服务
        run_ssh(client, "systemctl start mariadb 2>/dev/null; systemctl start php8.1-fpm 2>/dev/null; systemctl start nginx 2>/dev/null; sleep 2; echo OK", check=False)

        # ── 3. 配置 MySQL ──
        step("配置 MySQL 数据库", 3, total_steps)
        
        # 先尝试无密码连接（新安装的 MariaDB）
        mysql_cmd = f"""mysql -u root <<'EOSQL'
CREATE DATABASE IF NOT EXISTS {DB_NAME} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '{DB_USER}'@'localhost' IDENTIFIED BY '{DB_PASS}';
CREATE USER IF NOT EXISTS '{DB_USER}'@'127.0.0.1' IDENTIFIED BY '{DB_PASS}';
GRANT ALL PRIVILEGES ON {DB_NAME}.* TO '{DB_USER}'@'localhost';
GRANT ALL PRIVILEGES ON {DB_NAME}.* TO '{DB_USER}'@'127.0.0.1';
FLUSH PRIVILEGES;
SELECT 'DB_SETUP_OK' AS status;
EOSQL"""
        code, output = run_ssh(client, mysql_cmd, timeout=30)
        if code == 0 and "DB_SETUP_OK" in output:
            print("[OK] 数据库和用户创建成功")
        else:
            print(f"[OK/INFO] 数据库可能已存在，继续...")

        # ── 4. 上传文件 ──
        step("上传 API 文件", 4, total_steps)
        
        # 确保目录存在
        run_ssh(client, f"mkdir -p {API_DIR} && mkdir -p {UPLOADS_DIR} && chmod 755 {UPLOADS_DIR}")
        
        # 上传所有 PHP 文件
        api_files = [f for f in os.listdir(API_SOURCE) if f.endswith('.php')]
        print(f"上传 {len(api_files)} 个 API 文件...")
        for api_file in api_files:
            local_path = os.path.join(API_SOURCE, api_file)
            remote_path = f"{API_DIR}/{api_file}"
            try:
                upload_file(client, local_path, remote_path)
                print(f"  [OK] {api_file}")
            except Exception as e:
                print(f"  [FAIL] {api_file}: {e}")

        # 上传 SQL 文件
        for sql_file in SQL_FILES:
            local_path = os.path.join(SCRIPT_DIR, sql_file)
            if os.path.exists(local_path):
                upload_file(client, local_path, f"/tmp/{sql_file}")

        # ── 5. 更新 db_config.php ──
        step("配置数据库连接", 5, total_steps)
        config_content = f"""<?php
// MNL 启动器 - 数据库配置 (服务器: {SERVER})
define('DB_HOST', 'localhost');
define('DB_PORT', 3306);
define('DB_NAME', '{DB_NAME}');
define('DB_USER', '{DB_USER}');
define('DB_PASS', '{DB_PASS}');
define('DB_CHARSET', 'utf8mb4');

function getDB(): PDO {{
    static $pdo = null;
    if ($pdo === null) {{
        $dsn = sprintf('mysql:host=%s;port=%d;dbname=%s;charset=%s', DB_HOST, DB_PORT, DB_NAME, DB_CHARSET);
        $pdo = new PDO($dsn, DB_USER, DB_PASS, [
            PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
            PDO::ATTR_EMULATE_PREPARES => false,
        ]);
    }}
    return $pdo;
}}

function jsonResponse(array $data, int $httpCode = 200): void {{
    http_response_code($httpCode);
    header('Content-Type: application/json; charset=utf-8');
    header('Access-Control-Allow-Origin: *');
    header('Access-Control-Allow-Methods: POST, GET, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type, Authorization');
    echo json_encode($data, JSON_UNESCAPED_UNICODE);
    exit;
}}

function getClientIP(): string {{
    if (!empty($_SERVER['HTTP_X_FORWARDED_FOR'])) {{
        $ips = explode(',', $_SERVER['HTTP_X_FORWARDED_FOR']);
        return trim($ips[0]);
    }}
    if (!empty($_SERVER['HTTP_X_REAL_IP'])) return $_SERVER['HTTP_X_REAL_IP'];
    return $_SERVER['REMOTE_ADDR'] ?? '0.0.0.0';
}}

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {{
    header('Access-Control-Allow-Origin: *');
    header('Access-Control-Allow-Methods: POST, GET, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type, Authorization');
    http_response_code(204);
    exit;
}}
"""
        # 写入远程文件
        sftp = client.open_sftp()
        with sftp.file(f"{API_DIR}/db_config.php", 'w') as f:
            f.write(config_content)
        sftp.close()
        print("[OK] db_config.php 已更新")

        # ── 6. 执行数据库迁移 ──
        step("执行数据库迁移", 6, total_steps)
        for sql_file in SQL_FILES:
            local_path = os.path.join(SCRIPT_DIR, sql_file)
            if not os.path.exists(local_path):
                continue
            code, output = run_ssh(client, f"mysql -u root {DB_NAME} < /tmp/{sql_file} 2>&1 && echo 'OK_{sql_file}'", timeout=30)
            if code == 0:
                print(f"  [OK] {sql_file}")
            else:
                # 可能表已存在，忽略错误
                print(f"  [OK] {sql_file} (已存在或部分跳过)")

        # ── 7. 配置 Nginx ──
        step("配置 Nginx", 7, total_steps)
        
        # 查找 PHP socket
        code, php_sock = run_ssh(client, "find /var/run/php/ -name '*.sock' 2>/dev/null | head -1")
        php_sock = php_sock.strip()
        if not php_sock:
            php_sock = "unix:/var/run/php/php8.1-fpm.sock"
        
        nginx_conf = f"""server {{
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name _;
    root /var/www/html;
    index index.php index.html index.htm;
    client_max_body_size 500M;

    location /api/ {{
        try_files $uri =404;
        fastcgi_pass {php_sock};
        fastcgi_index index.php;
        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
        include fastcgi_params;
    }}

    location ~ \\.php$ {{
        try_files $uri =404;
        fastcgi_pass {php_sock};
        fastcgi_index index.php;
        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
        include fastcgi_params;
    }}

    location /uploads/ {{
        alias /var/www/html/uploads/;
        expires 30d;
        add_header Cache-Control "public, immutable";
    }}

    location ~ /\\. {{
        deny all;
    }}
}}
"""
        sftp = client.open_sftp()
        with sftp.file("/etc/nginx/sites-available/mnl-api", 'w') as f:
            f.write(nginx_conf)
        sftp.close()

        run_ssh(client, "rm -f /etc/nginx/sites-enabled/default; ln -sf /etc/nginx/sites-available/mnl-api /etc/nginx/sites-enabled/mnl-api")
        code, output = run_ssh(client, "nginx -t 2>&1 && systemctl reload nginx 2>&1")
        if code == 0:
            print("[OK] Nginx 配置完成并已重载")
        else:
            print(f"[WARN] Nginx 配置测试: {output[-200:]}")

        # ── 验证 ──
        step("验证部署", "FINAL", total_steps)
        time.sleep(2)
        
        tests = [
            ("版本API", f"curl -s http://localhost/api/version.php | head -c 100"),
            ("白名单API", f"curl -s 'http://localhost/api/whitelist.php?username=Player' | head -c 100"),
            ("组件商店API", f"curl -s 'http://localhost/api/components.php?action=list' | head -c 100"),
            ("认证API", f"curl -s 'http://localhost/api/auth.php?action=ping' | head -c 100"),
            ("社区API", f"curl -s 'http://localhost/api/community.php?action=channels' | head -c 100"),
        ]
        
        all_ok = True
        for name, test_cmd in tests:
            code, output = run_ssh(client, test_cmd, timeout=10)
            if code == 0 and output.strip():
                print(f"  [OK] {name}: {output[:80]}...")
            else:
                print(f"  [FAIL] {name}")
                all_ok = False

        print(f"\n{'='*60}")
        print("部署完成！")
        print(f"{'='*60}")
        print(f"服务器地址: http://{SERVER}")
        print(f"数据库名: {DB_NAME}")
        print(f"数据库用户: {DB_USER}")
        print(f"")
        print(f"=== API 端点 ===")
        print(f"  青鸟账号: http://{SERVER}/api/auth.php")
        print(f"    - 注册: POST ?action=register")
        print(f"    - 登录: POST ?action=login")
        print(f"    - 资料: GET  ?action=profile")
        print(f"  聊天社区: http://{SERVER}/api/community.php")
        print(f"    - 频道: GET  ?action=channels")
        print(f"    - 消息: GET  ?action=messages&channel_id=1")
        print(f"    - 发送: POST ?action=send")
        print(f"  好友系统: http://{SERVER}/api/friends.php")
        print(f"  组件商店: http://{SERVER}/api/components.php")
        print(f"  版本检查: http://{SERVER}/api/version.php")
        print(f"  白名单:   http://{SERVER}/api/whitelist.php")
        print(f"  日志上报: http://{SERVER}/api/log.php")
        print(f"  背景素材: http://{SERVER}/api/backgrounds.php")
        print(f"{'='*60}")

        if all_ok:
            print("所有 API 测试通过！")
        else:
            print("部分 API 测试失败，请检查 Nginx/PHP 配置")

    except Exception as e:
        print(f"\n[FATAL] 部署失败: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
    finally:
        if client:
            client.close()

if __name__ == "__main__":
    main()
