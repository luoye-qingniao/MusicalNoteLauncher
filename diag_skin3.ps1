Add-Type -AssemblyName System.Drawing
$files = @((Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\steve.png'), (Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\alex.png'))
foreach ($f in $files) {
    $bmp = [System.Drawing.Bitmap]::FromFile($f)
    $name = [System.IO.Path]::GetFileName($f)
    Write-Host "=== $name ($($bmp.Width)x$($bmp.Height)) ==="
    $tests = @{}
    $tests['HEAD_FACE'] = @(0,0,32,16)
    $tests['HEAD_HAT'] = @(32,0,32,16)
    $tests['BODY'] = @(16,16,24,16)
    $tests['RIGHT_LEG'] = @(0,16,16,16)
    $tests['RIGHT_ARM'] = @(40,16,16,16)
    $tests['BODY_OUTER'] = @(16,32,24,16)
    $tests['LEFT_ARM'] = @(32,48,16,16)
    $tests['LEFT_LEG'] = @(16,48,16,16)
    $tests['RIGHT_ARM_OUTER'] = @(40,32,16,16)
    $tests['RIGHT_LEG_OUTER'] = @(0,32,16,16)
    foreach ($key in $tests.Keys) {
        $c = $tests[$key]
        $x=$c[0]; $y=$c[1]; $w=$c[2]; $h=$c[3]
        $opaque=0; $total=$w*$h; $sampleColor = $null
        for ($yy=0; $yy -lt $h; $yy++) { for ($xx=0; $xx -lt $w; $xx++) { $cc = $bmp.GetPixel($x+$xx, $y+$yy); if ($cc.A -gt 0) { $opaque++; if ($sampleColor -eq $null) { $sampleColor = $cc } } } }
        if ($sampleColor -ne $null) { Write-Host " $key ($x,$y,$w,$h): opaque=$opaque/$total sample=R$($sampleColor.R) G$($sampleColor.G) B$($sampleColor.B) A$($sampleColor.A)" } else { Write-Host " $key ($x,$y,$w,$h): ALL TRANSPARENT!" }
    }
    $bmp.Dispose()
}
