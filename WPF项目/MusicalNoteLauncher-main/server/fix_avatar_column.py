#!/usr/bin/env python3
"""Fix avatar_url column on server"""
import paramiko

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect('85.137.246.87', username='root', password='5V1T28sB8PmZ', timeout=10)

# Check current column type
stdin, stdout, stderr = ssh.exec_command(
    'mysql -u root -p5V1T28sB8PmZ mnl_launcher -e "SHOW COLUMNS FROM users LIKE \'avatar_url\';" 2>&1'
)
print("=== Current avatar_url column ===")
print(stdout.read().decode())
print(stderr.read().decode())

# Alter to MEDIUMTEXT
stdin, stdout, stderr = ssh.exec_command(
    'mysql -u root -p5V1T28sB8PmZ mnl_launcher -e "ALTER TABLE users MODIFY COLUMN avatar_url MEDIUMTEXT;" 2>&1'
)
print("=== ALTER result ===")
out = stdout.read().decode()
err = stderr.read().decode()
print(out)
print(err)

if not err or "Warning" in err:
    print("SUCCESS: avatar_url column changed to MEDIUMTEXT")
else:
    print("FAILED:", err)

ssh.close()
