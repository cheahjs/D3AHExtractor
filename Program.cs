/*
 * Copyright 2012 Cheah Jun Siang
 * 
 * This file is part of D3AHExtractor
 * 
 * D3AHExtractor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * D3AHExtractor is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with D3AHExtractor.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using MODI;
using System.IO;

namespace D3AHExtractor
{
    class Program
    {
        private static Regex price = new Regex(@"last 10 trades: (\d+((,\d+)?(.\d+)?)?) ",
                                               RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex rmahprice = new Regex(@"last 10 trades: (([a-z]+)?[\$-€]\d+(,\d+)?\.\d+) ",
                                                   RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static FileSystemWatcher watcher;

        private static Dictionary<string, Rectangle> Sizes = new Dictionary<string, Rectangle>()
            {
                {"1280x1024w", new Rectangle(472, 388, 400, 100)},
                {"1280x1024", new Rectangle(398, 335, 550, 150)},
                {"1280x720", new Rectangle(398, 183, 550, 150)}
            };

        private static Rectangle Size = new Rectangle(0, 0, 0, 0);

        private static bool ResetSize = true;

        static void Main(string[] args)
        {
            var path = args.Length == 0 ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Diablo III\Screenshots" : args[0];
            if (args.Length == 3)
            {
                Size = new Rectangle(int.Parse(args[1].Split('x')[0]), int.Parse(args[1].Split('x')[1]),
                                     int.Parse(args[2].Split('x')[0]), int.Parse(args[2].Split('x')[1]));
                ResetSize = false;
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
            Thread.Sleep(1000); //make sure file is fully writen
            try
            {
                Console.WriteLine("New file created: {0}", Path.GetFileName(e.FullPath));
                if (!SetSize(e.FullPath))
                    return;
                var croppath = CropAndSave(e.FullPath);
                var ocrtext = OCRFile(croppath);
                Console.WriteLine("----------------------------");
                Console.WriteLine(ocrtext);
                Console.WriteLine("----------------------------");
                var price = GetPrice(ocrtext);
                if (price == "")
                {
                    Console.WriteLine("Could not extract price information from {0}",
                                      Path.GetFileNameWithoutExtension(e.FullPath));
                    return;
                }
                var stream = File.OpenRead(croppath);
                var cropbmp = (Bitmap)Bitmap.FromStream(stream);
                var itemname = MatchItem(cropbmp);
                stream.Close();
                if (itemname == "")
                {
                    itemname = Replace(ocrtext.Split(new[] {"\r\n"}, StringSplitOptions.RemoveEmptyEntries)[0], " ", "_");
                    Console.WriteLine("{0} does not match any known items, using {1} as the name.",
                                      Path.GetFileNameWithoutExtension(e.FullPath), itemname);
                }
                Console.WriteLine("{0} has a price of {1} as of {2}.", itemname, price,
                                  File.GetCreationTimeUtc(e.FullPath));
                var writeline = string.Format("{0} - {1} - {2}\n",
                                              DateTimeToUnixTimestamp(File.GetCreationTimeUtc(e.FullPath)), itemname,
                                              price);
                File.Delete(croppath);
                File.AppendAllText("priceinfo.txt", writeline);
            }
            catch (Exception)
            {
            }
        }

        static string Replace(string data, string replace, string replacement)
        {
            return (new Regex(replace)).Replace(data, replacement);
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
            MODI.Document md = new MODI.Document();
            md.Create(path);
            md.OCR(MiLANGUAGES.miLANG_ENGLISH);
            MODI.Images imgs = md.Images;
            MODI.Image img = imgs[0];
            MODI.Layout layout = img.Layout;
            try
            {
                var text = layout.Text;
                return text;
            }
            finally
            {
                md.Close(false);
                Marshal.FinalReleaseComObject(md);
                Marshal.FinalReleaseComObject(imgs);
                Marshal.FinalReleaseComObject(img);
                Marshal.FinalReleaseComObject(layout);
                md = null;
                GC.Collect();  
            }
        }

        static string GetPrice(string data)
        {
            var match = price.Match(data);
            if (!match.Success)
            {
                match = rmahprice.Match(data);
                double x;
                return match.Success ? (double.TryParse(match.Groups[1].Value.Substring(1), out x) ? match.Groups[1].Value : "" ) : "";
            }
            int x2;
            return int.TryParse(new Regex(@"(\.)?(,)?").Replace(match.Groups[1].Value, ""), out x2) ? new Regex(@"(\.)?(,)?").Replace(match.Groups[1].Value, "") : "";
        }
        
        static string MatchItem(Bitmap image)
        {
            var image2 = image.Clone(new Rectangle(0, 0, image.Width, image.Height), PixelFormat.Format24bppRgb);
            var files = Directory.GetFiles("items", "*.png", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                Console.WriteLine("Comparing to {0}", file);
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

        static bool SetSize(string path)
        {
            if (!ResetSize)
                return true;
            using (var bmp = new Bitmap(path))
            {
                var reso = GetResolution(bmp);
                var result = Sizes.TryGetValue(reso, out Size);
                Console.WriteLine(
                    !result ? "Unable to find suitable points for {0}" : "Using predetermined points for {0}.", reso);
                return result;
            }
        }

        static string GetResolution(Bitmap bmp)
        {
            var widescreen = ((double) bmp.Size.Width/bmp.Size.Height).CompareTo(((double) 16/9)) == 0;
            bool letterbox = true;
            var threshold = 0;
            if (!widescreen)
            {
                for (int i = 0;i < 20;i++)
                {
                    for (int j = 0; j < bmp.Size.Width - 1; j++)
                    {
                        var pixel = bmp.GetPixel(j, i);
                        if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)
                        {
                            threshold++;
                            if (threshold > 1000)
                            {
                                letterbox = false;
                                break;
                            }
                        }
                    }
                }
            }
            return string.Format("{0}x{1}{2}", bmp.Size.Width, bmp.Size.Height, letterbox ? "w" : "");
        }
    }
}
