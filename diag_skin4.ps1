Add-Type -AssemblyName System.Drawing
$files = @((Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\steve.png'), (Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\alex.png'))
foreach ($f in $files) {
    $bmp = [System.Drawing.Bitmap]::FromFile($f)
    $name = [System.IO.Path]::GetFileName($f)
    Write-Host "=== $name ==="
    Write-Host "-- Face area (x=8..16, y=8..16): checking each pixel --"
    $faceTransparent = 0
    for ($y=8; $y -lt 16; $y++) {
        $row = ''
        for ($x=8; $x -lt 16; $x++) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -eq 0) { $row += '.'; $faceTransparent++ }
            else { $row += '#' }
        }
        Write-Host " y=$y : $row"
    }
    Write-Host " Face transparent pixels: $faceTransparent / 64"
    $bmp.Dispose()
}
