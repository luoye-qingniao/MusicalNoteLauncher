#!/usr/bin/env python3
# ============================================
# MNL 背景上传功能 - 远程一键部署脚本
# 在当前机器上执行，自动通过 SSH 部署到远程服务器
# ============================================
import subprocess
import os
import sys
import tempfile

# ── 配置 ──
SERVER = "192.168.100.106"
USER = "xyyd"
PASSWORD = "zsq13892152486"
WEB_ROOT = "/var/www/html"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SQL_FILE = os.path.join(SCRIPT_DIR, "migration_v3.sql")
API_FILE = os.path.join(SCRIPT_DIR, "api", "backgrounds.php")

def run_ssh(cmd, check=True):
    """通过 SSH 执行远程命令，自动输入密码"""
    full_cmd = f'ssh -o StrictHostKeyChecking=no -o PreferredAuthentications=password -o PubkeyAuthentication=no {USER}@{SERVER} "{cmd}"'
    proc = subprocess.Popen(
        full_cmd, shell=True, stdin=subprocess.PIPE,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )
    stdout, stderr = proc.communicate(input=f"{PASSWORD}\n".encode(), timeout=30)
    output = stdout.decode('utf-8', errors='replace') + stderr.decode('utf-8', errors='replace')
    if check and proc.returncode != 0:
        print(f"[FAIL] 命令执行失败 (exit={proc.returncode})")
        print(output[-500:])
        return False, output
    return True, output

def run_scp(local_path, remote_path):
    """通过 SCP 复制文件到远程服务器"""
    full_cmd = f'scp -o StrictHostKeyChecking=no -o PreferredAuthentications=password -o PubkeyAuthentication=no "{local_path}" {USER}@{SERVER}:"{remote_path}"'
    proc = subprocess.Popen(
        full_cmd, shell=True, stdin=subprocess.PIPE,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )
    stdout, stderr = proc.communicate(input=f"{PASSWORD}\n".encode(), timeout=60)
    if proc.returncode != 0:
        output = stderr.decode('utf-8', errors='replace')
        print(f"[FAIL] SCP 上传失败: {output[-300:]}")
        return False
    return True

def main():
    print("=" * 50)
    print("MNL 背景上传功能 - 远程部署")
    print(f"目标服务器: {USER}@{SERVER}")
    print(f"网站根目录: {WEB_ROOT}")
    print("=" * 50)
    
    # 验证文件存在
    if not os.path.exists(API_FILE):
        print(f"[ERROR] 找不到 {API_FILE}")
        sys.exit(1)
    if not os.path.exists(SQL_FILE):
        print(f"[ERROR] 找不到 {SQL_FILE}")
        sys.exit(1)
    
    # ── 1. 测试 SSH 连接 ──
    print("\n[1/5] 测试 SSH 连接...")
    ok, output = run_ssh("whoami && uname -a")
    if not ok:
        print("[FATAL] SSH 连接失败，请检查用户名密码和网络")
        sys.exit(1)
    print(f"[OK] SSH 连接成功: {output.strip().split(chr(10))[0]}")

    # ── 2. 上传 backgrounds.php ──
    print("\n[2/5] 上传 backgrounds.php...")
    remote_api_file = f"{WEB_ROOT}/api/backgrounds.php"
    if run_scp(API_FILE, remote_api_file):
        print(f"[OK] backgrounds.php 已上传")
    else:
        print("[FATAL] 文件上传失败")
        sys.exit(1)

    # ── 3. 创建上传目录 ──
    print("\n[3/5] 创建上传目录...")
    ok, output = run_ssh(f"mkdir -p {WEB_ROOT}/uploads/backgrounds && chmod 755 {WEB_ROOT}/uploads/backgrounds && echo 'OK'")
    if ok:
        print(f"[OK] 上传目录已就绪: {WEB_ROOT}/uploads/backgrounds")
    else:
        print("[WARN] 创建目录可能失败，请手动检查")

    # ── 4. 上传并执行 SQL 迁移 ──
    print("\n[4/5] 执行数据库迁移...")
    remote_sql = "/tmp/migration_v3.sql"
    if not run_scp(SQL_FILE, remote_sql):
        print("[FATAL] SQL 文件上传失败")
        sys.exit(1)
    
    # 通过 PHP 执行 SQL（因为 MySQL 只允许本地连接）
    php_cmd = (
        f"php -r \""
        f"\\$sql = file_get_contents('{remote_sql}');"
        f"\\$pdo = new PDO('mysql:host=localhost;dbname=mnl_launcher;charset=utf8mb4', 'root', 'zsq13892152486');"
        f"\\$pdo->exec(\\$sql);"
        f"echo 'OK';"
        f"\""
    )
    ok, output = run_ssh(php_cmd)
    if ok and "OK" in output:
        print("[OK] 数据库 migration_v3.sql 执行成功")
    else:
        print(f"[WARN] PHP 方式执行失败，尝试 mysql 命令行...")
        # 尝试直接 mysql 命令
        ok2, _ = run_ssh(
            f"mysql -u root -p'zsq13892152486' mnl_launcher < {remote_sql} 2>&1 && echo 'OK'"
        )
        if ok2:
            print("[OK] 数据库迁移执行成功")
        else:
            print("[WARN] 自动执行失败，请手动执行 migration_v3.sql")
            print(f"      文件已在服务器: {remote_sql}")
            print(f"      命令: mysql -u root -p mnl_launcher < {remote_sql}")
    
    # 清理临时文件
    run_ssh(f"rm -f {remote_sql}", check=False)

    # ── 5. 验证 API ──
    print("\n[5/5] 验证 API...")
    ok, output = run_ssh("curl -s http://localhost/api/backgrounds.php?action=list 2>&1 | head -c 200")
    if ok and '"backgrounds"' in output:
        print(f"[OK] API 响应正常")
        print(f"     响应: {output[:150]}")
    else:
        print(f"[WARN] API 验证未通过，请检查 PHP 服务")

    print("\n" + "=" * 50)
    print("部署完成！")
    print(f"背景 API 地址: http://{SERVER}:8080/api/backgrounds.php")
    print("=" * 50)

if __name__ == "__main__":
    main()
