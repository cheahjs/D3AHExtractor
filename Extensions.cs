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
