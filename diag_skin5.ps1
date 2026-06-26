Add-Type -AssemblyName System.Drawing
$files = @((Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\steve.png'), (Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\alex.png'))
foreach ($f in $files) {
    $bmp = [System.Drawing.Bitmap]::FromFile($f)
    $name = [System.IO.Path]::GetFileName($f)
    Write-Host "=== $name ($($bmp.Width)x$($bmp.Height)) ==="
    # 检查关键区域
    $checks = @()
    $checks += ,@('RIGHT_ARM_inner', 40, 16, 16, 16)
    $checks += ,@('RIGHT_ARM_outer_lower', 40, 32, 16, 16)
    $checks += ,@('LEFT_ARM_inner', 32, 48, 16, 16)
    $checks += ,@('BODY_skin', 16, 16, 24, 16)
    $checks += ,@('BODY_jacket', 16, 32, 24, 16)
    $checks += ,@('RIGHT_LEG_pants', 0, 32, 16, 16)
    foreach ($ch in $checks) {
        $label = $ch[0]; $x=$ch[1]; $y=$ch[2]; $w=$ch[3]; $h=$ch[4]
        $firstC = $bmp.GetPixel($x, $y)
        $midC = $bmp.GetPixel($x + $w/2, $y + $h/2)
        Write-Host " $label ($x,$y) first_pixel A=$($firstC.A) RGB=($($firstC.R),$($firstC.G),$($firstC.B))"
        Write-Host "            mid_pixel   A=$($midC.A) RGB=($($midC.R),$($midC.G),$($midC.B))"
    }
    $bmp.Dispose()
}
