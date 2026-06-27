# ============================================
# MNL 背景上传功能 - 一键部署脚本
# 在服务器上执行此脚本即可完成全部部署
# ============================================

param(
    [string]$WebRoot = "",          # PHP 网站根目录，如 C:\xampp\htdocs 或 /var/www/html
    [string]$DbHost = "localhost",
    [string]$DbUser = "root",
    [string]$DbPass = ""
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── 1. 获取 WebRoot ──
if ([string]::IsNullOrWhiteSpace($WebRoot)) {
    # 尝试自动检测
    $possiblePaths = @(
        "C:\xampp\htdocs",
        "C:\php\htdocs",
        "/var/www/html",
        "/usr/share/nginx/html"
    )
    foreach ($p in $possiblePaths) {
        if (Test-Path (Join-Path $p "api")) {
            $WebRoot = $p
            break
        }
    }
    if ([string]::IsNullOrWhiteSpace($WebRoot)) {
        $WebRoot = Read-Host "请输入 PHP 网站根目录路径"
    }
}

Write-Host "网站根目录: $WebRoot" -ForegroundColor Cyan

# ── 2. 复制 backgrounds.php ──
$apiDir = Join-Path $WebRoot "api"
if (-not (Test-Path $apiDir)) {
    Write-Host "创建 api 目录: $apiDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $apiDir -Force | Out-Null
}

$sourceApi = Join-Path $scriptDir "api" "backgrounds.php"
$destApi = Join-Path $apiDir "backgrounds.php"
Copy-Item -Path $sourceApi -Destination $destApi -Force
Write-Host "[OK] backgrounds.php 已复制到 $destApi" -ForegroundColor Green

# ── 3. 创建上传目录 ──
$uploadsDir = Join-Path $WebRoot "uploads" "backgrounds"
if (-not (Test-Path $uploadsDir)) {
    New-Item -ItemType Directory -Path $uploadsDir -Force | Out-Null
    Write-Host "[OK] 上传目录已创建: $uploadsDir" -ForegroundColor Green
} else {
    Write-Host "[OK] 上传目录已存在: $uploadsDir" -ForegroundColor Green
}

# ── 4. 获取数据库密码 ──
if ([string]::IsNullOrWhiteSpace($DbPass)) {
    $DbPass = Read-Host "请输入 MySQL root 密码"
}

# ── 5. 执行数据库迁移 ──
$sqlFile = Join-Path $scriptDir "migration_v3.sql"
$sqlContent = Get-Content $sqlFile -Raw

Write-Host "正在执行数据库迁移..." -ForegroundColor Yellow

# 使用 mysql 命令行执行
$mysqlCmd = "mysql"
$env:MYSQL_PWD = $DbPass
$sqlContent | & $mysqlCmd -h $DbHost -u $DbUser --default-character-set=utf8mb4 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] 数据库 migration_v3.sql 执行成功" -ForegroundColor Green
} else {
    # 尝试用 PHP 执行
    Write-Host "mysql 命令失败，尝试通过 PHP 执行..." -ForegroundColor Yellow
    $phpScript = @"
<?php
\$pdo = new PDO('mysql:host=$DbHost;dbname=mnl_launcher;charset=utf8mb4', '$DbUser', '$DbPass');
\$sql = file_get_contents('$sqlFile');
\$pdo->exec(\$sql);
echo "OK";
"@
    $phpScript | php 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] 数据库迁移通过 PHP 执行成功" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] 数据库迁移失败，请手动执行 migration_v3.sql" -ForegroundColor Red
    }
}
Remove-Item Env:\MYSQL_PWD -ErrorAction SilentlyContinue

# ── 6. 验证 ──
$testUrl = "http://localhost/api/backgrounds.php?action=list"
Write-Host "`n验证 API 是否正常..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri $testUrl -UseBasicParsing -TimeoutSec 5
    Write-Host "[OK] API 响应正常: $($response.Content.Substring(0, [Math]::Min(100, $response.Content.Length)))" -ForegroundColor Green
} catch {
    Write-Host "[WARN] API 验证未通过，请检查 PHP 服务是否运行" -ForegroundColor Yellow
}

Write-Host "`n===== 部署完成 =====" -ForegroundColor Green
Write-Host "背景 API 地址: http://你的服务器/api/backgrounds.php" -ForegroundColor Cyan
