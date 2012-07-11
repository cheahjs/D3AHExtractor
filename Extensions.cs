using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using AForge.Imaging;
using AForge.Imaging.Filters;

namespace D3AHExtractor
{
    public static class BitmapExtensions
    {
        public static bool Contains(this Bitmap template, Bitmap bmp)
        {
            const Int32 divisor = 1;
            const Int32 epsilon = 10;

            var etm = new ExhaustiveTemplateMatching(0.9f);

            var tm = etm.ProcessImage(
                new ResizeNearestNeighbor(template.Width/divisor, template.Height/divisor).Apply(template),
                new ResizeNearestNeighbor(bmp.Width/divisor, bmp.Height/divisor).Apply(bmp)
                );

            if (tm.Length == 1)
            {
                Rectangle tempRect = tm[0].Rectangle;

                if (Math.Abs(bmp.Width/divisor - tempRect.Width) < epsilon
                    &&
                    Math.Abs(bmp.Height/divisor - tempRect.Height) < epsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
