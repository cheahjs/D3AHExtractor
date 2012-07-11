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
        static Regex price = new Regex(@"last 10 trades: (\d+) p", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static Regex rmahprice = new Regex(@"last 10 trades: (\$\d+(,\d+)?\.\d+) p", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static FileSystemWatcher watcher = new FileSystemWatcher(@"C:\Users\Deathmax\Documents\Diablo III\Screenshots", "*.jpg");
        static void Main(string[] args)
        {
            Console.WriteLine("Registering filesystem events.");
            watcher.BeginInit();
            watcher.EnableRaisingEvents = true;
            //watcher.Created += WatcherOnCreated;
            watcher.EndInit();
            //AnalyseFile(@"C:\Users\Deathmax\Documents\Diablo III\Screenshots\720p_crop.jpg");
            //CropImage(new Bitmap(@"C:\Users\Deathmax\Documents\Diablo III\Screenshots\Screenshot012.jpg")).Save(@"C:\Users\Deathmax\Documents\Diablo III\Screenshots\Screenshot012_autocrop.tif", ImageFormat.Tiff);
            //AnalyseFile(@"C:\Users\Deathmax\Documents\Diablo III\Screenshots\Screenshot012_autocrop.tif");
            WatcherOnCreated(null, new FileSystemEventArgs(WatcherChangeTypes.Created, @"C:\Users\Deathmax\Documents\Diablo III\Screenshots", "Screenshot065.jpg"));
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
            //var cropbmp = new Bitmap(croppath);
            var itemname = MatchItem(cropbmp);
            stream.Close();
            if (itemname == "")
            {
                Console.WriteLine("{0} does not match any known items.", Path.GetFileNameWithoutExtension(e.FullPath));
                return;
            }
            var ocrtext = OCRFile(croppath);
            Console.WriteLine(ocrtext);
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
            //File.AppendAllText("priceinfo.txt", writeline);
        }

        static string CropAndSave(string path)
        {
            CropImage(new Bitmap(path)).Save(Path.ChangeExtension(path, "_autocrop.tif"), ImageFormat.Tiff);
            return Path.ChangeExtension(path, "_autocrop.tif");
        }

        static Bitmap CropImage(Bitmap data)
        {
            var retn = new Bitmap(400, 100);
            int w1 = 0;
            int h1 = 0;
            for (int w = 472; w < 872; w++, w1++)
            {
                h1 = 0;
                for (int h = 388; h < 488; h++, h1++)
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
            //md.Close();
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
