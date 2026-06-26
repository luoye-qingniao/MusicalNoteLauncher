Add-Type -AssemblyName System.Drawing
$files = @((Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\steve.png'), (Join-Path $PSScriptRoot 'MusicalNoteLauncher-main\Assets\Skins\alex.png'))
foreach ($f in $files) {
    $bmp = [System.Drawing.Bitmap]::FromFile($f)
    $name = [System.IO.Path]::GetFileName($f)
    Write-Host "=== $name ==="
    $checks = @{}
    $checks['HEAD'] = @(0,0,32,16)
    $checks['BODY'] = @(16,16,24,16)
    $checks['RIGHT_ARM'] = @(40,16,16,16)
    $checks['LEFT_ARM'] = @(32,48,16,16)
    $checks['RIGHT_LEG'] = @(0,16,16,16)
    $checks['LEFT_LEG'] = @(16,48,16,16)
    foreach ($k in $checks.Keys) {
        $c = $checks[$k]
        $x=$c[0];$y=$c[1];$w=$c[2];$h=$c[3]
        $transp=0;$total=0
        for ($yy=0;$yy -lt $h;$yy++){for($xx=0;$xx -lt $w;$xx++){$c2=$bmp.GetPixel($x+$xx,$y+$yy);if($c2.A -eq 0){$transp++};$total++}}
        $pct = [math]::Round(100.0 * $transp / $total, 1)
        Write-Host " $k area ($x,$y)-($($x+$w),$($y+$h)) : transparent=$transp/$total = $pct%"
    }
    $bmp.Dispose()
}
