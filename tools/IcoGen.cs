using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// 图标生成器：输出 16/24/32/48/64/128/256 多尺寸 ICO（蓝底白喇叭）
// 用法：IcoGen.exe <输出路径.ico>
class IcoGen
{
    static Bitmap Render(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float inset = size * 0.05f;
            RectangleF circle = new RectangleF(inset, inset, size - inset * 2, size - inset * 2);
            using (LinearGradientBrush br = new LinearGradientBrush(
                circle,
                Color.FromArgb(255, 82, 152, 255),
                Color.FromArgb(255, 20, 88, 206),
                60f))
            {
                g.FillEllipse(br, circle);
            }

            using (SolidBrush w = new SolidBrush(Color.White))
            {
                float bx = size * 0.26f, by = size * 0.41f, bw = size * 0.17f, bh = size * 0.18f;
                g.FillRectangle(w, bx, by, bw, bh);

                PointF[] cone = new PointF[]
                {
                    new PointF(size * 0.43f, size * 0.41f),
                    new PointF(size * 0.60f, size * 0.27f),
                    new PointF(size * 0.60f, size * 0.73f),
                    new PointF(size * 0.43f, size * 0.59f)
                };
                g.FillPolygon(w, cone);

                if (size >= 24)
                {
                    using (Pen p = new Pen(Color.White, size * 0.058f))
                    {
                        g.DrawArc(p, size * 0.585f, size * 0.335f, size * 0.22f, size * 0.33f, -55f, 110f);
                        if (size >= 48)
                            g.DrawArc(p, size * 0.575f, size * 0.235f, size * 0.40f, size * 0.53f, -55f, 110f);
                    }
                }
            }
        }
        return bmp;
    }

    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("usage: IcoGen.exe <output.ico>");
            return;
        }
        string outPath = args[0];
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] pngs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
        {
            using (Bitmap b = Render(sizes[i]))
            using (MemoryStream ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                pngs[i] = ms.ToArray();
            }
        }

        using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
        {
            BinaryWriter w = new BinaryWriter(fs);
            w.Write((short)0);
            w.Write((short)1);
            w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((short)1);
                w.Write((short)32);
                w.Write(pngs[i].Length);
                w.Write(offset);
                offset += pngs[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++) w.Write(pngs[i]);
            w.Flush();
        }
        Console.WriteLine("icon written: " + outPath + " (" + new FileInfo(outPath).Length + " bytes)");
    }
}
