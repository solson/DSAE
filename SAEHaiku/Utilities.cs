using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace SAEHaiku
{
    static class Utilities
    {
        public static DateTime experimentBeganAtTime = DateTime.Now;

        static public double distanceBetweenPoints(Point point1, Point point2)
        {
            return Math.Sqrt(Math.Pow((point1.X - point2.X), 2) + Math.Pow((point1.Y - point2.Y), 2));
        }

        public struct Segment
        {
            public PointF Start;
            public PointF End;
        }

        static public PointF? areSegmentsIntersecting(Segment AB, Segment CD)
        {
            double deltaACy = AB.Start.Y - CD.Start.Y;
            double deltaDCx = CD.End.X - CD.Start.X;
            double deltaACx = AB.Start.X - CD.Start.X;
            double deltaDCy = CD.End.Y - CD.Start.Y;
            double deltaBAx = AB.End.X - AB.Start.X;
            double deltaBAy = AB.End.Y - AB.Start.Y;

            double denominator = deltaBAx * deltaDCy - deltaBAy * deltaDCx;
            double numerator = deltaACy * deltaDCx - deltaACx * deltaDCy;

            if (denominator == 0)
            {
                if (numerator == 0)
                {
                    // collinear. Potentially infinite intersection points.
                    // Check and return one of them.
                    if (AB.Start.X >= CD.Start.X && AB.Start.X <= CD.End.X)
                    {
                        return AB.Start;
                    }
                    else if (CD.Start.X >= AB.Start.X && CD.Start.X <= AB.End.X)
                    {
                        return CD.Start;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                { // parallel
                    return null;
                }
            }

            double r = numerator / denominator;
            if (r < 0 || r > 1)
            {
                return null;
            }

            double s = (deltaACy * deltaBAx - deltaACx * deltaBAy) / denominator;
            if (s < 0 || s > 1)
            {
                return null;
            }

            return new PointF((float)(AB.Start.X + r * deltaBAx), (float)(AB.Start.Y + r * deltaBAy));
        }

        /**
* Returns a point on the line (x1,y1) -> (x2,y2)
* that is closest to the point (x,y)
*
* The result is a PVector.
* result.x and result.y are points on the line.
* The result.z variable contains the distance from (x,y) to the line,
* just in case you need it :)
*/

        static public double getMinimumDistanceBetweenLineAndPoint(Utilities.Segment line, float x, float y)
        {
            float x1 = line.Start.X;
            float y1 = line.Start.Y;
            float x2 = line.End.X;
            float y2 = line.End.Y;
            PointF closestPoint = new PointF();

            float dx = x2 - x1;
            float dy = y2 - y1;
            //float d = sqrt(dx * dx + dy * dy);
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            float ca = dx / d; // cosine
            float sa = dy / d; // sine

            float mX = (-x1 + x) * ca + (-y1 + y) * sa;

            if (mX <= 0)
            {
                closestPoint.X = x1;
                closestPoint.Y = y1;
            }
            else if (mX >= d)
            {
                closestPoint.X = x2;
                closestPoint.X = y2;
            }
            else
            {
                closestPoint.X = x1 + mX * ca;
                closestPoint.Y = y1 + mX * sa;
            }

            dx = x - closestPoint.X;
            dy = y - closestPoint.Y;
            //result.z = sqrt(dx * dx + dy * dy);

            return Math.Sqrt(dx * dx + dy * dy); ;
        }

        public static unsafe void setAlpha(Bitmap dest, Bitmap source)
        {
            BitmapData sourceData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height),
                                                    ImageLockMode.ReadOnly,
                                                    PixelFormat.Format32bppArgb);

            BitmapData destData = dest.LockBits(new Rectangle(0, 0, dest.Width, dest.Height),
                                                ImageLockMode.WriteOnly,
                                                PixelFormat.Format32bppArgb);

            byte* srcPtr = (byte*)sourceData.Scan0;
            byte* destPtr = (byte*)destData.Scan0;

            int width = source.Width;
            Parallel.For(0, source.Height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    byte* srcPixel = srcPtr + y * sourceData.Stride + x * 4;
                    byte* destPixel = destPtr + y * destData.Stride + x * 4;

                    destPixel[3] = srcPixel[3];
                }
            });

            source.UnlockBits(sourceData);
            dest.UnlockBits(destData);
        }

        public static unsafe Bitmap smoothMask(Bitmap mask)
        {
            BitmapData maskData = mask.LockBits(new Rectangle(0, 0, mask.Width, mask.Height),
                                                ImageLockMode.ReadOnly,
                                                PixelFormat.Format1bppIndexed);

            Bitmap dest = new Bitmap(mask.Width, mask.Height, PixelFormat.Format32bppArgb);
            BitmapData destData = dest.LockBits(new Rectangle(0, 0, dest.Width, dest.Height),
                                                ImageLockMode.ReadOnly,
                                                PixelFormat.Format32bppArgb);

            const int filterWidth = 3;
            const double factor = 1.0 / (filterWidth * filterWidth);
            const int filterOffset = (filterWidth - 1) / 2;

            byte* maskPtr = (byte*)maskData.Scan0;
            byte* destPtr = (byte*)destData.Scan0;

            int maskWidth = mask.Width;

            Parallel.For(filterOffset, mask.Height - filterOffset, offsetY =>
            {
                for (int offsetX = filterOffset; offsetX < maskWidth - filterOffset; offsetX++)
                {
                    double alpha = 0;

                    for (int filterY = -filterOffset; filterY <= filterOffset; filterY++)
                    {
                        for (int filterX = -filterOffset; filterX <= filterOffset; filterX++)
                        {
                            int calcOffset = (offsetY + filterY) * maskData.Stride + (offsetX + filterX) / 8;
                            int bitOffset = 7 - ((offsetX + filterX) % 8);
                            bool bitSet = (maskPtr[calcOffset] & (1 << bitOffset)) != 0;

                            if (bitSet)
                                alpha += 1;
                        }
                    }

                    alpha = factor * alpha;

                    int destByteOffset = offsetY * destData.Stride + offsetX * 4;
                    destPtr[destByteOffset + 3] = (byte)Math.Round(alpha * 255);
                }
            });

            mask.UnlockBits(maskData);
            dest.UnlockBits(destData);

            return dest;
        }

        public static unsafe int MaskOverlapArea(Bitmap mask1, Bitmap mask2)
        {
            int height = mask1.Height;
            int width = mask1.Width;

            BitmapData mask1Data = mask1.LockBits(new Rectangle(0, 0, mask1.Width, mask1.Height),
                                                ImageLockMode.ReadOnly,
                                                PixelFormat.Format24bppRgb);

            BitmapData mask2Data = mask2.LockBits(new Rectangle(0, 0, mask2.Width, mask2.Height),
                                                ImageLockMode.ReadOnly,
                                                PixelFormat.Format24bppRgb);

            byte* mask1Ptr = (byte*)mask1Data.Scan0;
            byte* mask2Ptr = (byte*)mask2Data.Scan0;

            int[] rowSums = new int[height];

            Parallel.For(0, height, (y) =>
            {
                rowSums[y] = 0;

                for (int x = 0; x < width; x++)
                {
                    int byteOffset = y * mask1Data.Stride + x * 3;
                    if (mask1Ptr[byteOffset] != 0 && mask2Ptr[byteOffset] != 0)
                        rowSums[y]++;
                }
            });

            mask1.UnlockBits(mask1Data);
            mask2.UnlockBits(mask2Data);

            return rowSums.Sum();
        }
    }
}
