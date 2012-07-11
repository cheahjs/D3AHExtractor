using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using MODI;
using System.IO;
using AForge.Imaging;

namespace D3AHExtractor
{
    class Program
    {
        private static Regex price = new Regex(@"last 10 trades: (\d+((,\d+)?(.\d+)?)?) p",
                                               RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex rmahprice = new Regex(@"last 10 trades: (\$\d+(,\d+)?\.\d+) p",
                                                   RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static FileSystemWatcher watcher;

        private static Rectangle Size = new Rectangle(472, 388, 400, 100);  // 1280x1024 with letterboxing

        static void Main(string[] args)
        {
            var path = args.Length == 0 ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Diablo III\Screenshots" : args[0];
            if (args.Length == 3)
            {
                Size = new Rectangle(int.Parse(args[1].Split('x')[0]), int.Parse(args[1].Split('x')[1]),
                                     int.Parse(args[2].Split('x')[0]), int.Parse(args[2].Split('x')[1]));
            }
            Console.WriteLine("Registering filesystem events. ({0})", path);
            watcher = new FileSystemWatcher(path, "*.jpg");
            watcher.BeginInit();
            watcher.EnableRaisingEvents = true;
            watcher.Created += WatcherOnCreated;
            watcher.EndInit();
            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static void WatcherOnCreated(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("New file created: {0}", Path.GetFileName(e.FullPath));
            var croppath = CropAndSave(e.FullPath);
            var stream = File.OpenRead(croppath);
            var cropbmp = (Bitmap)Bitmap.FromStream(stream);
            var itemname = MatchItem(cropbmp);
            stream.Close();
            if (itemname == "")
            {
                Console.WriteLine("{0} does not match any known items.", Path.GetFileNameWithoutExtension(e.FullPath));
                return;
            }
            var ocrtext = OCRFile(croppath);
            var price = GetPrice(ocrtext);
            if (price == "")
            {
                Console.WriteLine("Could not extract price information from {0}({1}).\nOCR text:\n{2}",
                                  Path.GetFileNameWithoutExtension(e.FullPath), itemname,
                                  ocrtext);
                return;
            }
            Console.WriteLine("{0} has a price of {1} as of {2}.", itemname, price, File.GetCreationTimeUtc(e.FullPath));
            var writeline = string.Format("{0} - {1} - {2}\n",
                                          DateTimeToUnixTimestamp(File.GetCreationTimeUtc(e.FullPath)), itemname, price);
            File.AppendAllText("priceinfo.txt", writeline);
        }

        static string CropAndSave(string path)
        {
            CropImage(new Bitmap(path)).Save(Path.ChangeExtension(path, "_autocrop.tif"), ImageFormat.Tiff);
            return Path.ChangeExtension(path, "_autocrop.tif");
        }

        static Bitmap CropImage(Bitmap data)
        {
            var retn = new Bitmap(Size.Size.Width, Size.Size.Height);
            int w1 = 0;
            int h1 = 0;
            for (int w = Size.X; w < Size.X + retn.Width; w++, w1++)
            {
                h1 = 0;
                for (int h = Size.Y; h < Size.Y + retn.Height; h++, h1++)
                {
                    retn.SetPixel(w1, h1, data.GetPixel(w, h));
                }
            }
            return retn;
        }

        static string OCRFile(string path)
        {
            var md = new MODI.Document();
            md.Create(path);
            md.OCR(MiLANGUAGES.miLANG_ENGLISH);
            return ((MODI.Image) md.Images[0]).Layout.Text;
        }

        static string GetPrice(string data)
        {
            var match = price.Match(data);
            if (!match.Success)
                match = rmahprice.Match(data);
            return match.Success ? match.Groups[1].Value : "";
        }
        
        static string MatchItem(Bitmap image)
        {
            var image2 = image.Clone(new Rectangle(0, 0, image.Width, image.Height), PixelFormat.Format24bppRgb);
            var files = Directory.GetFiles("items");
            foreach (var file in files)
            {
                var compare = new Bitmap(file);
                compare = compare.Clone(new Rectangle(0, 0, compare.Width, compare.Height), PixelFormat.Format24bppRgb);
                if (image2.Contains(compare))
                {
                    compare.Dispose();
                    return Path.GetFileNameWithoutExtension(file);
                }
                compare.Dispose();
            }
            return "";
        }

        static int DateTimeToUnixTimestamp(DateTime dateTime)
        {
            return (int)(dateTime - new DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds;
        }
    }
}
