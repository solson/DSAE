using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using USask.HCI.Polhemus.HighLevel;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using MathNet.Numerics.LinearAlgebra;

namespace SAEHaiku
{
    public class PolhemusController
    {
        Dictionary<int, Dataset> lastDatasetForEachStation;

        public class TableController
        {
            public SimpleCalibrationController calibrationControllerStation1;
            public SimpleCalibrationController calibrationControllerStation2;

            public bool isCalibrated = false;
            public TableController() 
            {
                tableCorners = new List<Dataset>();
                topleft = default(Dataset);
                bottomright = default(Dataset);

                calibrationControllerStation1 = new SimpleCalibrationController();
                calibrationControllerStation2 = new SimpleCalibrationController();
            }

            public List<Dataset> tableCorners;
            public Dataset topleft;
            public Dataset topright;
            public Dataset bottomright;
            public Dataset bottomleft;

            public Point screenLocationForTableLocation(Dataset dataset)
            {
                if(dataset.Station == 1)
                    return calibrationControllerStation1.tableLocationForPolhemusData(dataset);
                if (dataset.Station == 2)
                    return calibrationControllerStation2.tableLocationForPolhemusData(dataset);
                return Point.Empty;
            }

            public float heightForDataset(Dataset dataset)
            {
                return dataset.Position.x;
            }

            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
            public static extern void mouse_event(long dwFlags, long dx, long dy, long cButtons, long dwExtraInfo);

            private const int MOUSEEVENTF_LEFTDOWN = 0x02;
            private const int MOUSEEVENTF_LEFTUP = 0x04;
            private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
            private const int MOUSEEVENTF_RIGHTUP = 0x10;
            private const int MOUSEEVENTF_MOVE = 0x0001;
            private const int MOUSEEVENTF_ABSOLUTE = 0x8000;
            public void mouseDownAtTableLocation(Point location)
            {
                Cursor.Position = new Point((int)location.X, (int)location.Y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, location.X, location.Y, 0, 0);
            }
            public void mouseUpAtTableLocation(Point location)
            {
                //mouseDownAtTableLocation(location);
                Cursor.Position = new Point((int)location.X, (int)location.Y);
                mouse_event(MOUSEEVENTF_LEFTDOWN, location.X, location.Y, 0, 0);
                System.Threading.Thread.Sleep(10);
                mouse_event(MOUSEEVENTF_LEFTUP, location.X, location.Y, 0, 0);
            }
        }

        public TableController tableController = new TableController();

        public bool isUser1Touching = false;
        public bool isUser2Touching = false;

        public static Boolean isConnected = false;
        public static Boolean isCalibrated = false;

        public static PolhemusController sharedPolhemusController = null;

        public PolhemusController(Boolean polhemusConnected)
        {
            lastDatasetForEachStation = new Dictionary<int, Dataset>();
            //for (int i = 1; i <= Constants.numberOfPlayers; i++)
            lastDatasetForEachStation[1] = new Dataset();
            lastDatasetForEachStation[2] = new Dataset();

            if (polhemusConnected == true)
            {
                try
                {
                    Factory.CreateInstance(Debugmode.NoDebug);

                    Factory.Instance.PollingDelay = 30;
                    Factory.Instance.OnPolhemusButtonDown += new PolhemusEvent(OnPolhemusButtonDown);
                    Factory.Instance.OnPolhemusButtonUp += new PolhemusEvent(OnPolhemusButtonUp);
                    Factory.Instance.OnPolhemusMove += new PolhemusEvent(OnPolhemusMove);
                    isConnected = true;

                    Console.WriteLine("done with polhemus setup");
                }
                catch (Exception e)
                {
                    Console.WriteLine("There is no Polhemus connection: " + e);
                    isConnected = false;
                }
                sharedPolhemusController = this;
            }
        }

        /*public void useLastCalibration()
        {
            Dataset dataset = new Dataset(1, USask.HCI.Polhemus.HighLevel.ButtonState.Up, 2.828f, -12.497f, 13.211f,
                 -136.306f, 3.126f, 96.687f);
            tableController.calibrationController.addPolhemusDataset(dataset);

            dataset = new Dataset(1, USask.HCI.Polhemus.HighLevel.ButtonState.Up, 2.289f, 10.616f, 13.597f,
                 -154.106f, 12.614f, 118.244f);
            tableController.calibrationController.addPolhemusDataset(dataset);

            dataset = new Dataset(1, USask.HCI.Polhemus.HighLevel.ButtonState.Up, 2.59f, 10.886f, -21.115f,
                 -156.521f, -12.941f, 134.037f);
            tableController.calibrationController.addPolhemusDataset(dataset);

            dataset = new Dataset(1, USask.HCI.Polhemus.HighLevel.ButtonState.Up, 2.84f, -11.718f, -21.535f,
                 -140.27f, 8.503f, 108.406f);
            tableController.calibrationController.addPolhemusDataset(dataset);
        }*/


        public void addDatasetToCalibrationForStation(int stationID)
        {
            Console.WriteLine("adding " + lastDatasetForEachStation[stationID] + " to calibration for station " + stationID);

            if(stationID == 1)
                tableController.calibrationControllerStation1.addPolhemusDataset(lastDatasetForEachStation[stationID]);
            else if(stationID == 2)
                tableController.calibrationControllerStation2.addPolhemusDataset(lastDatasetForEachStation[stationID]);

            if (tableController.calibrationControllerStation1.isCalibrated == true
                && tableController.calibrationControllerStation2.isCalibrated == true)
                tableController.isCalibrated = true;
        }

        /*public Boolean isStationTouchingAtLocation(int stationID, Point atLocation)
        {
            Dataset dataset = lastDatasetForEachStation[stationID];
            Player player = Constants.playerForNumber(stationID);
            if (dataset.XPos < Constants.touchingHeightForPlayer(player))
                return true;
            else
                return false;
        }*/

        /*public Boolean isStationHoveringAtLocation(int stationID, Point atLocation)
        {
            Dataset dataset = lastDatasetForEachStation[stationID];
            Player player = Constants.playerForNumber(stationID);
            if (dataset.XPos > Constants.hoveringHeightForPlayer(player))
                return true;
            else
                return false;
        }*/

        public void stationDidClick(int stationID)
        {
            if (Program.mainForm.calibrating == true)
            {
                addDatasetToCalibrationForStation(stationID);

                if (tableController.calibrationControllerStation1.isCalibrated == true 
                    && tableController.calibrationControllerStation2.isCalibrated == true)
                {
                    Program.mainForm.calibrating = false;
                    Program.mainForm.Invalidate();
                }
            }
            else
            {
                //Point tableLocation = tableController.calibrationController.tableLocationForPolhemusData(lastDatasetForEachStation[stationID]);
                //tableController.mouseUpAtTableLocation(tableLocation);

                Program.mainForm.toggleWordBoxUnderCursorNumberDragging(stationID);
            }          
        }

        public Point tableLocationForStation(int stationID)
        {
            if (tableController.calibrationControllerStation1.isCalibrated == true
                && tableController.calibrationControllerStation2.isCalibrated == true)
            {
                Dataset dataset = lastDatasetForEachStation[stationID];
                return tableController.screenLocationForTableLocation(dataset);
            }
            else
                return Point.Empty;
        }

        public float heightForStation(int stationID)
        {
            Dataset dataset = lastDatasetForEachStation[stationID];
            return tableController.heightForDataset(dataset);
        }


        public Dataset lastDatasetForStation(int stationID)
        {
            return lastDatasetForEachStation[stationID];
        }

       
        private void OnPolhemusButtonUp(Dataset Dataset)
        {
            //Console.WriteLine(@"Button of sensor #" + Dataset.Station + @" was released.");
        }

        private void OnPolhemusButtonDown(Dataset Dataset)
        {
            //Console.WriteLine(@"Button of sensor #" + Dataset.Station + @" was pressed.");
        }

        private void OnPolhemusMove(Dataset dataset)
        {
            //if (dataset.Station == 1)
             //   Console.WriteLine("x: " + dataset.Position.x + " y: " + dataset.Position.y + " z: " + dataset.Position.z);

            //if (dataset.Station == stationNumberToPrintHeight)
            //    Console.WriteLine("station " + stationNumberToPrintHeight 
            //        + " height is: " + dataset.XPos);
            lastDatasetForEachStation[dataset.Station] = dataset;
        }
    }
}
