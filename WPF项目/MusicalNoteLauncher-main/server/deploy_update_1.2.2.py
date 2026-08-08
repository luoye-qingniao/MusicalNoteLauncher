#!/usr/bin/env python3
# ============================================
# MNL 启动器 v1.2.2 更新部署
# ============================================
import os
import paramiko

SERVER = "85.137.246.87"
USER = "root"
PASSWORD = "5V1T28sB8PmZ"
PORT = 22

VERSION = "1.2.2"
VERSION_CODE = 5
ZIP_FILE = "MNL-1.2.2-update.zip"
DOWNLOAD_URL = f"http://{SERVER}/updates/{ZIP_FILE}"
FILE_HASH = "1efec6aac38c96047d9fc25fa2cd2073b6f7a2b98d4032f3e59e11df8ce1158b"
FILE_SIZE = 100439667

RELEASE_NOTES = """v1.2.2 更新内容：
- 修复下载功能完全失效
- 修复模组下载失败
- 修复搜索不到资源时静默失败
- 修复游戏版本无法启动
- 修复无法启动 Fabric、NeoForge、Forge 游戏版本
- 修复无法下载整合包
- 修复不能切换模组加载器
- 修复会把 UTF-8 错误解码为 GBK
- 修复对 Java 21 的参数文件编码处理存在明确缺陷"""

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ZIP_PATH = os.path.join(SCRIPT_DIR, "..", ZIP_FILE)

def main():
    if not os.path.exists(ZIP_PATH):
        print(f"[ERROR] 找不到更新包: {ZIP_PATH}")
        return

    print("=" * 60)
    print(f"MNL 启动器 v{VERSION} 更新部署")
    print(f"目标: {USER}@{SERVER}")
    print("=" * 60)

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    
    try:
        # 连接
        print("[1/4] 连接服务器...")
        client.connect(SERVER, PORT, USER, PASSWORD, timeout=30,
                       look_for_keys=False, allow_agent=False)
        print("[OK] 已连接")

        # 创建目录
        client.exec_command("mkdir -p /var/www/html/updates")

        # 上传
        print(f"[2/4] 上传 {ZIP_FILE} ({FILE_SIZE/1024/1024:.1f} MB)...")
        sftp = client.open_sftp()
        sftp.put(ZIP_PATH, f"/var/www/html/updates/{ZIP_FILE}")
        sftp.close()
        print("[OK] 上传完成")

        # 更新数据库
        print("[3/4] 更新数据库版本记录...")
        
        # 写 SQL 到文件
        notes_one_line = RELEASE_NOTES.replace("\n", " ").replace("'", "''")
        
        sql_script = f"""USE mnl_launcher;
DELETE FROM launcher_versions WHERE version = '{VERSION}';
UPDATE launcher_versions SET is_active = 0;
INSERT INTO launcher_versions (version, version_code, download_url, file_hash, file_size, release_notes, is_forced, min_launcher_version, is_active) VALUES ('{VERSION}', {VERSION_CODE}, '{DOWNLOAD_URL}', '{FILE_HASH}', {FILE_SIZE}, '{notes_one_line}', 0, '', 1);
"""
        sftp = client.open_sftp()
        with sftp.file("/tmp/deploy_update.sql", 'w') as f:
            f.write(sql_script)
        sftp.close()
        
        stdin, stdout, stderr = client.exec_command("mysql -u root < /tmp/deploy_update.sql 2>&1; echo 'EXIT_CODE:'$?")
        out = stdout.read().decode('utf-8', errors='replace')
        err = stderr.read().decode('utf-8', errors='replace')
        exit_found = "EXIT_CODE:0" in out
        has_error = "ERROR" in err or "ERROR" in out
        if exit_found and not has_error:
            print("[OK] 数据库已更新")
        else:
            print(f"[DBG] stdout: {out.strip()[-200:]}")
            print(f"[DBG] stderr: {err.strip()[-200:]}")
        client.exec_command("rm -f /tmp/deploy_update.sql")

        # 验证
        print("[4/4] 验证版本 API...")
        stdin, stdout, stderr = client.exec_command(
            f"curl -s 'http://localhost/api/version.php?current_version=1.2.1' | head -c 200")
        out = stdout.read().decode('utf-8', errors='replace')
        print(f"  API 响应: {out}")

        print("\n" + "=" * 60)
        print(f"部署完成！v{VERSION} 已上线")
        print(f"下载地址: {DOWNLOAD_URL}")
        print("=" * 60)

    except Exception as e:
        print(f"[FATAL] 部署失败: {e}")
        import traceback
        traceback.print_exc()
    finally:
        client.close()

if __name__ == "__main__":
    main()
