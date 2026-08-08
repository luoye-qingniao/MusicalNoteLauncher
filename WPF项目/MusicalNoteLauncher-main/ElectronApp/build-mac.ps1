$R = "..\dist-electron\mac-unpacked"
$Z = ".\.electron-cache\electron-v28.3.3-darwin-x64.zip"
rm -r -force $R -ea 0
ni -ty d -force $R | Out-Null
$T = "$R\_tmp"
ni -ty d -force $T | Out-Null
Add-Type -As System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Z).Path, (Resolve-Path $T).Path)
move "$T\Electron.app" "$R\MusicalNoteLauncher.app"
rmdir -r -force $T
$A = "$R\MusicalNoteLauncher.app\Contents\Resources\app"
ni -ty d -force $A | Out-Null
copy main.js $A\
copy preload.js $A\
copy package.json $A\
ni -ty d -force "$R\MusicalNoteLauncher.app\Contents\Resources\Assets" | Out-Null
copy "..\Assets\图标.png" "$R\MusicalNoteLauncher.app\Contents\Resources\Assets\" -ea 0
xcopy "..\MNL.Backend\bin\Release\net8.0\osx-x64\publish\*" "$R\MusicalNoteLauncher.app\Contents\Resources\backend\" /E /Y /Q
echo "macOS OK"
