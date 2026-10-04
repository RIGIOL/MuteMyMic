// Generates assets\app.ico from the same drawing code the app uses.
// Run by build.bat only when assets\app.ico is missing.
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;

static class MakeIcon
{
    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "app.ico";
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (int s in sizes)
        {
            using (var bmp = MuteMyMic.IconPainter.DrawBadge(s, true))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                images.Add(ms.ToArray());
            }
        }

        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((short)1);
                w.Write((short)32);
                w.Write(images[i].Length);
                w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }
        Console.WriteLine("Wrote " + path);
    }
}
