using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace MetafileDifference
{
    public static class Program
    {
        public static void Main()
        {
            const string aPath = "a.txt";
            File.WriteAllLines(aPath, EmfRecord.GetMetafileRecords(DrawMetafile()).Select(_ => _.ToString()));

            const string bPath = "b.txt";
            File.WriteAllLines(bPath, EmfRecord.GetMetafileRecords(DrawMetafile()).Select(_ => _.ToString()));

            Process.Start(@"C:\Program Files (x86)\WinMerge\WinMergeU.exe", $"/s /u /wr /wl \"{aPath}\" \"{bPath}\"");
        }
        
        private static Metafile DrawMetafile()
        {
            var entireRect = new Rectangle(0, 0, 100, 100);
            var metafile = CreateMetafileInstance(entireRect, MetafileFrameUnit.Pixel, EmfType.EmfPlusOnly);
            
            using (var g = Graphics.FromImage(metafile))
            {
                for (var times = 0; times < 2; times++)
                {
                    using (var region = new Region(entireRect))
                        g.FillRegion(Brushes.Black, region);

                    using (var pen = new Pen(Color.Black))
                        g.DrawRectangle(pen, entireRect);
                }
            }

            return metafile;
        }

        private static Metafile CreateMetafileInstance(Rectangle bounds, MetafileFrameUnit units, EmfType emfType)
        {
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                var hdc = graphics.GetHdc();
                try
                {
                    return new Metafile(hdc, bounds, units, emfType);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }
        }
    }
}
