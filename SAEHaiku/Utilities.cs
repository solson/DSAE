using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

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
    }
}
