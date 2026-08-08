#!/usr/bin/env python3
"""Fix auth.php: add error handling to updateProfile using sed"""
import paramiko

ssh = paramiko.SSHClient()
ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
ssh.connect('85.137.246.87', username='root', password='5V1T28sB8PmZ', timeout=10)

# Show the exact lines around the SQL execution in updateProfile
stdin, stdout, stderr = ssh.exec_command("grep -n 'pdo->prepare.*execute\|jsonResponse.*success\|updateProfile' /var/www/html/api/auth.php")
print("=== Matching lines ===")
print(stdout.read().decode())

ssh.close()
