$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $projectRoot "dist-electron"
$publishBase = Join-Path $projectRoot "MNL.Backend" "bin" "Release" "net8.0"
$cacheDir = Join-Path $PSScriptRoot ".electron-cache"
$electronVersion = "28.3.3"

Write-Host "=== MNL 三平台打包脚本 ==="

# ── 步骤 1: 编译所有平台后端 ──
Write-Host "`n[1/4] 编译后端..."
foreach ($rid in @("win-x64", "linux-x64", "osx-x64")) {
    $publishDir = Join-Path $publishBase $rid "publish"
    $exeName = if ($rid -eq "win-x64") { "MNL.Backend.exe" } else { "MNL.Backend" }
    $exePath = Join-Path $publishDir $exeName
    if (-not (Test-Path $exePath)) {
        Write-Host "  编译 $rid ..."
        Push-Location (Join-Path $projectRoot "MNL.Backend")
        dotnet publish -c Release -r $rid --self-contained true *>$null
        Pop-Location
    } else {
        Write-Host "  $rid 已存在，跳过编译"
    }
}

# ── 步骤 2: 下载 Electron 二进制 (如果还没下载) ──
Write-Host "`n[2/4] 检查 Electron 二进制..."
$electronUrls = @{
    "linux" = "https://github.com/electron/electron/releases/download/v$electronVersion/electron-v$electronVersion-linux-x64.zip"
    "darwin" = "https://github.com/electron/electron/releases/download/v$electronVersion/electron-v$electronVersion-darwin-x64.zip"
}
foreach ($platform in $electronUrls.Keys) {
    $zipPath = Join-Path $cacheDir "electron-v$electronVersion-$platform-x64.zip"
    if (-not (Test-Path $zipPath)) {
        Write-Host "  下载 electron-$platform ..."
        Invoke-WebRequest -Uri $electronUrls[$platform] -OutFile $zipPath -UseBasicParsing
    } else {
        Write-Host "  electron-$platform 已缓存"
    }
}

# ── 步骤 3: 组装各平台 ──
Write-Host "`n[3/4] 组装各平台..."

# ── Windows ──
function Build-Windows {
    $outDir = Join-Path $distRoot "win-unpacked"
    $electronSrc = Join-Path $PSScriptRoot "node_modules" "electron" "dist"
    if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
    
    Write-Host "  组装 Windows..."
    New-Item -ItemType Directory -Force $outDir | Out-Null
    Copy-Item "$electronSrc\*" $outDir -Recurse -Exclude "LICENSE","LICENSES.chromium.html" -Force
    Rename-Item "$outDir\electron.exe" "MusicalNoteLauncher.exe" -Force
    
    # 先删掉 electron 自带 resources, 重新创建
    $appDir = "$outDir\resources\app"
    New-Item -ItemType Directory -Force $appDir | Out-Null
    Copy-Item "$PSScriptRoot\main.js" $appDir
    Copy-Item "$PSScriptRoot\preload.js" $appDir
    Copy-Item "$PSScriptRoot\package.json" $appDir
    New-Item -ItemType Directory -Force "$outDir\resources\Assets" | Out-Null
    Copy-Item (Join-Path $projectRoot "Assets" "图标.ico") "$outDir\resources\Assets\" -Force -ErrorAction SilentlyContinue
    
    $backendSrc = Join-Path $publishBase "win-x64" "publish"
    xcopy "$backendSrc\*" "$outDir\resources\backend\" /E /Y /Q
    
    # 嵌入图标和版本信息 (rcedit 不支持中文路径，用英文临时副本)
    $rceditTool = Join-Path $PSScriptRoot "node_modules" "rcedit" "bin" "rcedit-x64.exe"
    $icoSrc = Join-Path $projectRoot "Assets" "图标.ico"
    $icoTmp = Join-Path $env:TEMP "mnl-app-icon.ico"
    if (Test-Path $rceditTool) {
        Copy-Item $icoSrc $icoTmp -Force
        & $rceditTool "$outDir\MusicalNoteLauncher.exe" --set-icon $icoTmp *>$null
        & $rceditTool "$outDir\MusicalNoteLauncher.exe" --set-file-version "1.2.1.0" --set-product-version "1.2.1" --set-version-string FileDescription "MusicalNoteLauncher" --set-version-string ProductName "MusicalNoteLauncher" *>$null
        Remove-Item $icoTmp -Force -ErrorAction SilentlyContinue
    }
    Write-Host "  Windows OK"
}

# ── Linux ──
function Build-Linux {
    $outDir = Join-Path $distRoot "linux-unpacked"
    $zipPath = Join-Path $cacheDir "electron-v$electronVersion-linux-x64.zip"
    if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
    
    Write-Host "  组装 Linux..."
    New-Item -ItemType Directory -Force $outDir | Out-Null
    
    # 解压 electron
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $outDir)
    
    # 创建 resources/app
    $appDir = "$outDir\resources\app"
    New-Item -ItemType Directory -Force $appDir | Out-Null
    Copy-Item "$PSScriptRoot\main.js" $appDir
    Copy-Item "$PSScriptRoot\preload.js" $appDir
    Copy-Item "$PSScriptRoot\package.json" $appDir
    
    $backendSrc = Join-Path $publishBase "linux-x64" "publish"
    xcopy "$backendSrc\*" "$outDir\resources\backend\" /E /Y /Q
    Rename-Item "$outDir\electron" "MusicalNoteLauncher" -Force -ErrorAction SilentlyContinue
    Write-Host "  Linux OK"
}

# ── macOS ──
function Build-macOS {
    $baseOut = Join-Path $distRoot "mac-unpacked"
    $zipPath = Join-Path $cacheDir "electron-v$electronVersion-darwin-x64.zip"
    if (Test-Path $baseOut) { Remove-Item -Recurse -Force $baseOut }
    
    Write-Host "  组装 macOS..."
    New-Item -ItemType Directory -Force $baseOut | Out-Null
    
    # 解压到临时目录
    $tmpDir = Join-Path $baseOut "_tmp"
    New-Item -ItemType Directory -Force $tmpDir | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $tmpDir)
    
    # 重命名 Electron.app
    $appBundle = "$baseOut\MusicalNoteLauncher.app"
    Move-Item "$tmpDir\Electron.app" $appBundle
    Remove-Item -Recurse -Force $tmpDir
    
    # 放置 app 文件
    $appDir = "$appBundle\Contents\Resources\app"
    New-Item -ItemType Directory -Force $appDir | Out-Null
    Copy-Item "$PSScriptRoot\main.js" $appDir
    Copy-Item "$PSScriptRoot\preload.js" $appDir
    Copy-Item "$PSScriptRoot\package.json" $appDir
    
    # 放置后端
    $backendSrc = Join-Path $publishBase "osx-x64" "publish"
    $backendDest = "$appBundle\Contents\Resources\backend"
    xcopy "$backendSrc\*" $backendDest\ /E /Y /Q
    
    # 更新 Info.plist (改名称)
    $plistPath = "$appBundle\Contents\Info.plist"
    if (Test-Path $plistPath) {
        $plist = Get-Content $plistPath -Raw
        $plist = $plist -replace '<string>Electron</string>', '<string>MusicalNoteLauncher</string>'
        $plist = $plist -replace '<string>com.github.electron</string>', '<string>com.mnl.launcher</string>'
        Set-Content $plistPath $plist -Encoding UTF8
    }
    
    Write-Host "  macOS OK"
}

Build-Windows
Build-Linux
Build-macOS

# ── 步骤 4: 汇总 ──
Write-Host "`n[4/4] 打包完成!`n"
foreach ($dir in @("win-unpacked", "linux-unpacked", "mac-unpacked")) {
    $path = Join-Path $distRoot $dir
    if (Test-Path $path) {
        $size = (Get-ChildItem $path -Recurse -File | Measure-Object -Property Length -Sum).Sum
        Write-Host "  $dir : $([math]::Round($size / 1MB, 2)) MB"
    }
}
