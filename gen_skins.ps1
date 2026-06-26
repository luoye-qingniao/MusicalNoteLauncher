# 生成正确的 Steve 和 Alex 皮肤文件 —— 所有区域都填充颜色，没有透明像素
Add-Type -AssemblyName System.Drawing

function Fill-Rect {
    param($bmp, $x, $y, $w, $h, $r, $g, $b)
    for ($yy = 0; $yy -lt $h; $yy++) {
        for ($xx = 0; $xx -lt $w; $xx++) {
            $bmp.SetPixel($x + $xx, $y + $yy, [System.Drawing.Color]::FromArgb(255, $r, $g, $b))
        }
    }
}

# 为每个身体部位设置颜色（基于 Minecraft 标准皮肤调色板
# HEAD_FACE: 皮肤色
# HEAD_HAT: 深色头发/帽子
# BODY: 衣服色
# BODY_OUTER: 衣服外层（稍深或相同）
# RIGHT_ARM/LEFT_ARM: 手臂（与身体相同或稍深）
# ARMS_OUTER: 外层手臂
# LEGS/LEGS_OUTER: 腿/裤腿

function Write-Skin {
    param($outPath, $skinTone, $skinToneDark, $shirt, $shirtDark, $pants, $pantsDark, $hair)
    $bmp = New-Object System.Drawing.Bitmap(64, 64)

    # ==== 内层（内层头 (y=0..15) ====
    # 头 - 顶/底/左/右 以及前后面需要正确映射，所以我们直接在整个 (0,0)-(32,16) 区域填充
    # 注意：Minecraft 皮肤标准布局 头 (0,0,32,16)：
    #   x=0..7, y=0..7   => 顶面（上方）
    #   x=8..15, y=0..7  => 底面（下方）
    #   x=16..23, y=0..7 => 前面（前面）
    #   x=24..31, y=0..7 => 后面（后方）
    #   x=0..7, y=8..15 => 右侧
    #   x=8..15, y=8..15 => 前面（脸）
    #   x=16..23, y=8..15 => 后面
    #   x=24..31, y=8..15 => 左侧
    # 简化方式：直接填充整个区域以避免空像素

    # 头：整体底色 —— 先填皮肤色
    Fill-Rect $bmp 0 0 32 16 $skinTone[0] $skinTone[1] $skinTone[2]
    # 面部细节（眼睛 + 嘴巴）
    # 眼睛 - 左眼（x=16+1, y=8+2）& 右眼（x=16+5, y=8+2）
    # 白色背景已设置为皮肤色
    # 现在填充帽子 (外层头) (32,0,32,16)
    # 整体底色 - fill with hair 深色
    Fill-Rect $bmp 32 0 32 16 $hair[0] $hair[1] $hair[2]

    # ==== 身体 (16,16,24,16) ====
    Fill-Rect $bmp 16 16 24 16 $shirt[0] $shirt[1] $shirt[2]
    # 身体外层 (16,32,24,16) - 稍深或深色
    Fill-Rect $bmp 16 32 24 16 $shirtDark[0] $shirtDark[1] $shirtDark[2]

    # ==== 腿 (内层) ====
    # 右腿 (0,16,16,16)
    Fill-Rect $bmp 0 16 16 16 $pants[0] $pants[1] $pants[2]
    # 左腿 (16,48,16,16)
    Fill-Rect $bmp 16 48 16 16 $pants[0] $pants[1] $pants[2]
    # 右腿外层 (0,32,16,16)
    Fill-Rect $bmp 0 32 16 16 $pantsDark[0] $pantsDark[1] $pantsDark[2]
    # 左腿外层 (16,48,16,16) 其实不存在。。实际上是 (0,48,16,16)
    # Minecraft 1.8+ 外层左腿在 (0,48,16,16)
    # 但1.8 布局:
    # 左外层腿在 (0,48,16,16)
    Fill-Rect $bmp 0 48 16 16 $pantsDark[0] $pantsDark[1] $pantsDark[2]

    # ==== 手臂 ====
    # 右臂内层 (40,16,16,16)
    Fill-Rect $bmp 40 16 16 16 $shirt[0] $shirt[1] $shirt[2]
    # 左臂内层 (32,48,16,16)
    Fill-Rect $bmp 32 48 16 16 $shirt[0] $shirt[1] $shirt[2]
    # 右臂外层 (40,32,16,16)
    Fill-Rect $bmp 40 32 16 16 $shirtDark[0] $shirtDark[1] $shirtDark[2]
    # 左臂外层 (48,48,16,16)
    Fill-Rect $bmp 48 48 16 16 $shirtDark[0] $shirtDark[1] $shirtDark[2]

    # ==== 添加面部细节 ====
    # 左眼（眼睛位置：x=16+2, y=8+3, w=3, h=3）
    # 眼睛颜色：深灰
    $eyeR = 30; $eyeG = 30; $eyeB = 30
    for ($y = 0; $y -lt 3; $y++) {
        for ($x = 0; $x -lt 3; $x++) {
            $bmp.SetPixel(18 + $x, 11 + $y, [System.Drawing.Color]::FromArgb(255, $eyeR, $eyeG, $eyeB))
        }
    }
    # 右眼位置：(24, 11)
    for ($y = 0; $y -lt 3; $y++) {
        for ($x = 0; $x -lt 3; $x++) {
            $bmp.SetPixel(24 + $x, 11 + $y, [System.Drawing.Color]::FromArgb(255, $eyeR, $eyeG, $eyeB))
        }
    }

    # 保存
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Saved: $outPath"
}

# Steve colors
$steveSkin = @(193, 135, 79)      # 肤色
$steveSkinDark = @(150, 100, 60)
$steveShirt = @(50, 100, 150)       # 青色衬衫
$steveShirtDark = @(30, 70, 120)
$stevePants = @(40, 40, 120)         # 蓝裤子
$stevePantsDark = @(25, 25, 80)
$steveHair = @(50, 30, 10)            # 深棕色头发

$outputRoot = $PSScriptRoot
Write-Skin (Join-Path $outputRoot 'MusicalNoteLauncher-mainssetskinsteve.png') $steveSkin $steveSkinDark $steveShirt $steveShirtDark $stevePants $stevePantsDark $steveHair

# Alex colors
$alexSkin = @(230, 180, 150)        # 较浅肤色
$alexSkinDark = @(190, 140, 110)
$alexShirt = @(60, 110, 80)          # 绿色衬衫
$alexShirtDark = @(40, 80, 60)
$alexPants = @(90, 60, 130)          # 紫色裤子
$alexPantsDark = @(60, 40, 90)
$alexHair = @(200, 70, 70)                # 红棕色头发

Write-Skin (Join-Path $outputRoot 'MusicalNoteLauncher-mainssetskinslex.png') $alexSkin $alexSkinDark $alexShirt $alexShirtDark $alexPants $alexPantsDark $alexHair

Write-Host "Done"
