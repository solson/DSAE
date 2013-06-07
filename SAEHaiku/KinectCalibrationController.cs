using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace SAEHaiku
{
    class KinectCalibrationController
    {
        private string calibrationFile = HaikuStudyController.filenamePrefix + "kinect_calibration.cfg";

        public Point? currentKinectLocation;
        private Point topLeftKinect;
        private Point topRightKinect;
        private Point bottomLeftKinect;
        private Point bottomRightKinect;
        public int currentPoint;

        private Point topLeftScreen;
        private Point topRightScreen;
        private Point bottomLeftScreen;
        private Point bottomRightScreen;

        private double[,] screenToKinect;
        private double[,] kinectToScreen;

        public bool calibrated = false;

        public KinectCalibrationController()
        {
            screenToKinect = new double[3, 3];
            kinectToScreen = new double[3, 3];

            int baseHeight = Program.tableHeight - Program.usableHeight;

            topLeftScreen = new Point(Program.tableWidth / 4, baseHeight + Program.usableHeight / 4);
            topRightScreen = new Point(Program.tableWidth * 3 / 4, baseHeight + Program.usableHeight / 4);
            bottomRightScreen = new Point(Program.tableWidth * 3 / 4, baseHeight + Program.usableHeight * 3 / 4);
            bottomLeftScreen = new Point(Program.tableWidth / 4, baseHeight + Program.usableHeight * 3 / 4);

            if (ReadCalibration())
            {
                calibrated = true;
                ComputeMatrix();
            }
        }

        public void StartCalibration()
        {
            currentKinectLocation = null;
            calibrated = false;
            currentPoint = 0;
        }

        public void RecordPosition()
        {
            if (currentKinectLocation == null)
                return;
            var currentLocation = (Point)currentKinectLocation;

            Console.WriteLine(currentPoint);

            switch (currentPoint)
            {
                case 0: topLeftKinect     = currentLocation; break;
                case 1: topRightKinect    = currentLocation; break;
                case 2: bottomRightKinect = currentLocation; break;
                case 3: bottomLeftKinect  = currentLocation; break;
            }

            currentPoint++;

            if (currentPoint > 3)
            {
                calibrated = true;
                ComputeMatrix();
                Program.mainForm.SendCalibration(Matrix);
                WriteCalibration();
            }
        }

        public Point KinectToScreen(Point point)
        {
            double x = kinectToScreen[0, 0] * point.X + kinectToScreen[0, 1] * point.Y + kinectToScreen[0, 2];
            double y = kinectToScreen[1, 0] * point.X + kinectToScreen[1, 1] * point.Y + kinectToScreen[1, 2];
            return new Point((int)x, (int)y);
        }

        public Point ScreenToKinect(Point point)
        {
            double x = screenToKinect[0, 0] * point.X + screenToKinect[0, 1] * point.Y + screenToKinect[0, 2];
            double y = screenToKinect[1, 0] * point.X + screenToKinect[1, 1] * point.Y + screenToKinect[1, 2];
            return new Point((int)x, (int)y);
        }

        private void ComputeMatrix()
        {
            double det;

            // build the screen-to-Kinect transformation matrix
            screenToKinect[0, 0] = (topRightKinect.X - topLeftKinect.X) / (double)(topRightScreen.X - topLeftScreen.X); // (1,0) basis vector
            screenToKinect[1, 0] = (topRightKinect.Y - topLeftKinect.Y) / (double)(topRightScreen.X - topLeftScreen.X);
            screenToKinect[0, 1] = (bottomLeftKinect.X - topLeftKinect.X) / (double)(bottomLeftScreen.Y - topLeftScreen.Y);  // (0,1) basis vector
            screenToKinect[1, 1] = (bottomLeftKinect.Y - topLeftKinect.Y) / (double)(bottomLeftScreen.Y - topLeftScreen.Y);
            screenToKinect[0, 2] = topLeftKinect.X - topLeftScreen.X * screenToKinect[0, 0] - topLeftScreen.Y * screenToKinect[0, 1]; //x translation component (?)
            screenToKinect[1, 2] = topLeftKinect.Y - topLeftScreen.Y * screenToKinect[1, 1] - topLeftScreen.X * screenToKinect[1, 0]; //y translation component
            screenToKinect[2, 0] = 0;
            screenToKinect[2, 1] = 0;
            screenToKinect[2, 2] = 1;

            // compute the inverse of the above matrix
            det = screenToKinect[0, 0] * screenToKinect[1, 1] - screenToKinect[0, 1] * screenToKinect[1, 0]; //4 other terms are zero
            kinectToScreen[0, 0] = screenToKinect[1, 1] / det;
            kinectToScreen[1, 0] = -screenToKinect[1, 0] / det;
            kinectToScreen[0, 1] = -screenToKinect[0, 1] / det;
            kinectToScreen[1, 1] = screenToKinect[0, 0] / det;
            kinectToScreen[0, 2] = (screenToKinect[0, 1] * screenToKinect[1, 2] - screenToKinect[0, 2] * screenToKinect[1, 1]) / det;
            kinectToScreen[1, 2] = (screenToKinect[0, 2] * screenToKinect[1, 0] - screenToKinect[0, 0] * screenToKinect[1, 2]) / det;
            kinectToScreen[2, 0] = 0; //bottom row does not change
            kinectToScreen[2, 1] = 0; //bottom row does not change
            kinectToScreen[2, 2] = 1; //bottom row does not change

            Matrix = new Matrix(
                    (float)kinectToScreen[0, 0], (float)kinectToScreen[1, 0],
                    (float)kinectToScreen[0, 1], (float)kinectToScreen[1, 1],
                    (float)kinectToScreen[0, 2], (float)kinectToScreen[1, 2]);
        }

        public Matrix Matrix { get; private set; }

        private bool ReadCalibration()
        {
            if (!File.Exists(calibrationFile))
                return false;

            try
            {
                using (TextReader reader = File.OpenText(calibrationFile))
                {
                    topLeftKinect.X = int.Parse(reader.ReadLine());
                    topLeftKinect.Y = int.Parse(reader.ReadLine());
                    topRightKinect.X = int.Parse(reader.ReadLine());
                    topRightKinect.Y = int.Parse(reader.ReadLine());
                    bottomRightKinect.X = int.Parse(reader.ReadLine());
                    bottomRightKinect.Y = int.Parse(reader.ReadLine());
                    bottomLeftKinect.X = int.Parse(reader.ReadLine());
                    bottomLeftKinect.Y = int.Parse(reader.ReadLine());
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private void WriteCalibration()
        {
            using (TextWriter writer = File.CreateText(calibrationFile))
            {
                writer.WriteLine(topLeftKinect.X);
                writer.WriteLine(topLeftKinect.Y);
                writer.WriteLine(topRightKinect.X);
                writer.WriteLine(topRightKinect.Y);
                writer.WriteLine(bottomRightKinect.X);
                writer.WriteLine(bottomRightKinect.Y);
                writer.WriteLine(bottomLeftKinect.X);
                writer.WriteLine(bottomLeftKinect.Y);
            }
        }

    }
}
