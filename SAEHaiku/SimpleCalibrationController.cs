using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Interpolation;
using USask.HCI.Polhemus.HighLevel;
using System.Drawing;
using System.Windows.Forms;

namespace SAEHaiku
{
    public class SimpleCalibrationController
    {
        private List<Dataset> polhemusDatasets;
        private IInterpolationMethod x1Interpolation;
        private IInterpolationMethod x2Interpolation;
        private IInterpolationMethod y1Interpolation;
        private IInterpolationMethod y2Interpolation;
        private IInterpolationMethod z1Interpolation;
        private IInterpolationMethod z2Interpolation;

        private List<Point> tablePointsToRecord;

        public Boolean isCalibrated;

        public SimpleCalibrationController()
        {
            tablePointsToRecord = new List<Point>();
            polhemusDatasets = new List<Dataset>();
            
            Point p;
            p = new Point(Program.tableWidth / 4, Program.tableHeight / 4);
            tablePointsToRecord.Add(p);
            p = new Point(3 * Program.tableWidth / 4, Program.tableHeight / 4);
            tablePointsToRecord.Add(p);
            p = new Point(3 * Program.tableWidth / 4, 3 * Program.tableHeight / 4);
            tablePointsToRecord.Add(p);
            p = new Point(Program.tableWidth / 4, 3 * Program.tableHeight / 4);
            tablePointsToRecord.Add(p);

            isCalibrated = false;
        }

        public void addPolhemusDataset(Dataset dataset)
        {
            polhemusDatasets.Add(dataset);
            if (polhemusDatasets.Count == 4)
            {
                setUpCalibration();
            }           
        }

        private void setUpCalibration()
        {

            double[] polhemus1Xs = new double[] { polhemusDatasets[0].Position.y, polhemusDatasets[1].Position.y } ;
            double[] table1Xs = new double[] { tablePointsToRecord[0].X, tablePointsToRecord[1].X };
            x1Interpolation = Interpolation.CreateLinearSpline(polhemus1Xs, table1Xs);

            double[] polhemus2Xs = new double[] { polhemusDatasets[2].Position.y, polhemusDatasets[3].Position.y };
            double[] table2Xs = new double[] { tablePointsToRecord[2].X, tablePointsToRecord[3].X };
            x2Interpolation = Interpolation.CreateLinearSpline(polhemus2Xs, table2Xs);

            double[] polhemus1Ys = new double[] { polhemusDatasets[0].Position.z, polhemusDatasets[3].Position.z };
            double[] table1Ys = new double[] { tablePointsToRecord[0].Y, tablePointsToRecord[3].Y };
            y1Interpolation = Interpolation.CreateLinearSpline(polhemus1Ys, table1Ys);

            double[] polhemus2Ys = new double[] { polhemusDatasets[1].Position.z, polhemusDatasets[2].Position.z };
            double[] table2Ys = new double[] { tablePointsToRecord[1].Y, tablePointsToRecord[2].Y };
            y2Interpolation = Interpolation.CreateLinearSpline(polhemus2Ys, table2Ys);

            double[] polhemus1Zs = new double[] { polhemusDatasets[0].Position.x, polhemusDatasets[3].Position.x };
            double[] table1Zs = new double[] { 0, 0 };
            //z1Interpolation = Interpolation.CreateLinearSpline(polhemus1Zs, table1Zs);
            z1Interpolation = Interpolation.CreatePolynomial(polhemus1Zs, table1Zs);

            double[] polhemus2Zs = new double[] { polhemusDatasets[1].Position.x, polhemusDatasets[2].Position.x };
            double[] table2Zs = new double[] { 0, 0 };
            //z2Interpolation = Interpolation.CreateLinearSpline(polhemus2Zs, table2Zs);
            z2Interpolation = Interpolation.CreatePolynomial(polhemus2Zs, table2Zs);

            isCalibrated = true;
        }

        public Point tableLocationForPolhemusData(Dataset data)
        {
            Point location = new Point();

            double x1 = x1Interpolation.Interpolate(data.Position.y);
            double x2 = x2Interpolation.Interpolate(data.Position.y);

            double y1 = y1Interpolation.Interpolate(data.Position.z);
            double y2 = y2Interpolation.Interpolate(data.Position.z);
            
            location.X = (int)(x1 + x2) / 2;
            location.Y = (int)(y1 + y2) / 2;

            return location;
        }

        public double heightForPolhemusData(Dataset data)
        {
            double z1 = z1Interpolation.Interpolate(data.Position.x);
            double z2 = z2Interpolation.Interpolate(data.Position.x);
            return (z1 + z2) / 2;
        }
    }
}
