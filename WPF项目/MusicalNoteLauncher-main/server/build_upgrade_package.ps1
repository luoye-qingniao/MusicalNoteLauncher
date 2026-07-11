# ============================================
# MNL 启动器 - 升级包构建脚本
# 用法: .\build_upgrade_package.ps1 -Version "1.0.1"
# ============================================

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [string]$OutputDir = "$PSScriptRoot\updates",
    [string]$SourceDir = (Resolve-Path "$PSScriptRoot\..\bin\Release\net8.0-windows\publish")
)

$ErrorActionPreference = "Stop"

$packageName = "MusicalNoteLauncher_${Version}.zip"
$outputPath = Join-Path $OutputDir $packageName

# 确保输出目录存在
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " MNL 启动器升级包构建工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "版本号: $Version"
Write-Host "源码目录: $SourceDir"
Write-Host "输出文件: $outputPath"
Write-Host ""

# 1. 发布项目
Write-Host "[1/4] 发布项目..." -ForegroundColor Yellow
$projPath = Resolve-Path "$PSScriptRoot\..\MusicalNoteLauncher.csproj"
dotnet publish $projPath -c Release -o $SourceDir --self-contained false | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "发布失败！" -ForegroundColor Red
    exit 1
}
Write-Host "  发布完成" -ForegroundColor Green

# 2. 打包为 ZIP
Write-Host "[2/4] 打包升级包..." -ForegroundColor Yellow
if (Test-Path $outputPath) {
    Remove-Item $outputPath -Force
}

Compress-Archive -Path "$SourceDir\*" -DestinationPath $outputPath -Force
Write-Host "  打包完成: $outputPath" -ForegroundColor Green

# 3. 计算 SHA256
Write-Host "[3/4] 计算 SHA256..." -ForegroundColor Yellow
$hash = (Get-FileHash -Path $outputPath -Algorithm SHA256).Hash.ToLower()
$fileSize = (Get-Item $outputPath).Length
Write-Host "  SHA256: $hash" -ForegroundColor Green
Write-Host "  文件大小: $fileSize 字节 ($([math]::Round($fileSize/1MB, 2)) MB)" -ForegroundColor Green

# 4. 输出部署信息
Write-Host "[4/4] 生成部署信息..." -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " 升级包已就绪！请将以下信息填入服务器" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "文件: $packageName" -ForegroundColor White
Write-Host "SHA256: $hash" -ForegroundColor White
Write-Host "大小: $fileSize" -ForegroundColor White
Write-Host ""
Write-Host "部署步骤:" -ForegroundColor Yellow
Write-Host "  1. 将 $packageName 上传到服务器可访问的目录" -ForegroundColor White
Write-Host "  2. 在 MySQL 中执行以下 SQL 来注册新版本:" -ForegroundColor White
Write-Host ""
Write-Host "  USE mnl_launcher;" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  -- 先将旧版本设为非活跃" -ForegroundColor DarkGray
Write-Host "  UPDATE launcher_versions SET is_active = 0;" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  -- 插入新版本（请替换 DOWNLOAD_URL 为实际地址）" -ForegroundColor DarkGray
Write-Host "  INSERT INTO launcher_versions" -ForegroundColor DarkGray
Write-Host "    (version, version_code, download_url, file_hash, file_size," -ForegroundColor DarkGray
Write-Host "     release_notes, is_forced, min_launcher_version, is_active)" -ForegroundColor DarkGray
Write-Host "  VALUES" -ForegroundColor DarkGray
Write-Host "    ('$Version', $([int]($Version -replace '\.','')), 'http://YOUR_SERVER/updates/$packageName'," -ForegroundColor DarkGray
Write-Host "     '$hash', $fileSize," -ForegroundColor DarkGray
Write-Host "     '新版本 $Version 更新内容', 0, '', 1);" -ForegroundColor DarkGray

# 保存配置到 JSON 文件
$configJson = @{
    version = $Version
    package_file = $packageName
    sha256 = $hash
    file_size = $fileSize
    build_time = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
} | ConvertTo-Json

$configPath = Join-Path $OutputDir "version_${Version}_config.json"
$configJson | Out-File -FilePath $configPath -Encoding utf8
Write-Host ""
Write-Host "部署配置文件已保存到: $configPath" -ForegroundColor Green
Write-Host ""
