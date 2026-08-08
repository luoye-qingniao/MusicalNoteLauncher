$R = "..\dist-electron\linux-unpacked"
$Z = ".\.electron-cache\electron-v28.3.3-linux-x64.zip"
rm -r -force $R -ea 0
ni -ty d -force $R | Out-Null
Add-Type -As System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Z).Path, (Resolve-Path $R).Path)
ni -ty d -force "$R\resources\app" | Out-Null
copy main.js "$R\resources\app\"
copy preload.js "$R\resources\app\"
copy package.json "$R\resources\app\"
ni -ty d -force "$R\resources\Assets" | Out-Null
copy "..\Assets\图标.png" "$R\resources\Assets\" -ea 0
xcopy "..\MNL.Backend\bin\Release\net8.0\linux-x64\publish\*" "$R\resources\backend\" /E /Y /Q
ren "$R\electron" "MusicalNoteLauncher" -ea 0
echo "Linux OK"
