Add-Type -AssemblyName System.Drawing
$files = @(
    (Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\steve.png'),
    (Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\alex.png')
)
foreach ($f in $files) {
    $bmp = [System.Drawing.Bitmap]::FromFile($f)
    Write-Host "=== $([System.IO.Path]::GetFileName($f)) ($($bmp.Width)x$($bmp.Height)) ==="
    # 列出一些关键区域
    $regions = @("HEAD_FACE 0,0,32,16", "HEAD_HAT 32,0,32,16", "BODY 16,16,24,16", "RIGHT_LEG 0,16,16,16", "RIGHT_ARM 40,16,16,16", "BODY_OUTER 16,32,24,16", "LEFT_ARM 32,48,16,16", "LEFT_LEG 16,48,16,16", "RIGHT_ARM_OUTER 40,32,16,16", "RIGHT_LEG_OUTER 0,32,16,16")
    foreach ($r in $regions) {
        $parts = $r -split ' '
        $coords = $parts[1] -split ','
        $x=[int]$coords[0]; $y=[int]$coords[1]; $w=[int]$coords[2]; $h=[int]$coords[3]
        $opaque=0; $total=$w*$h
        $sampleColor = $null
        for ($yy=0; $yy -lt $h; $yy++) {
            for ($xx=0; $xx -lt $w; $xx++) {
                $c = $bmp.GetPixel($x+$xx, $y+$yy)
                if ($c.A -gt 0) {
                    $opaque++
                    if ($sampleColor -eq $null) { $sampleColor = $c }
                }
            }
        }
        if ($sampleColor -ne $null) {
            Write-Host " $($parts[0]) ($x,$y,$w,$h): opaque=$opaque/$total sampleColor=R$($sampleColor.R) G$($sampleColor.G) B$($sampleColor.B) A$($sampleColor.A)"
        } else {
            Write-Host " $($parts[0]) ($x,$y,$w,$h): opaque=0 (全透明!)"
        }
    }
    $bmp.Dispose()
}
