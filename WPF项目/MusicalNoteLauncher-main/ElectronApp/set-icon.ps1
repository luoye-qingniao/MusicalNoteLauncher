# 使用 rcedit 嵌入图标和版本信息 (rcedit 不支持中文路径，先复制到英文临时路径)
$rcedit = ".\node_modules\rcedit\bin\rcedit-x64.exe"
$exe = "..\dist-electron\win-unpacked\MusicalNoteLauncher.exe"
$icoSrc = "..\Assets\图标.ico"
$icoTmp = "$env:TEMP\mnl-app-icon.ico"

copy $icoSrc $icoTmp -Force
& $rcedit $exe --set-icon $icoTmp
& $rcedit $exe --set-file-version "1.2.1.0" --set-product-version "1.2.1" --set-version-string FileDescription "MusicalNoteLauncher" --set-version-string ProductName "MusicalNoteLauncher"
rm $icoTmp -Force

(Get-Item $exe).VersionInfo | Format-List ProductName, FileDescription, FileVersion
Write-Host "Icon OK"
