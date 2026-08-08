﻿Add-Type @"
using System;
using System.IO;
using System.Runtime.InteropServices;

public class IconReplacer {
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern IntPtr BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cb);

    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    // RT_ICON = 3, RT_GROUP_ICON = 14
    static readonly IntPtr RT_ICON = new IntPtr(3);
    static readonly IntPtr RT_GROUP_ICON = new IntPtr(14);

    public static bool SetIcon(string exePath, string icoPath) {
        byte[] icoData = File.ReadAllBytes(icoPath);
        if (icoData.Length < 22) return false;

        using (var ms = new MemoryStream(icoData))
        using (var br = new BinaryReader(ms)) {
            br.ReadInt16(); // reserved
            int type = br.ReadInt16(); // 1 = ICO
            int count = br.ReadInt16();
            if (type != 1 || count == 0) return false;

            IntPtr hUpdate = BeginUpdateResource(exePath, true);
            if (hUpdate == IntPtr.Zero) return false;

            try {
                // Read icon directory entries
                var entries = new (byte width, byte height, byte colors, byte reserved, short planes, short bpp, int size, int offset)[count];
                for (int i = 0; i < count; i++) {
                    entries[i] = (
                        br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte(),
                        br.ReadInt16(), br.ReadInt16(), br.ReadInt32(), br.ReadInt32()
                    );
                }

                // Write GROUP_ICON resource (name=1, language=1033)
                using (var gms = new MemoryStream()) {
                    using (var gw = new BinaryWriter(gms)) {
                        gw.Write((short)0); // reserved
                        gw.Write((short)1); // ICO type
                        gw.Write((short)count);
                        ushort id = 1;
                        for (int i = 0; i < count; i++) {
                            var e = entries[i];
                            gw.Write(e.width == 0 ? (byte)256 : e.width);
                            gw.Write(e.height == 0 ? (byte)256 : e.height);
                            gw.Write(e.colors);
                            gw.Write(e.reserved);
                            gw.Write(e.planes);
                            gw.Write(e.bpp);
                            gw.Write(e.size);
                            gw.Write(id++);
                        }
                    }
                    byte[] groupData = gms.ToArray();
                    if (!UpdateResource(hUpdate, RT_GROUP_ICON, new IntPtr(1), 1033, groupData, (uint)groupData.Length))
                        return false;
                }

                // Write each icon as RT_ICON resource
                ushort iconId = 1;
                for (int i = 0; i < count; i++) {
                    byte[] iconEntry = new byte[entries[i].size];
                    Array.Copy(icoData, entries[i].offset, iconEntry, 0, entries[i].size);
                    if (!UpdateResource(hUpdate, RT_ICON, new IntPtr(iconId), 1033, iconEntry, (uint)iconEntry.Length))
                        return false;
                    iconId++;
                }

                return EndUpdateResource(hUpdate, false);
            } catch {
                EndUpdateResource(hUpdate, true);
                return false;
            }
        }
    }
}
"@ -ReferencedAssemblies System.Drawing

$exe = "f:\MCMNL\MNL\WPF项目\MusicalNoteLauncher-main\dist-electron\win-unpacked\MusicalNoteLauncher.exe"
$ico = "f:\MCMNL\MNL\WPF项目\MusicalNoteLauncher-main\Assets\图标.ico"
$result = [IconReplacer]::SetIcon($exe, $ico)
echo "Icon set: $result"

