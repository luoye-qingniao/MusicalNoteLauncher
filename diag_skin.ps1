Add-Type -AssemblyName System.Drawing
$files = @(
    (Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\steve.png'),
    (Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\alex.png')
)
foreach ($f in $files) {
    try {
        $bmp = [System.Drawing.Bitmap]::FromFile($f)
        Write-Host "=== $([System.IO.Path]::GetFileName($f)) ==="
        Write-Host " W=$($bmp.Width) H=$($bmp.Height) PF=$($bmp.PixelFormat)"
        $samples = @(@(0,0),@(8,8),@(20,8),@(40,16),@(0,16),@(32,48),@(60,60))
        foreach ($s in $samples) {
            $x = [math]::Min($s[0], $bmp.Width-1)
            $y = [math]::Min($s[1], $bmp.Height-1)
            $c = $bmp.GetPixel($x,$y)
            Write-Host " ($x,$y) A=$($c.A) R=$($c.R) G=$($c.G) B=$($c.B)"
        }
        $bmp.Dispose()
    } catch {
        Write-Host "ERR: $($_.Exception.Message)"
    }
}
