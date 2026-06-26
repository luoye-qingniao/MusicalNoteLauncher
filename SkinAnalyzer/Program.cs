using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SkinAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] files = new string[]
            {
                @"f:\mc音符启动器\MusicalNoteLauncher-main\musicalnotelauncher-main\Assets\Skins\steve.png",
                @"f:\mc音符启动器\MusicalNoteLauncher-main\musicalnotelauncher-main\Assets\Skins\alex.png",
                @"f:\mc音符启动器\HMCL-3.15.1\HMCLCore\src\main\resources\assets\img\skin\wide\steve.png",
                @"f:\mc音符启动器\HMCL-3.15.1\HMCLCore\src\main\resources\assets\img\skin\wide\alex.png",
            };

            foreach (string file in files)
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine("NOT FOUND: " + file);
                    continue;
                }

                Console.WriteLine("===========================================");
                Console.WriteLine("File: " + file);
                Console.WriteLine("===========================================");

                using (Bitmap bmp = new Bitmap(file))
                {
                    Console.WriteLine("Size: " + bmp.Width + "x" + bmp.Height);
                    Console.WriteLine("PixelFormat: " + bmp.PixelFormat);

                    // Analyze body regions
                    Rectangle[] regions = new Rectangle[]
                    {
                        new Rectangle(0, 0, 8, 8),        // Head top (8x8)
                        new Rectangle(8, 0, 8, 8),        // Head bottom (8x8)
                        new Rectangle(0, 8, 8, 8),        // Head front (8x8)
                        new Rectangle(8, 8, 8, 8),        // Head back (8x8)
                        new Rectangle(16, 0, 4, 8),       // Head L-side (4x8)
                        new Rectangle(20, 0, 4, 8),       // Head R-side (4x8)

                        new Rectangle(16, 16, 8, 8),      // Body top (8x8)
                        new Rectangle(16, 24, 8, 12),     // Body front (8x12) body torso
                        new Rectangle(24, 16, 8, 8),      // Body bottom (8x8)
                        new Rectangle(0, 16, 4, 12),      // Body L-side (4x12)
                        new Rectangle(12, 16, 4, 12),     // Body R-side (4x12)

                        new Rectangle(40, 16, 4, 12),     // Arm front L (4x12)
                        new Rectangle(44, 16, 4, 12),     // Arm front R (4x12)

                        new Rectangle(0, 16, 4, 12),      // Leg front L (4x12)

                        // Hat/overlay
                        new Rectangle(32, 0, 8, 8),       // Hat top (8x8)
                        new Rectangle(40, 0, 8, 8),       // Hat bottom (8x8)
                        new Rectangle(32, 8, 8, 8),       // Hat front (8x8)
                        new Rectangle(40, 8, 8, 8),       // Hat back (8x8)
                        new Rectangle(48, 0, 4, 8),       // Hat L-side (4x8)
                        new Rectangle(52, 0, 4, 8),       // Hat R-side (4x8)
                    };

                    string[] regionNames = new string[]
                    {
                        "Head top (8x8)",
                        "Head bottom (8x8)",
                        "Head front (8x8) [face!]",
                        "Head back (8x8)",
                        "Head L-side (4x8)",
                        "Head R-side (4x8)",
                        "Body top (8x8)",
                        "Body front torso (8x12) [body!]",
                        "Body bottom (8x8)",
                        "Body L-side (4x12)",
                        "Body R-side (4x12)",
                        "Arm front L (4x12)",
                        "Arm front R (4x12)",
                        "Leg front L (4x12)",
                        "Hat top (8x8)",
                        "Hat bottom (8x8)",
                        "Hat front (8x8)",
                        "Hat back (8x8)",
                        "Hat L-side (4x8)",
                        "Hat R-side (4x8)",
                    };

                    // First overall transparency check
                    int totalTransparent = 0;
                    int totalOpaque = 0;
                    for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.A < 128) totalTransparent++;
                        else totalOpaque++;
                    }
                    Console.WriteLine("Overall transparent: " + totalTransparent + " / opaque: " + totalOpaque);

                    // Analyze specific regions
                    for (int i = 0; i < regions.Length; i++)
                    {
                        Rectangle r = regions[i];
                        int transparent = 0;
                        int opaque = 0;
                        long rSum = 0, gSum = 0, bSum = 0;
                        int sampleCount = 0;

                        for (int y = r.Y; y < r.Y + r.Height; y++)
                        for (int x = r.X; x < r.X + r.Width; x++)
                        {
                            if (x >= bmp.Width || y >= bmp.Height) continue;
                            Color c = bmp.GetPixel(x, y);
                            if (c.A < 128)
                                transparent++;
                            else
                            {
                                opaque++;
                                rSum += c.R;
                                gSum += c.G;
                                bSum += c.B;
                                sampleCount++;
                            }
                        }

                        if (sampleCount > 0)
                        {
                            int avgR = (int)(rSum / sampleCount);
                            int avgG = (int)(gSum / sampleCount);
                            int avgB = (int)(bSum / sampleCount);
                            Console.WriteLine($"  {regionNames[i]}: transparent={transparent}, opaque={opaque}, avgRGB=({avgR},{avgG},{avgB})");
                        }
                        else
                        {
                            Console.WriteLine($"  {regionNames[i]}: transparent={transparent}, opaque={opaque}, avgRGB=(ALL TRANSPARENT)");
                        }
                    }

                    // Find what the head/skin color is (sample pixels from head region that aren't the hair/eyes)
                    Console.WriteLine();
                    Console.WriteLine("Sampling skin colors from head region (0-32, 0-16):");
                    for (int y = 0; y < 16; y++)
                    {
                        for (int x = 0; x < 32; x++)
                        {
                            Color c = bmp.GetPixel(x, y);
                            if (c.A >= 128)
                                Console.Write($"({c.R},{c.G},{c.B}) ");
                            else
                                Console.Write("___ ");
                        }
                        Console.WriteLine();
                    }
                }

                Console.WriteLine();
            }
        }
    }
}
