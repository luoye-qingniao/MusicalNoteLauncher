#!/usr/bin/env python3
# ============================================
# MNL 启动器 v1.2.1 更新部署
# ============================================
import os
import paramiko

SERVER = "85.137.246.87"
USER = "root"
PASSWORD = "5V1T28sB8PmZ"
PORT = 22

VERSION = "1.2.1"
VERSION_CODE = 4
ZIP_FILE = "MNL-1.2.1-update.zip"
DOWNLOAD_URL = f"http://{SERVER}/updates/{ZIP_FILE}"
FILE_HASH = "fa89dc6f72ea756217656ad2036daaa792cb7bdebef8c27f9987d9e3559f4622"
FILE_SIZE = 100357504

RELEASE_NOTES = """v1.2.1 更新内容：
- 修复部分自定义名称游戏版本无法启动
- 新增国内整合包、BBSMC 两个下载选项
- AI 助手更名为「音涟」，新增 AI Agent 功能（不稳定）
- 修复非标准名称文件夹无法添加
- 修复模组管理界面过多模组重叠
- 修复游戏版本下载未真正执行安装
- 新增基岩版下载入口
- 新增多套自定义 JVM 参数预设
- 完善感谢名单"""

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
            f"curl -s 'http://localhost/api/version.php?current_version=1.2.0' | head -c 200")
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
