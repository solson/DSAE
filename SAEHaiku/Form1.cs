using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using GT.Net;
using KinectTableNet;
using TUIO;

namespace SAEHaiku
{
    
    public partial class Form1 : Form, TuioListener
    {
        WordBoxController wordBoxController;
        public HaikuStudyController studyController;
        PolhemusController polhemusController;
        public PhidgetController phidgetController;

        private Point user1Origin
        {
            get { return Program.user1Origin; }
            set { Program.user1Origin = value; }
        }

        private Point user2Origin
        {
            get { return Program.user2Origin; }
            set { Program.user2Origin = value; }
        }

        private const int SessionUpdatesChannelId = 0;
        private const int PointersChannelId = 1;
        private const int ControlChannelId = 2;
        private const int ClickChannelId = 3;
        private const int ArmImageChannelId = 4;
        private const int OriginsChannelId = 5;
        private const int ShowArmsChannelId = 6;
        private const int KinectCalibrationChannelId = 6;

        private ISessionChannel updates;
        private IStreamedTuple<int, int> coords;
        private IStringChannel control;
        private IStringChannel clicks;
        private IObjectChannel armImages;
        private IStreamedTuple<int, int> origins;
        private IStreamedTuple<bool> showArms;
        private IObjectChannel kinectCalibrationChannel;

        private GT.Net.Client client;

        private string host;
        private string port;

        // Which player the local user is (0 for player 1, 1 for player 2)
        private int playerID;

        // KinectTable fields
        private KinectData kinectData;
        private bool kinectDataIsFresh = false;
        private Hand currentHand;
        private KinectTableNet.Client kinectClient;
        private KinectCalibrationController kinectCalibration;
        private bool calibratingKinect = false;
        private const int kinectWidth = 640;
        private const int kinectHeight = 480;

        private Rectangle myArmRect;
        private Bitmap myArmImage;
        private Bitmap theirArmImage;
        private bool showMyArm = false;
        private bool showTheirArm = false;
        private Matrix theirCalibration;
        private DateTime lastArmImageFlush = DateTime.Now;

        // Kinect fingertip smoothing fields
        private const int recentCount = 30;
        private const int recentDistance = 25;
        private Queue<Point> recentOrigins = new Queue<Point>(recentCount);
        private Queue<Point> recentCursors = new Queue<Point>(recentCount);

        // TUIO fields
        private TuioClient tuioClient;
        private int tuioCursorID = -1;
        private Point tuioCursor;

        public Form1(PolhemusController newPolhemusController, PhidgetController newPhidgetController, string host, string port)
        {
            this.host = host;
            this.port = port;

            InitializeComponent();

            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer, true);

            polhemusController = newPolhemusController;
            phidgetController = newPhidgetController;

            this.BackColor = Color.Black;
            this.Size = new Size(Program.tableWidth, Program.tableHeight);

            studyController = new HaikuStudyController(HaikuStudyPosition.SideBySide);//, HaikuStudyType.RealArmsPictureArms);
            wordBoxController = new WordBoxController(studyController);

            if (studyController.isActuatePenalty == true)
            {
                //phidgetController.setUpServos();
            }

            //to snap words back to their original locations when dropped outside of a user's paper, set this to true.
            wordBoxController.boxesShouldSnapBack = true;

            studyController.wordBoxController = wordBoxController;
            studyController.currentCondition = HaikuStudyCondition.Cursors;
            studyController.isInSetUpMode = false;

            setMouseProperties();
            setUpEmbodiments();

            if (Program.kinectEnabled)
            {
                // Set up KinectTable
                kinectData = new KinectData();

                // Set up session parameters
                SessionParameters sessionParams = new SessionParameters(KinectDataParams.EnableType.All);
                sessionParams.DataParams.validityImageEnable = false;
                sessionParams.DataParams.testImageEnable = false;

                // Connect to a local Kinect and hook up to the data event
                kinectClient = KinectTableNet.KinectTable.ConnectLocal(sessionParams);
                kinectClient.DataReady += kinectClient_DataReady;

                // Set up Kinect calibration
                kinectCalibration = new KinectCalibrationController();
            }

            Cursor.Hide();

            Load += Form1_Load;
            FormClosed += Form1_FormClosed;

            playerID = 0;

            updateTimer = new Timer();
            updateTimer.Interval = 25;
            updateTimer.Tick += new EventHandler(updateTimer_Tick);
            updateTimer.Start();
        }

        void Form1_Load(object sender, EventArgs e)
        {
            if (Program.kinectEnabled)
                kinectClient.RecalculateTable();

            // Set up GT
            var config = new DefaultClientConfiguration();
            client = new GT.Net.Client(config);

            client.ErrorEvent += delegate(ErrorSummary es)
            {
                Console.WriteLine(es);

                if (es.Context is GT.Net.CannotConnectException)
                {
                    Cursor.Show();
                    MessageBox.Show(this, "Could not connect to server. Make sure the ClientRepeater is started.");
                    quitting = true;
                }
            };

            client.ConnexionRemoved += client_ConnexionRemoved;
            client.Start();

            updates = client.OpenSessionChannel(host, port, SessionUpdatesChannelId,
                ChannelDeliveryRequirements.SessionLike);
            updates.MessagesReceived += updates_SessionMessagesReceived;

            coords = client.OpenStreamedTuple<int, int>(host, port, PointersChannelId,
                TimeSpan.FromMilliseconds(25),
                ChannelDeliveryRequirements.AwarenessLike);
            coords.StreamedTupleReceived += coords_StreamedTupleReceived;

            control = client.OpenStringChannel(host, port, ControlChannelId,
                ChannelDeliveryRequirements.CommandsLike);
            control.MessagesReceived += control_MessagesReceived;

            clicks = client.OpenStringChannel(host, port, ClickChannelId,
                ChannelDeliveryRequirements.CommandsLike);
            clicks.MessagesReceived += clicks_MessagesReceived;

            armImages = client.OpenObjectChannel(host, port, ArmImageChannelId,
                ChannelDeliveryRequirements.AwarenessLike);
            armImages.MessagesReceived += armImages_MessagesReceived;

            origins = client.OpenStreamedTuple<int, int>(host, port, OriginsChannelId,
                TimeSpan.FromMilliseconds(25),
                ChannelDeliveryRequirements.AwarenessLike);
            origins.StreamedTupleReceived += origins_StreamedTupleReceived;

            showArms = client.OpenStreamedTuple<bool>(host, port, ShowArmsChannelId,
                TimeSpan.FromMilliseconds(25),
                ChannelDeliveryRequirements.AwarenessLike);
            showArms.StreamedTupleReceived += showArms_StreamedTupleReceived;

            kinectCalibrationChannel = client.OpenObjectChannel(host, port, KinectCalibrationChannelId,
                ChannelDeliveryRequirements.CommandsLike);
            kinectCalibrationChannel.MessagesReceived += kinectCalibrationChannel_MessagesReceived;

            if (Program.useTouch)
            {
                tuioClient = new TuioClient();
                tuioClient.addTuioListener(this);
                tuioClient.connect();
            }
        }

        // TuioListener methods
        public void addTuioObject(TuioObject o) { }
        public void updateTuioObject(TuioObject o) { }
        public void removeTuioObject(TuioObject o) { }
        public void refresh(TuioTime frameTime) { }

        bool tuioMouseDown = false;
        bool tuioMouseUp = false;
        public void addTuioCursor(TuioCursor c)
        {
            if (tuioCursorID == -1)
            {
                tuioCursorID = c.getCursorID();
                tuioCursor = new Point(c.getScreenX(Program.tableWidth), c.getScreenY(Program.tableHeight));
                tuioMouseDown = true;
            }
        }

        public void updateTuioCursor(TuioCursor c)
        {
            if (c.getCursorID() == tuioCursorID)
                tuioCursor = new Point(c.getScreenX(Program.tableWidth), c.getScreenY(Program.tableHeight));
        }

        public void removeTuioCursor(TuioCursor c)
        {
            if (c.getCursorID() == tuioCursorID)
            {
                tuioCursorID = -1;
                tuioCursor = new Point(c.getScreenX(Program.tableWidth), c.getScreenY(Program.tableHeight));
                tuioMouseUp = true;
            }
        }

        // Kinect methods
        private void kinectClient_DataReady(object sender, DataReadyEventArgs args)
        {
            args.GetData(out kinectData);
            kinectDataIsFresh = true;
        }

        private void handleKinectData()
        {
            kinectDataIsFresh = false;
            currentHand = null;

            var hands = kinectData.Hands.Where(hand => hand.FingerTips.Length > 0);

            if (hands.Count() == 0)
            {
                if (showMyArm)
                {
                    showMyArm = false;  // Hide local arm
                    showArms.X = false; // Hide arm on other client
                }

                return;
            }

            if (hands.Count() == 1)
            {
                currentHand = hands.First();
            }
            else
            {
                currentHand = hands
                    .OrderByDescending(h => h.MeanDepth).Take(2)
                    .OrderByDescending(h => h.Area).First();
            }

            if (studyController.currentCondition == HaikuStudyCondition.KinectPictureArms
                || studyController.currentCondition == HaikuStudyCondition.KinectPictureArmsPocketVibrate)
            {
                // Generate new arm image.
                myArmImage = new Bitmap(kinectWidth, kinectHeight, PixelFormat.Format24bppRgb);
                ImageFrameConverter.SetColorImage(myArmImage, kinectData.ColorImage);

                // Generate the arm mask.
                ImageFrame mask = currentHand.CreateArmBlob();
                var maskImg = new Bitmap(kinectWidth, kinectHeight, PixelFormat.Format1bppIndexed);
                ImageFrameConverter.SetBinaryImage(maskImg, mask);
                var maskBytes = ImageToByteArray(maskImg, ImageFormat.Png);

                myArmRect = trimArmImages(ref maskImg, ref myArmImage);

                maskImg.MakeTransparent(Color.White);

                using (Graphics img = Graphics.FromImage(myArmImage))
                {
                    img.CompositingMode = CompositingMode.SourceOver;
                    img.DrawImage(maskImg, 0, 0, myArmRect.Width, myArmRect.Height);
                }

                myArmImage.MakeTransparent(Color.Black);

                var imgBytes = ImageToByteArrayJpeg(myArmImage, 50);
                armImages.Send(new ArmImageMessage(imgBytes, maskBytes));

                if (DateTime.Now - lastArmImageFlush > TimeSpan.FromMilliseconds(100))
                {
                    armImages.Flush();
                    lastArmImageFlush = DateTime.Now;
                }

                if (!showMyArm)
                {
                    showMyArm = true;  // Show local arm
                    showArms.X = true; // Show arm on other client
                }
            }

            if (calibratingKinect)
                kinectCalibration.currentKinectLocation = currentHand.FingerTips.First();

            Point cursor = currentHand.FingerTips.First();

            if (kinectCalibration.calibrated)
                cursor = kinectCalibration.KinectToScreen(cursor);

            if (cursor.X < 0 || cursor.X > Program.tableWidth || cursor.Y < 0 || cursor.Y > Program.tableHeight)
                return;

            Point avgCursor = smoothInput(cursor, recentCursors, recentDistance);
            Cursor.Position = PointToScreen(avgCursor);

            Point origin = currentHand.ArmBase;

            // Only set the origin if KinectTable could find the arm base.
            if (origin.X != -1)
            {
                if (kinectCalibration.calibrated)
                    origin = kinectCalibration.KinectToScreen(origin);

                Point avgOrigin = smoothInput(origin, recentOrigins, recentDistance);

                // Create a box surrounding the window by 60px to attach arm origins to
                Rectangle box = new Rectangle(-60, -60, Program.tableWidth + 120, Program.tableHeight + 120);

                // Find angle between arm origin and cursor
                int xDiff = avgCursor.X - avgOrigin.X;
                int yDiff = avgCursor.Y - avgOrigin.Y;
                double theta = Math.Atan2(yDiff, xDiff);

                // Move the origin along the origin-cursor line to a point on the bounding box
                Point correctedOrigin = findIntersection(box, avgOrigin, theta);

                if (playerID == 0)
                    user1Origin = correctedOrigin;
                else if (playerID == 1)
                    user2Origin = correctedOrigin;

                origins.X = correctedOrigin.X;
                origins.Y = correctedOrigin.Y;
            }
        }

        private static Rectangle trimArmImages(ref Bitmap maskImage, ref Bitmap armImage)
        {
            Rectangle srcRect = default(Rectangle);
            BitmapData data = null;
            try
            {
                data = maskImage.LockBits(new Rectangle(0, 0, maskImage.Width, maskImage.Height), ImageLockMode.ReadOnly, PixelFormat.Format1bppIndexed);
                byte[] buffer = new byte[data.Height * data.Stride];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                int xMin = int.MaxValue,
                    xMax = int.MinValue,
                    yMin = int.MaxValue,
                    yMax = int.MinValue;

                bool foundPixel = false;

                // Find xMin
                for (int x = 0; x < data.Width; x++)
                {
                    bool stop = false;
                    for (int y = 0; y < data.Height; y++)
                    {
                        byte curByte = buffer[y * data.Stride + x / 8];
                        int shiftAmount = 7 - x % 8;
                        bool pixelSet = (curByte & (1 << shiftAmount)) != 0;

                        if (pixelSet)
                        {
                            xMin = x;
                            stop = true;
                            foundPixel = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Image is empty...
                if (!foundPixel)
                    return new Rectangle(0, 0, maskImage.Width, maskImage.Height);

                // Find yMin
                for (int y = 0; y < data.Height; y++)
                {
                    bool stop = false;
                    for (int x = xMin; x < data.Width; x++)
                    {
                        byte curByte = buffer[y * data.Stride + x / 8];
                        int shiftAmount = 7 - x % 8;
                        bool pixelSet = (curByte & (1 << shiftAmount)) != 0;

                        if (pixelSet)
                        {
                            yMin = y;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Find xMax
                for (int x = data.Width - 1; x >= xMin; x--)
                {
                    bool stop = false;
                    for (int y = yMin; y < data.Height; y++)
                    {
                        byte curByte = buffer[y * data.Stride + x / 8];
                        int shiftAmount = 7 - x % 8;
                        bool pixelSet = (curByte & (1 << shiftAmount)) != 0;

                        if (pixelSet)
                        {
                            xMax = x;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Find yMax
                for (int y = data.Height - 1; y >= yMin; y--)
                {
                    bool stop = false;
                    for (int x = xMin; x <= xMax; x++)
                    {
                        byte curByte = buffer[y * data.Stride + x / 8];
                        int shiftAmount = 7 - x % 8;
                        bool pixelSet = (curByte & (1 << shiftAmount)) != 0;

                        if (pixelSet)
                        {
                            yMax = y;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                srcRect = Rectangle.FromLTRB(xMin, yMin, xMax, yMax);
            }
            finally
            {
                if (data != null)
                    maskImage.UnlockBits(data);
            }

            Bitmap dest = new Bitmap(srcRect.Width, srcRect.Height, PixelFormat.Format24bppRgb);
            Rectangle destRect = new Rectangle(0, 0, srcRect.Width, srcRect.Height);
            using (Graphics graphics = Graphics.FromImage(dest))
            {
                graphics.DrawImage(maskImage, destRect, srcRect, GraphicsUnit.Pixel);
            }

            maskImage = dest;

            Bitmap armDest = new Bitmap(srcRect.Width, srcRect.Height, PixelFormat.Format24bppRgb);
            Rectangle armDestRect = new Rectangle(0, 0, srcRect.Width, srcRect.Height);
            using (Graphics graphics = Graphics.FromImage(armDest))
            {
                graphics.DrawImage(armImage, armDestRect, srcRect, GraphicsUnit.Pixel);
            }

            armImage = armDest;

            return srcRect;
        }

        private static Point findIntersection(Rectangle box, Point origin, double angle)
        {
            // Refer to http://stackoverflow.com/a/3197924
            int x1 = box.X;
            int y1 = box.Y;
            int x2 = box.X + box.Width;
            int y2 = box.Y + box.Height;

            int px = origin.X;
            int py = origin.Y;

            double vx = Math.Cos(angle);
            double vy = -Math.Sin(angle);

            double t = new double[] {
                (x1 - px) / vx,
                (x2 - px) / vx,
                (y1 - py) / vy,
                (y2 - py) / vy
            }.Select(x => Math.Abs(x)).Min();

            return new Point((int)(px + t * vx), (int)(py + t * vy));
        }

        private static Point smoothInput(Point current, Queue<Point> recent, int distance)
        {
            if (recent.Count == recentCount)
                recent.Dequeue();

            recent.Enqueue(current);

            Point avg = new Point();

            var near = recent.Where(point =>
                Math.Sqrt(Math.Pow(point.X - current.X, 2) + Math.Pow(point.Y - current.Y, 2)) < distance);

            avg.X = near.Sum(point => point.X) / near.Count();
            avg.Y = near.Sum(point => point.Y) / near.Count();

            return avg;
        }

        public static byte[] ImageToByteArray(Image image, ImageFormat format)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                image.Save(stream, format);
                return stream.ToArray();
            }
        }

        public static byte[] ImageToByteArrayJpeg(Image image, long quality)
        {
            EncoderParameters parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            ImageCodecInfo jpgEncoder = ImageCodecInfo.GetImageEncoders().Single(x => x.FormatDescription == "JPEG");

            using (var stream = new MemoryStream())
            {
                image.Save(stream, jpgEncoder, parameters);
                return stream.ToArray();
            }
        }

        public static Bitmap BitmapFromByteArray(byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
                return new Bitmap(Image.FromStream(stream));
        }

        private void client_ConnexionRemoved(Communicator c, IConnexion conn)
        {
            if (!IsDisposed && client.Connexions.Count == 0)
            {
                MessageBox.Show(this, "Disconnected from server", Text);
                Close();
            }
        }

        private void control_MessagesReceived(IStringChannel channel)
        {
            string cmd;
            while ((cmd = channel.DequeueMessage(0)) != null)
            {
                Console.WriteLine("Command received: " + cmd);
                doCommand(cmd);
            }
        }

        [Serializable]
        private struct ArmImageMessage
        {
            public byte[] Image;
            public byte[] Mask;

            public ArmImageMessage(byte[] img, byte[] mask)
            {
                Image = img;
                Mask = mask;
            }
        }

        private void armImages_MessagesReceived(IObjectChannel channel)
        {
            object obj;
            while ((obj = channel.DequeueMessage(0)) != null)
            {
                var msg = (ArmImageMessage)obj;

                theirArmImage = BitmapFromByteArray(msg.Image);

                var mask = BitmapFromByteArray(msg.Mask);
                mask.MakeTransparent(Color.White);

                using (Graphics img = Graphics.FromImage(theirArmImage))
                {
                    img.CompositingMode = CompositingMode.SourceOver;
                    img.DrawImage(mask, 0, 0, kinectWidth, kinectHeight);
                }

                theirArmImage.MakeTransparent(Color.Black);
            }
        }

        public void SendCalibration(Matrix matrix)
        {
            kinectCalibrationChannel.Send(matrix.Elements);
        }

        private void kinectCalibrationChannel_MessagesReceived(IObjectChannel channel)
        {
            object obj;
            while ((obj = channel.DequeueMessage(0)) != null)
            {
                float[] vals = (float[])obj;
                theirCalibration = new Matrix(vals[0], vals[1], vals[2], vals[3], vals[4], vals[5]);
            }
        }

        private void clicks_MessagesReceived(IStringChannel channel)
        {
            string click;
            while ((click = channel.DequeueMessage(0)) != null)
            {
                Console.WriteLine("Click received: " + click);

                string[] parts = click.Split(new char[] {' '}, 3);
                string button = parts[0];
                string type = parts[1];
                string player = parts[2];
                int clickingPlayerID = int.Parse(player);

                if (clickingPlayerID == playerID)
                    return;

                if (type == "down")
                {
                    if (button == "right")
                    {
                        if (playerID == 0)
                            user2RightDown = true;
                        else if (playerID == 1)
                            user1RightDown = true;
                    }
                    else
                    {
                        int otherPlayerID = (playerID == 0) ? 1 : 0;
                        toggleWordBoxUnderCursorNumberDragging(otherPlayerID + 1);
                    }
                }
                else if (type == "up")
                {
                    if (button == "right")
                    {
                        if (playerID == 0)
                            user2RightDown = false;
                        if (playerID == 1)
                            user1RightDown = false;
                    }
                    else
                    {
                        if (playerID == 1 && boxBeingDraggedByUser1 != null)
                        {
                            boxBeingDraggedByUser1.dropped();
                            boxBeingDraggedByUser1 = null;
                        }
                        if (playerID == 0 && boxBeingDraggedByUser2 != null)
                        {
                            boxBeingDraggedByUser2.dropped();
                            boxBeingDraggedByUser2 = null;
                        }
                    }
                }
            }
        }

        private void updates_SessionMessagesReceived(ISessionChannel channel)
        {
            SessionMessage m;
            while ((m = channel.DequeueMessage(0)) != null)
            {
                Console.WriteLine("Session: " + m);

                if (m.Action == SessionAction.Lives)
                {
                    // we get this message if other clients are already on the
                    // server, meaning we should be player 2
                    playerID = 1;
                }
            }
        }

        private void coords_StreamedTupleReceived(RemoteTuple<int, int> tuple, int clientId)
        {
            Point windowLocation = new Point(tuple.X, tuple.Y);

            if (playerID == 0)
            {
                user2MouseLocation = windowLocation;
                user2LastMousePositions.Enqueue(new PointAtTime(windowLocation, DateTime.Now));
            }
            else if (playerID == 1)
            {
                user1MouseLocation = windowLocation;
                user1LastMousePositions.Enqueue(new PointAtTime(windowLocation, DateTime.Now));
            }

            updateBoxLocations();
            //updateEmbodimentStuff();
        }

        private void origins_StreamedTupleReceived(RemoteTuple<int, int> tuple, int clientId)
        {
            Point windowLocation = new Point(tuple.X, tuple.Y);

            if (playerID == 0)
                user2Origin = windowLocation;
            else if (playerID == 1)
                user1Origin = windowLocation;
        }

        private void showArms_StreamedTupleReceived(RemoteTuple<bool> tuple, int clientId)
        {
            showTheirArm = tuple.X;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Program.kinectEnabled)
                KinectTableNet.KinectTable.Disconnect(kinectClient);

            client.Stop();
            client.Dispose();
        }

        private void Redraw()
        {
            BeginInvoke(new MethodInvoker(Invalidate));
        }

        Point user1MouseLocation = new Point(0, Program.tableHeight - Program.usableHeight);
        Point user2MouseLocation = new Point(0, Program.tableHeight - Program.usableHeight);

        enum Direction
        {
            Left, Right, Up, Down
        }

        bool user1AtFault = false;
        bool user2AtFault = false;

        bool quitting = false;
        Timer updateTimer;
        void updateTimer_Tick(object sender, EventArgs e)
        {
            if (client != null)
                client.Update();

            // If there is any new data, handle it.
            if (Program.kinectEnabled && kinectDataIsFresh)
                handleKinectData();

            if (quitting)
            {
                if (Program.isDebug == false)
                    PhidgetController.turnOffVibration();

                if (Program.useTouch)
                {
                    tuioClient.removeTuioListener(this);
                    tuioClient.disconnect();
                }

                Application.Exit();
            }

            if (tuioCursorID != -1 || tuioMouseUp)
            {
                handleMouseMove(tuioCursor);
                Cursor.Position = PointToScreen(tuioCursor);
            }

            if (tuioMouseDown)
            {
                handleMouseDown(false);
                tuioMouseDown = false;
            }

            if (tuioMouseUp)
            {
                handleMouseUp(false);
                tuioMouseUp = false;
            }

            if (Program.useVelocity)
            {
                // Remove old cursor positions
                var now = DateTime.Now;

                while (user1LastMousePositions.Count > 0
                    && now - user1LastMousePositions.First().Time > TimeSpan.FromMilliseconds(300))
                    user1LastMousePositions.Dequeue();

                while (user2LastMousePositions.Count > 0
                    && now - user2LastMousePositions.First().Time > TimeSpan.FromMilliseconds(300))
                    user2LastMousePositions.Dequeue();

                // Calculate cursor velocity
                if (user1LastMousePositions.Count > 1)
                {
                    Point start = user1LastMousePositions.First().Location;
                    Point end = user1LastMousePositions.Last().Location;
                    user1Velocity = Velocity.FromCursorPositions(start, end);
                }
                else
                {
                    user1Velocity = new Velocity(0, 0);
                }

                // Calculate cursor velocity
                if (user2LastMousePositions.Count > 1)
                {
                    Point start = user2LastMousePositions.First().Location;
                    Point end = user2LastMousePositions.Last().Location;
                    user2Velocity = Velocity.FromCursorPositions(start, end);
                }
                else
                {
                    user2Velocity = new Velocity(0, 0);
                }
            }

            Refresh();

            //if using Polhemus
            /*if ((studyController.currentCondition == HaikuStudyCondition.Pens
                || studyController.currentCondition == HaikuStudyCondition.OnePens
                || studyController.currentCondition == HaikuStudyCondition.TwoPens)
                && Program.isDebug == false
                && polhemusController.tableController.isCalibrated == true)
            {
                Point windowLocation1 = polhemusController.tableLocationForStation(1);
                Point windowLocation2 = polhemusController.tableLocationForStation(2);

                if (studyController.currentCondition == HaikuStudyCondition.OnePens
                    || studyController.currentCondition == HaikuStudyCondition.Pens)
                {
                    if (windowLocation1.X >= 0 && windowLocation1.X <= Program.tableWidth && windowLocation1.Y >= 0 && windowLocation1.Y <= Program.tableHeight)
                        user1MouseLocation = windowLocation1;
                    sdgManager1.Mice[0].Location = user1MouseLocation;
                }
                if (studyController.currentCondition == HaikuStudyCondition.TwoPens
                    || studyController.currentCondition == HaikuStudyCondition.Pens)
                {
                    if (windowLocation2.X >= 0 && windowLocation2.X <= Program.tableWidth && windowLocation2.Y >= 0 && windowLocation2.Y <= Program.tableHeight)
                        user2MouseLocation = windowLocation2;
                    if (sdgManager1.Mice.Count > 1)
                        sdgManager1.Mice[1].Location = user2MouseLocation;
                }
                updateBoxLocations();
                //updateEmbodimentStuff();
            }*/

            if (Program.useVelocity)
            {
                if (studyController.areCrossing(user1MouseLocation, user2MouseLocation))
                {
                    if (!user1AtFault && !user2AtFault)
                    {
                        Direction user1Dir;
                        if (user1Velocity.Angle > -Math.PI / 2 && user1Velocity.Angle < Math.PI / 2)
                            user1Dir = Direction.Right;
                        else
                            user1Dir = Direction.Left;

                        Direction user2Dir;
                        if (user2Velocity.Angle > -Math.PI / 2 && user2Velocity.Angle < Math.PI / 2)
                            user2Dir = Direction.Right;
                        else
                            user2Dir = Direction.Left;

                        if (user1Velocity.Magnitude < 50 && user2Velocity.Magnitude >= 50)
                            user2AtFault = true;
                        else if (user2Velocity.Magnitude < 50 && user1Velocity.Magnitude >= 50)
                            user1AtFault = true;
                        else if (user1Velocity.Magnitude > 50 && user1Velocity.Magnitude > user2Velocity.Magnitude && user1Dir == user2Dir)
                            user1AtFault = true;
                        else if (user2Velocity.Magnitude > 50 && user2Velocity.Magnitude > user1Velocity.Magnitude && user1Dir == user2Dir)
                            user2AtFault = true;
                        else
                            user1AtFault = user2AtFault = true;

                        if (user1AtFault && !user2AtFault)
                            Console.WriteLine("User 1 at fault.");
                        else if (user2AtFault && !user1AtFault)
                            Console.WriteLine("User 2 at fault.");
                        else
                            Console.WriteLine("Both at fault.");
                    }
                }
                else
                {
                    user1AtFault = false;
                    user2AtFault = false;
                }
            }
            else
            {
                user1AtFault = true;
                user2AtFault = true;
            }

            if (Program.isDebug == false)
            {
                //if touching and using a vibrate embodiment, vibrate
                PointF? intersection;
                if (studyController.areCrossing(user1MouseLocation, user2MouseLocation, out intersection) && studyController.isActuatePenalty == true
                    &&
                    (studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate
                    || studyController.currentCondition == HaikuStudyCondition.LinesBeltVibrate
                    || studyController.currentCondition == HaikuStudyCondition.PocketVibration
                    || studyController.currentCondition == HaikuStudyCondition.MouseVibration
                    || studyController.currentCondition == HaikuStudyCondition.KinectPictureArmsPocketVibrate))
                {
                    if (Program.useScaledVibration)
                    {
                        Point intersectionPoint = new Point((int)intersection.Value.X, (int)intersection.Value.Y);

                        if (playerID == 0 && user1AtFault)
                        {
                            double totalDist = Utilities.distanceBetweenPoints(user1Origin, user1MouseLocation);
                            double crossedDist = Utilities.distanceBetweenPoints(intersectionPoint, user1MouseLocation);

                            // Change smoothly from 50% vibration at just crossed to 100% vibration at 50% crossed.
                            double fractionCrossed = crossedDist / totalDist;
                            double amount = Math.Min(fractionCrossed + 0.5, 1);
                            PhidgetController.turnOnVibration(amount);
                        }
                        else if (playerID == 1 && user2AtFault)
                        {
                            double totalDist = Utilities.distanceBetweenPoints(user2Origin, user2MouseLocation);
                            double crossedDist = Utilities.distanceBetweenPoints(intersectionPoint, user2MouseLocation);

                            // Change smoothly from 50% vibration at just crossed to 100% vibration at 50% crossed.
                            double fractionCrossed = crossedDist / totalDist;
                            double amount = Math.Min(fractionCrossed + 0.5, 1);
                            PhidgetController.turnOnVibration(amount);
                        }
                    }
                    else if ((playerID == 0 && user1AtFault) || (playerID == 1 && user2AtFault))
                    {
                        PhidgetController.turnOnVibration();
                    }
                }
                else if (studyController.isActuatePenalty == true)
                    PhidgetController.turnOffVibration();
            }

            //do logging
            if(doLogging == true)
            {
                bool isdragging1 = false;
                bool isdragging2 = false;
                if (boxBeingDraggedByUser1 != null)
                    isdragging1 = true;
                if (boxBeingDraggedByUser2 != null)
                    isdragging2 = true;
                studyController.logMouseLocations(user1MouseLocation, user2MouseLocation, isdragging1, isdragging2, areTheyBlocked);
            }

        }

        private void setMouseProperties()
        {
            MouseDown += mouse_MouseDown;
            MouseMove += mouse_MouseMove;
            MouseUp += mouse_MouseUp;
        }
        private void hideMouseCursor()
        {
            Cursor.Hide();
        }
        private void showMouseCursor()
        {
            Cursor.Show();
        }
        
        WordBox boxBeingDraggedByUser1;
        WordBox boxBeingDraggedByUser2;
        void mouse_MouseUp(object sender, MouseEventArgs e)
        {
            handleMouseUp((e.Button & MouseButtons.Right) > 0);
        }

        public void handleMouseUp(bool right)
        {
            if (calibratingKinect)
                return;

            if (right)
            {
                if (playerID == 0)
                    user1RightDown = false;
                if (playerID == 1)
                    user2RightDown = false;

                clicks.Send("right up " + playerID);
            }
            else
            {
                if (playerID == 0 && boxBeingDraggedByUser1 != null)
                {
                    boxBeingDraggedByUser1.dropped();
                    boxBeingDraggedByUser1 = null;
                }
                if (playerID == 1 && boxBeingDraggedByUser2 != null)
                {
                    boxBeingDraggedByUser2.dropped();
                    boxBeingDraggedByUser2 = null;
                }

                clicks.Send("left up " + playerID);
            }
        }

        //Magic code so that the dragging doesn't crash
        private delegate void GetChildDelegate1(Point MouseLocation);
        private void GetChild1(Point MouseLocation)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new GetChildDelegate1(GetChild1), MouseLocation);
            }
            else
            {
                Control clickedControl = this.getWordBoxAtPoint(user1MouseLocation);
                if (clickedControl != null)
                {
                    boxBeingDraggedByUser1 = (WordBox)clickedControl;
                    boxBeingDraggedByUser1.BringToFront();
                }
            }
        }
        private delegate void GetChildDelegate2(Point MouseLocation);
        private void GetChild2(Point MouseLocation)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new GetChildDelegate2(GetChild2), MouseLocation);
            }
            else
            {
                Control clickedControl = this.getWordBoxAtPoint(user2MouseLocation);
                if (clickedControl != null)
                {
                    boxBeingDraggedByUser2 = (WordBox)clickedControl;
                    boxBeingDraggedByUser2.BringToFront();
                }
            }
        }
        //end magic code

        public void toggleWordBoxUnderCursorNumberDragging(int cursorNumber)
        {
            bool isUser1Dragging = false;
            bool isUser2Dragging = false;
            
            if (boxBeingDraggedByUser1 == null)
                isUser1Dragging = true;
            if (boxBeingDraggedByUser2 == null)
                isUser2Dragging = true;

            if (cursorNumber == 1)
            {
                if (boxBeingDraggedByUser1 == null)
                {
                    GetChild1(user1MouseLocation);
                    if (boxBeingDraggedByUser1 != null)
                    {
                        if (boxBeingDraggedByUser2 == boxBeingDraggedByUser1)
                            boxBeingDraggedByUser2 = null;

                        // Reorder the boxes so the grabbed one is drawn on top
                        currentWordBoxes.Remove(boxBeingDraggedByUser1);
                        currentWordBoxes.Add(boxBeingDraggedByUser1);

                        boxBeingDraggedByUser1.beginDragging(user1MouseLocation, 1);
                        studyController.logPickOrDropEvent(user1MouseLocation, user2MouseLocation, isUser1Dragging, isUser2Dragging,
                            boxBeingDraggedByUser1, true, 1);
                    }
                }
                else
                {
                    boxBeingDraggedByUser1.dropped();
                    studyController.logPickOrDropEvent(user1MouseLocation, user2MouseLocation, isUser1Dragging, isUser2Dragging,
                            boxBeingDraggedByUser1, false, 1);
                    boxBeingDraggedByUser1 = null;
                }
            }
            else if (cursorNumber == 2)
            {
                if (boxBeingDraggedByUser2 == null)
                {
                    GetChild2(user2MouseLocation);
                    if (boxBeingDraggedByUser2 != null)
                    {
                        if (boxBeingDraggedByUser1 == boxBeingDraggedByUser2)
                            boxBeingDraggedByUser1 = null;

                        // Reorder the boxes so the grabbed one is drawn on top
                        currentWordBoxes.Remove(boxBeingDraggedByUser2);
                        currentWordBoxes.Add(boxBeingDraggedByUser2);

                        boxBeingDraggedByUser2.beginDragging(user2MouseLocation, 2);
                        studyController.logPickOrDropEvent(user1MouseLocation, user2MouseLocation, isUser1Dragging, isUser2Dragging,
                            boxBeingDraggedByUser2,true, 2);
                    }
                }
                else
                {
                    boxBeingDraggedByUser2.dropped();
                    studyController.logPickOrDropEvent(user1MouseLocation, user2MouseLocation, isUser1Dragging, isUser2Dragging,
                            boxBeingDraggedByUser2, false, 2);
                    boxBeingDraggedByUser2 = null;
                }
            }
        }

        WordBox getWordBoxAtPoint(Point location)
        {
            const int boxPadding = 4;

            // Search the list in reverse (boxes drawn on top are at the end of the list)
            foreach (WordBox box in currentWordBoxes.Reverse<WordBox>())
            {
                double theta = -box.rotationAngle * Math.PI / 180;
                int px = location.X;
                int py = location.Y;
                int ox = box.Location.X + box.Size.Width / 2;
                int oy = box.Location.Y + box.Size.Height / 2;

                double x = Math.Cos(theta) * (px - ox) - Math.Sin(theta) * (py - oy) + ox;
                double y = Math.Sin(theta) * (px - ox) + Math.Cos(theta) * (py - oy) + oy;

                Point unrotatedLocation = new Point((int)Math.Round(x), (int)Math.Round(y));
                Point unrotatedBoxLocation = new Point(ox - box.wordBoxSize.Width / 2 - boxPadding, oy - box.wordBoxSize.Height / 2 - boxPadding);
                Size wordBoxSize = new Size(box.wordBoxSize.Width + boxPadding * 2, box.wordBoxSize.Height + boxPadding * 2);
                Rectangle rect = new Rectangle(unrotatedBoxLocation, wordBoxSize);

                if (rect.Contains(unrotatedLocation))
                    return box;
            }

            return null;
        }

        bool user1RightDown = false;
        bool user2RightDown = false;
        void mouse_MouseDown(object sender, MouseEventArgs e)
        {
            handleMouseDown((e.Button & MouseButtons.Right) > 0);
        }

        public void handleMouseDown(bool right)
        {
            if (calibratingKinect)
            {
                kinectCalibration.RecordPosition();

                if (kinectCalibration.calibrated)
                    calibratingKinect = false;

                return;
            }

            if (right)
            {
                if (playerID == 0)
                    user1RightDown = true;
                else if (playerID == 1)
                    user2RightDown = true;

                clicks.Send("right down " + playerID);
            }
            else
            {
                toggleWordBoxUnderCursorNumberDragging(playerID + 1);

                clicks.Send("left down " + playerID);
            }

            //Console.WriteLine(mouse.Text + " mouse down at location " + mouse.Location);
        }

        private struct PointAtTime
        {
            public Point Location;
            public DateTime Time;

            public PointAtTime(Point location, DateTime time)
            {
                Location = location;
                Time = time;
            }
        }

        private struct Velocity
        {
            public double Magnitude;
            public double Angle;

            public Velocity(double magnitude, double angle)
            {
                Magnitude = magnitude;
                Angle = angle;
            }

            public static Velocity FromCursorPositions(Point start, Point end)
            {
                int diffX = end.X - start.X;
                int diffY = end.Y - start.Y;

                double angle = Math.Atan2(diffY, diffX);
                double magnitude = Math.Sqrt(Math.Pow(diffX, 2) + Math.Pow(diffY, 2));

                return new Velocity(magnitude, angle);
            }
        }

        private Queue<PointAtTime> user1LastMousePositions = new Queue<PointAtTime>();
        private Queue<PointAtTime> user2LastMousePositions = new Queue<PointAtTime>();
        private Velocity user1Velocity = new Velocity(0, 0);
        private Velocity user2Velocity = new Velocity(0, 0);
        private Point user1LastMousePosition = Point.Empty;
        private Point user2LastMousePosition = Point.Empty;

        void mouse_MouseMove(object sender, MouseEventArgs e)
        {
            handleMouseMove(e.Location);
        }

        void handleMouseMove(Point windowLocation)
        {
            if (windowLocation.X < 0 || windowLocation.X > Program.tableWidth || windowLocation.Y < 0 || windowLocation.Y > Program.tableHeight)
                return;

            if (windowLocation.Y < (Program.tableHeight - Program.usableHeight))
            {
                Cursor.Position = new Point(windowLocation.X, (Program.tableHeight - Program.usableHeight));
                return;
            }

            if (playerID == 0)
            {
                user1MouseLocation = windowLocation;

                if (user1MouseLocation == user1LastMousePosition)
                    return;
            }
            else if (playerID == 1)
            {
                user2MouseLocation = windowLocation;

                if (user2MouseLocation == user2LastMousePosition)
                    return;
            }

            //if embodiments are touching and this embodiment should do something about it
            if (studyController.areCrossing(user1MouseLocation, user2MouseLocation) && studyController.isSpeedPenalty == true
                && (studyController.currentCondition != HaikuStudyCondition.Cursors && studyController.currentCondition != HaikuStudyCondition.Pens))
            {
                bool doit = true;
                if (studyController.currentCondition == HaikuStudyCondition.PictureArms
                    && (user1RightDown == true || user2RightDown == true))
                {
                    doit = false;
                }

                if (doit)
                {
                    float molassesValue = 25.0f;
                    if (studyController.currentCondition == HaikuStudyCondition.LinesSlowedLess)
                        molassesValue = 4.0f;

                    bool slowDown1 = true, slowDown2 = true;
                    float dx, dy;

                    if (studyController.currentCondition == HaikuStudyCondition.LinesSlowTwo)
                        slowDown1 = false;

                    if (studyController.currentCondition == HaikuStudyCondition.LinesSlowOne)
                        slowDown2 = false;

                    if (playerID == 0 && slowDown1)
                    {
                        // for player 1
                        dx = user1LastMousePosition.X - user1MouseLocation.X;
                        dy = user1LastMousePosition.Y - user1MouseLocation.Y;
                        dx = dx / molassesValue;
                        dy = dy / molassesValue;

                        if (studyController.currentCondition == HaikuStudyCondition.LinesBlocking
                            || studyController.currentCondition == HaikuStudyCondition.Blocking)
                        {
                            dx = dy = 0;
                            areTheyBlocked = true;
                        }

                        user1MouseLocation.X = (int)(user1LastMousePosition.X - dx);
                        user1MouseLocation.Y = (int)(user1LastMousePosition.Y - dy);
                    }
                    else if (playerID == 1 && slowDown2)
                    {
                        // for player 2
                        dx = user2LastMousePosition.X - user2MouseLocation.X;
                        dy = user2LastMousePosition.Y - user2MouseLocation.Y;
                        dx = dx / molassesValue;
                        dy = dy / molassesValue;

                        if (studyController.currentCondition == HaikuStudyCondition.LinesBlocking
                            || studyController.currentCondition == HaikuStudyCondition.Blocking)
                        {
                            dx = dy = 0;
                            areTheyBlocked = true;
                        }

                        user2MouseLocation.X = (int)(user2LastMousePosition.X - dx);
                        user2MouseLocation.Y = (int)(user2LastMousePosition.Y - dy);
                    }

                    if (playerID == 0)
                        Cursor.Position = user1MouseLocation;
                    else if (playerID == 1)
                        Cursor.Position = user2MouseLocation;
                }
            }
            else
            {
                //are not crossing
                areTheyBlocked = false;
            }

            updateBoxLocations();
            //updateEmbodimentStuff();

            user1LastMousePosition = user1MouseLocation;
            user2LastMousePosition = user2MouseLocation;

            if (playerID == 0)
            {
                if (Program.useVelocity)
                    user1LastMousePositions.Enqueue(new PointAtTime(user1MouseLocation, DateTime.Now));

                coords.X = user1MouseLocation.X;
                coords.Y = user1MouseLocation.Y;
            }
            else if (playerID == 1)
            {
                if (Program.useVelocity)
                    user2LastMousePositions.Enqueue(new PointAtTime(user2MouseLocation, DateTime.Now));

                coords.X = user2MouseLocation.X;
                coords.Y = user2MouseLocation.Y;
            }
        }
        bool areTheyBlocked = false;

        void updateBoxLocations()
        {
            if (boxBeingDraggedByUser1 != null)
                boxBeingDraggedByUser1.moveToLocation(user1MouseLocation);
            
            if (boxBeingDraggedByUser2 != null)
                boxBeingDraggedByUser2.moveToLocation(user2MouseLocation);
        }


        List<WordBox> currentWordBoxes = new List<WordBox>();
        private void addWordBoxes()
        {
            currentWordBoxes.Clear();

            List<WordBox> wordBoxesToAdd = studyController.currentWordBoxes();
            foreach (WordBox box in wordBoxesToAdd)
                currentWordBoxes.Add(box);
        }

        private string menuOptionsAsString()
        {
            string textString = "";
            if (isInFullScreen == false)
                textString += "f  to go to full screen\n";
            else
            {
                if (Program.isDebug == false && polhemusController.tableController.isCalibrated == false)
                    textString += "c to calibrate\n";

                else
                {
                    textString += "s to start the experiment\n\n";
                    textString += "n for next condition\n";
                    textString += "     (d to finish each condition)\n";
                    textString += "p prints out the boxes and locations\n";
                }
                textString += "f  to go back to windowed\n";
            }

            if (Program.kinectEnabled)
                textString += "k to calibrate kinect\n";
            textString += "q to quit\n";
            return textString;
        }

        SolidBrush whiteBrush = new SolidBrush(Color.White);
        SolidBrush greenBrush = new SolidBrush(Color.LimeGreen);
        private void drawCalibrationPoints(Graphics g)
        {
            Font textFont = new Font("Helvetica", 20f);
            int baseHeight = Program.tableHeight - Program.usableHeight;
            int step = kinectCalibration.currentPoint;

            Point p = new Point(Program.tableWidth / 4, baseHeight + Program.usableHeight / 4);
            g.DrawString("1", textFont, step == 0 ? greenBrush : whiteBrush, new Point(p.X, p.Y - 100));

            p = new Point(3 * Program.tableWidth / 4, baseHeight + Program.usableHeight / 4);
            g.DrawString("2", textFont, step == 1 ? greenBrush : whiteBrush, new Point(p.X, p.Y - 100));

            p = new Point(3 * Program.tableWidth / 4, baseHeight + 3 * Program.usableHeight / 4);
            g.DrawString("3", textFont, step == 2 ? greenBrush : whiteBrush, new Point(p.X, p.Y + 50));

            p = new Point(Program.tableWidth / 4, baseHeight + 3 * Program.usableHeight / 4);
            g.DrawString("4", textFont, step == 3 ? greenBrush : whiteBrush, new Point(p.X, p.Y + 50));
        }

        Dictionary<int, Point> originsForUserBoxes()
        {
            Dictionary<int, Point> ret = new Dictionary<int, Point>();
            Point locationOne = Point.Empty;
            Point locationTwo = Point.Empty;
            switch (studyController.currentPosition)
            {
                case HaikuStudyPosition.SideBySide:
                    {
                        locationOne = new Point(this.Size.Width / 2 - sizeOfEachHaikuArea.Width - 120, this.Size.Height - sizeOfEachHaikuArea.Height - 20);
                        locationTwo = new Point(this.Size.Width / 2 + 120, this.Size.Height - sizeOfEachHaikuArea.Height - 20);

                        HaikuStudyController.positionOneAreaRectangle = new Rectangle(locationOne, sizeOfEachHaikuArea);
                        HaikuStudyController.positionTwoAreaRectangle = new Rectangle(locationTwo, sizeOfEachHaikuArea);
                       
                        break;
                    }
                case HaikuStudyPosition.Corner:
                    {
                        locationOne = new Point(this.Size.Width / 2 - this.sizeOfEachHaikuArea.Width / 2, this.Size.Height - sizeOfEachHaikuArea.Height - 20);
                        locationTwo = new Point(this.Size.Width - sizeOfEachHaikuArea.Height - 20, this.Size.Height / 2 - this.sizeOfEachHaikuArea.Width / 2 - 20);

                        HaikuStudyController.positionOneAreaRectangle = new Rectangle(locationOne, sizeOfEachHaikuArea);
                        HaikuStudyController.positionTwoAreaRectangle = new Rectangle(locationTwo, new Size(sizeOfEachHaikuArea.Height, sizeOfEachHaikuArea.Width));
                        break;
                    }
                case HaikuStudyPosition.OppositeSides:
                    {
                        locationOne = new Point(this.Size.Width / 2 - this.sizeOfEachHaikuArea.Width / 2, this.Size.Height - sizeOfEachHaikuArea.Height - 20);
                        locationTwo = new Point(this.Size.Width / 2 - this.sizeOfEachHaikuArea.Width / 2, 10);

                        HaikuStudyController.positionOneAreaRectangle = new Rectangle(locationOne, sizeOfEachHaikuArea);
                        HaikuStudyController.positionTwoAreaRectangle = new Rectangle(locationTwo, sizeOfEachHaikuArea);
                        break;
                    }

                default:
                    break;
            }
            ret.Add(1, locationOne);
            ret.Add(2, locationTwo);
            return ret;
        }

        Size sizeOfEachHaikuArea = new Size(400, 175);
        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // SDG WORKAROUND - CARL
            user1dx = user1MouseLocation.X - user1Origin.X;
            user1dy = user1MouseLocation.Y - user1Origin.Y;
            user2dx = user2MouseLocation.X - user2Origin.X;
            user2dy = user2MouseLocation.Y - user2Origin.Y;
            mouseAngle1 = Math.PI / 2 - Math.Atan2(user1dx, user1dy);
            mouseDistance1 = Math.Sqrt(Math.Pow(user1dx, 2) + Math.Pow(user1dy, 2));
            mouseAngle2 = Math.PI / 2 - Math.Atan2(user2dx, user2dy);
            mouseDistance2 = Math.Sqrt(Math.Pow(user2dx, 2) + Math.Pow(user2dy, 2));


            if (showMainMenu == true)
            {
                //draw the main menu
                string textString = menuOptionsAsString();
                Size sizeOfTextBox = g.MeasureString(textString, textFont).ToSize();
                Point originOfText = new Point(Program.tableWidth / 2 - sizeOfTextBox.Width / 2, Program.tableHeight / 2 - sizeOfTextBox.Height / 2);

                g.DrawString(textString, textFont, new SolidBrush(Color.White), originOfText);
            }
                //pohemus stuff
            else if (calibrating == true || calibratingKinect == true)
            {
                drawCalibrationPoints(g);
            }
            else if (calibrating == false && calibratingKinect == false)
            {
                if (xsDisplayed == true)
                {
                    this.Controls.Clear();
                    xsDisplayed = false;
                    showMainMenu = true;
                }
            }


            if (showUserRectangles == true)
                drawUserPapersInGraphics(g);

            if (showUserRectangles == true && drawEmbodimentsBelow == false)
                drawTextOnTopOfUserPapersInGraphics(g);

            if (drawEmbodimentsBelow == false)
            {
                foreach (WordBox box in currentWordBoxes)
                {
                   // if (box == boxBeingDraggedByUser1 || box == boxBeingDraggedByUser2)
                     //   continue;

                    box.paintToGraphics(g);
                }
            }


            //EMBODIMENTS
            if (showEmbodiments == true)
            {
                switch (studyController.currentCondition)
                {
                        //Pens are polhemus
                    case HaikuStudyCondition.TwoPens:
                        hideMouseCursor();

                        g.TranslateTransform(user1Origin.X, user1Origin.Y);
                        g.RotateTransform((float)(mouseAngle1 * 57.295));
                        if(user1RightDown == false)
                            g.DrawImage(pictureArmImage1, 0, -32, (int)mouseDistance1-150 , 80);
                        g.TranslateTransform((int)mouseDistance1, -80);
                        g.DrawImage(pictureArmHandImage1, -151, 48, 150, 80);
                        break;
                    case HaikuStudyCondition.OnePens:
                        hideMouseCursor();

                        g.TranslateTransform(user2Origin.X, user2Origin.Y);
                        g.RotateTransform((float)(mouseAngle2 * 57.295));
                        if (user2RightDown == false)
                            g.DrawImage(pictureArmImage2, 0, -32, (int)mouseDistance2-150, 80);
                        g.TranslateTransform((int)mouseDistance2, -80);
                        g.DrawImage(pictureArmHandImage2, -151, 48, 150, 80);
                        break;
                    case HaikuStudyCondition.Pens:
                        //showMouseCursors();
                        hideMouseCursor();
                        //draw cursors
                        //g.DrawImage(user1CursorBitmap, user1MouseLocation);
                        //g.DrawImage(user2CursorBitmap, user2MouseLocation);
                        break;

                    case HaikuStudyCondition.Cursors:
                        //showMouseCursors();
                        hideMouseCursor();
                        //draw cursors
                        //g.DrawImage(user1CursorBitmap, user1MouseLocation);
                        //g.DrawImage(user2CursorBitmap, user2MouseLocation);
                        break;

                    case HaikuStudyCondition.LinesBlocking:
                    case HaikuStudyCondition.LinesBeltVibrate:
                    case HaikuStudyCondition.LinesMouseVibrate:
                    case HaikuStudyCondition.LinesSlowOne:
                    case HaikuStudyCondition.LinesSlowTwo:
                    case HaikuStudyCondition.LinesVibrateOne:
                    case HaikuStudyCondition.LinesVibrateTwo:
                    case HaikuStudyCondition.LinesGrow:
                    case HaikuStudyCondition.LinesVibrate:
                    case HaikuStudyCondition.LinesSlowed:
                    case HaikuStudyCondition.LinesSlowedLess:
                    case HaikuStudyCondition.Lines:
                        hideMouseCursor();

                        int radiusOfLine = 2;
                        if (studyController.currentCondition == HaikuStudyCondition.LinesGrow)
                        {
                            if (studyController.areCrossing(user1LastMousePosition, user2LastMousePosition))
                                radiusOfLine = 10;
                        }

                        g.DrawLine(linePen1, user1Origin.X, user1Origin.Y, user1MouseLocation.X, user1MouseLocation.Y);
                        for (int i = 0; i < radiusOfLine; i++)
                        {
                            g.DrawLine(linePen1, user1Origin.X - i, user1Origin.Y, user1MouseLocation.X - i, user1MouseLocation.Y);
                            g.DrawLine(linePen1, user1Origin.X + i, user1Origin.Y, user1MouseLocation.X + i, user1MouseLocation.Y);
                        }

                        g.DrawLine(linePen2, user2Origin.X, user2Origin.Y, user2MouseLocation.X, user2MouseLocation.Y);
                        for (int i = 0; i < radiusOfLine; i++)
                        {
                            g.DrawLine(linePen2, user2Origin.X - i, user2Origin.Y, user2MouseLocation.X - i, user2MouseLocation.Y);
                            g.DrawLine(linePen2, user2Origin.X + i, user2Origin.Y, user2MouseLocation.X + i, user2MouseLocation.Y);
                        }

                        break;

                    case HaikuStudyCondition.ThinCartoonArms:
                    case HaikuStudyCondition.CartoonArmsUnder:
                    case HaikuStudyCondition.CartoonArms:
                        hideMouseCursor();
                        //showMouseCursors();
                       
                        RotateArmPolygon();
                        for (int i = 1; i < 14; i++) {
                            armPoints1[i].X = armPointsRotated1[i].X + user1dx + user1Origin.X;
                            armPoints1[i].Y = armPointsRotated1[i].Y + user1dy + user1Origin.Y;
                        }
                        armPoints1[0].X = armPointsRotated1[0].X + user1Origin.X;
                        armPoints1[0].Y = armPointsRotated1[0].Y + user1Origin.Y;
                        armPoints1[14].X = armPointsRotated1[14].X + user1Origin.X;
                        armPoints1[14].Y = armPointsRotated1[14].Y + user1Origin.Y;

                        for (int i = 1; i < 14; i++) {
                            armPoints2[i].X = armPointsRotated2[i].X + user2dx + user2Origin.X;
                            armPoints2[i].Y = armPointsRotated2[i].Y + user2dy + user2Origin.Y;
                        }
                        armPoints2[0].X = armPointsRotated2[0].X + user2Origin.X;
                        armPoints2[0].Y = armPointsRotated2[0].Y + user2Origin.Y;
                        armPoints2[14].X = armPointsRotated2[14].X + user2Origin.X;
                        armPoints2[14].Y = armPointsRotated2[14].Y + user2Origin.Y;

                        g.FillPolygon(brush1, armPoints1);
                        g.FillPolygon(brush2, armPoints2);

                        g.ResetTransform();
                        break;

                    case HaikuStudyCondition.PictureArmsTurnOff:
                        hideMouseCursor();

                        g.TranslateTransform(user1Origin.X, user1Origin.Y);
                        g.RotateTransform((float)(mouseAngle1 * 57.295));
                        if (studyController.canTurnOffArm == false ||
                           (studyController.canTurnOffArm == true && user1RightDown == false))
                            g.DrawImage(pictureArmImage1, 0, -60, (int)mouseDistance1-150 , 120);
                        g.TranslateTransform((int)mouseDistance1, -80);
                        g.DrawImage(pictureArmHandImage1, -151, 20, 150, 120);
                        //x, y, width, height
                        
                        g.ResetTransform();
                        
                        g.TranslateTransform(user2Origin.X, user2Origin.Y);
                        g.RotateTransform((float)(mouseAngle2 * 57.295));
                        if (studyController.canTurnOffArm == false ||
                            (studyController.canTurnOffArm == true && user2RightDown == false))
                            g.DrawImage(pictureArmImage2, 0, -60, (int)mouseDistance2-150, 120);
                        g.TranslateTransform((int)mouseDistance2, -80);
                        g.DrawImage(pictureArmHandImage2, -151, 20, 150, 120);

                        g.ResetTransform();
                        break;
                    case HaikuStudyCondition.PictureArms:
                        hideMouseCursor();
                        //showMouseCursors();

                        g.TranslateTransform(user1Origin.X, user1Origin.Y);
                        g.RotateTransform((float)(mouseAngle1 * 57.295));
                        g.DrawImage(pictureArmImage1, 0, -60, (int)mouseDistance1-150 , 120);
                        g.TranslateTransform((int)mouseDistance1, -80);
                        g.DrawImage(pictureArmHandImage1, -151, 20, 150, 120);
                        //x, y, width, height
                        
                        g.ResetTransform();
                        
                        g.TranslateTransform(user2Origin.X, user2Origin.Y);
                        g.RotateTransform((float)(mouseAngle2 * 57.295));
                        g.DrawImage(pictureArmImage2, 0, -60, (int)mouseDistance2-150, 120);
                        g.TranslateTransform((int)mouseDistance2, -80);
                        g.DrawImage(pictureArmHandImage2, -151, 20, 150, 120);

                        g.ResetTransform();
                        break;

                    case HaikuStudyCondition.ColorArms:
                        hideMouseCursor();
                        //showMouseCursors();

                        g.TranslateTransform(user1Origin.X, user1Origin.Y);
                        g.RotateTransform((float)(mouseAngle1 * 57.295));
                        g.DrawImage(colorArmImage1, 0, -60, (int)mouseDistance1-150 , 120);
                        g.TranslateTransform((int)mouseDistance1, -80);
                        g.DrawImage(colorArmHandImage1, -152, 20, 150, 120);
                        //x, y, width, height
                        
                        g.ResetTransform();
                        
                        g.TranslateTransform(user2Origin.X, user2Origin.Y);
                        g.RotateTransform((float)(mouseAngle2 * 57.295));
                        g.DrawImage(colorArmImage2, 0, -60, (int)mouseDistance2-150, 120);
                        g.TranslateTransform((int)mouseDistance2, -80);
                        g.DrawImage(colorArmHandImage2, -152, 20, 150, 120);

                        g.ResetTransform();
                        break;

                    case HaikuStudyCondition.ThinColorArms:
                        hideMouseCursor();
                        //showMouseCursors();

                        g.TranslateTransform(user1Origin.X, user1Origin.Y);
                        g.RotateTransform((float)(mouseAngle1 * 57.295));
                        g.DrawImage(colorArmImage1, 0, -4, (int)mouseDistance1 - 15, 6);
                        g.TranslateTransform((int)mouseDistance1, -4);
                        g.DrawImage(colorArmHandImage1, -17, /*change this number*/0 , 15, 6);
                        //x, y, width, height
                        
                        g.ResetTransform();

                        g.TranslateTransform(user2Origin.X, user2Origin.Y);
                        g.RotateTransform((float)(mouseAngle2 * 57.295));
                        g.DrawImage(colorArmImage2, 0, -4, (int)mouseDistance2 - 15, 6);
                        g.TranslateTransform((int)mouseDistance2, -4);
                        g.DrawImage(colorArmHandImage2, -52, /*change this number*/0 , 15, 6);

                        g.ResetTransform();
                        break;

                    case HaikuStudyCondition.KinectPictureArms:
                    case HaikuStudyCondition.KinectPictureArmsPocketVibrate:
                        if (showTheirArm)
                        {
                            if (theirCalibration != null)
                                g.Transform = theirCalibration;

                            g.DrawImage(theirArmImage, 0, 0);
                        }

                        if (showMyArm)
                        {
                            if (Program.kinectEnabled && kinectCalibration.calibrated)
                                g.Transform = kinectCalibration.Matrix;

                            g.DrawImage(myArmImage, myArmRect.X, myArmRect.Y);
                        }

                        g.ResetTransform();

                        break;

                    case HaikuStudyCondition.Blocking:
                    case HaikuStudyCondition.Slowed:
                    case HaikuStudyCondition.PocketVibration:
                    case HaikuStudyCondition.MouseVibration:
                    case HaikuStudyCondition.TransArms1:
                    case HaikuStudyCondition.TransArms2:
                    case HaikuStudyCondition.ColorArmsTransparent:
                        hideMouseCursor();
                        //showMouseCursors();

                        

                        g.TranslateTransform(user1Origin.X, user1Origin.Y);
                        g.RotateTransform((float)(mouseAngle1 * 57.295));
                        g.DrawImage(colorArmImage1Transparent, 0, -60, (int)mouseDistance1 - 150, 120);
                        g.TranslateTransform((int)mouseDistance1, -80);
                        g.DrawImage(colorArmHandImage1Transparent, -151, 20, 150, 120);
                        //x, y, width, height

                        g.ResetTransform();

                        g.TranslateTransform(user2Origin.X, user2Origin.Y);
                        g.RotateTransform((float)(mouseAngle2 * 57.295));
                        g.DrawImage(colorArmImage2Transparent, 0, -60, (int)mouseDistance2 - 150, 120);
                        g.TranslateTransform((int)mouseDistance2, -80);
                        g.DrawImage(colorArmHandImage2Transparent, -151, 20, 150, 120);

                        g.ResetTransform();
                        break;
                }
                //this.Invalidate(false);
            }

            //never goes in
            if (showUserRectangles == true && drawEmbodimentsBelow == true)
            {
                drawTextOnTopOfUserPapersInGraphics(g);
                /*
                if (HaikuStudyController.positionOneAreaRectangle.Contains(user1MouseLocation)
                    || HaikuStudyController.positionTwoAreaRectangle.Contains(user1MouseLocation))
                    g.DrawImage(user1CursorBitmap, user1MouseLocation);
                
                if (HaikuStudyController.positionOneAreaRectangle.Contains(user2MouseLocation)
                    || HaikuStudyController.positionTwoAreaRectangle.Contains(user2MouseLocation))
                    g.DrawImage(user2CursorBitmap, user2MouseLocation);
                 */
            }

            if (drawEmbodimentsBelow == true)
            {
                foreach (WordBox box in currentWordBoxes)
                {
                    if (box == boxBeingDraggedByUser1 || box == boxBeingDraggedByUser2)
                        continue;

                    box.paintToGraphics(g);
                }
            }

            /*
            // Draw velocities
            if (user1Velocity.Magnitude > 0)
            {
                Pen arrowPen = new Pen(Color.Red, 3);
                arrowPen.EndCap = LineCap.ArrowAnchor;

                int x = (int)(Math.Cos(user1Velocity.Angle) * user1Velocity.Magnitude);
                int y = (int)(Math.Sin(user1Velocity.Angle) * user1Velocity.Magnitude);
                Point end = new Point(user1MouseLocation.X + x, user1MouseLocation.Y + y);

                g.DrawLine(arrowPen, user1MouseLocation, end);
            }

            if (user2Velocity.Magnitude > 0)
            {
                Pen arrowPen = new Pen(Color.Blue, 3);
                arrowPen.EndCap = LineCap.ArrowAnchor;

                int x = (int)(Math.Cos(user2Velocity.Angle) * user2Velocity.Magnitude);
                int y = (int)(Math.Sin(user2Velocity.Angle) * user2Velocity.Magnitude);
                Point end = new Point(user2MouseLocation.X + x, user2MouseLocation.Y + y);

                g.DrawLine(arrowPen, user2MouseLocation, end);
            }

            Font velocityFont = new Font("Consolas", 16f, FontStyle.Bold);
            g.DrawString(string.Format("{0,3}", (int)user1Velocity.Magnitude), velocityFont, new SolidBrush(Color.Red), 0, 0);
            g.DrawString(string.Format("{0,3}", (int)user2Velocity.Magnitude), velocityFont, new SolidBrush(Color.Blue), 0, 20);
            */

            /*
            if (Program.kinectEnabled)
            {
                IEnumerable<Point> tableCorners = kinectData.TableInfo.Corners;
                if (kinectCalibration.calibrated)
                    tableCorners = tableCorners.Select(point => kinectCalibration.KinectToScreen(point));
                drawPoints(g, tableCorners, Color.Red, 10);

                Font handIdFont = new Font("Helvetica", 16f, FontStyle.Bold);
                foreach (var hand in kinectData.Hands)
                {
                    var color = hand == currentHand ? Color.Blue : Color.Yellow;
                    var palmCenter = kinectCalibration.calibrated ? kinectCalibration.KinectToScreen(hand.PalmCenter) : hand.PalmCenter;
                    g.DrawString(hand.Id.ToString(), handIdFont, new SolidBrush(color), palmCenter);

                    IEnumerable<Point> fingerTips = hand.FingerTips;
                    if (kinectCalibration.calibrated)
                        fingerTips = fingerTips.Select(point => kinectCalibration.KinectToScreen(point));
                    drawPoints(g, fingerTips, color, 10);
                }
            }
            */

            /*if (boxBeingDraggedByUser1 != null)
                boxBeingDraggedByUser1.paintToGraphics(g);
            if (boxBeingDraggedByUser2 != null)
                boxBeingDraggedByUser2.paintToGraphics(g);
             * */
        }

        static void drawPoints(Graphics g, IEnumerable<Point> points, Color color, int size)
        {
            foreach (Point point in points)
            {
                int x = point.X - (size - 1);
                int y = point.Y - (size - 1);

                if (x < 0)
                    x = 0;
                if (y < 0)
                    y = 0;
                
                g.FillRectangle(new SolidBrush(color), x, y, size, size);
            }
        }

        void drawUserPapersInGraphics(Graphics g)
        {
            Dictionary<int, Point> locationsForUserAreas = originsForUserBoxes();
            Rectangle user1Rect = HaikuStudyController.positionOneAreaRectangle;
            Rectangle user2Rect = HaikuStudyController.positionTwoAreaRectangle;

            //draw the two white rectangles for user's piece of paper
            g.FillRectangle(new SolidBrush(Color.White), user1Rect);
            g.FillRectangle(new SolidBrush(Color.White), user2Rect);
        }

        Color color1 = Color.Purple;
        Color color2 = Color.DarkGreen;
        void drawTextOnTopOfUserPapersInGraphics(Graphics g)
        {
            Dictionary<int, Point> locationsForUserAreas = originsForUserBoxes();
            Rectangle user1Rect = HaikuStudyController.positionOneAreaRectangle;
            Rectangle user2Rect = HaikuStudyController.positionTwoAreaRectangle;

            Font userCategoryFont = new Font("Helvetica", 16f, FontStyle.Bold);
            string user1Category = studyController.user1Category.ToString();
            string user2Category = studyController.user2Category.ToString();
            Point locationOfUser1Category = locationsForUserAreas[1];
            Point locationOfUser2Category = locationsForUserAreas[2];
            locationOfUser1Category.X = locationOfUser1Category.X + user1Rect.Width / 2
                - (int)(g.MeasureString(user1Category, userCategoryFont).Width / 2.0);
            locationOfUser2Category.X = locationOfUser2Category.X + user2Rect.Width / 2
                - (int)(g.MeasureString(user2Category, userCategoryFont).Width / 2.0);
            locationOfUser1Category.Y += 5;
            locationOfUser2Category.Y += 5;

            // This if/else thing is a horrible hack, added by Jared. Sorry Andre.
            if (studyController.user1Category == WordBoxCategory.Horse ||
                studyController.user1Category == WordBoxCategory.Planet)
            {
                g.DrawString(user2Category, userCategoryFont, new SolidBrush(color1),
                locationOfUser1Category);
                g.DrawString(user1Category, userCategoryFont, new SolidBrush(color2),
                    locationOfUser2Category);
            }
            else
            {
                g.DrawString(user1Category, userCategoryFont, new SolidBrush(color1),
                    locationOfUser1Category);
                g.DrawString(user2Category, userCategoryFont, new SolidBrush(color2),
                    locationOfUser2Category);
            }

            Point user1LinePoint1 = new Point(user1Rect.Location.X + 28, user1Rect.Location.Y + 60);
            Point user1LinePoint2 = new Point(user1Rect.Location.X + user1Rect.Size.Width - 15, user1Rect.Location.Y + 60);
            Point user2LinePoint1 = new Point(user2Rect.Location.X + 28, user2Rect.Location.Y + 60);
            Point user2LinePoint2 = new Point(user2Rect.Location.X + user2Rect.Size.Width - 15, user2Rect.Location.Y + 60);

            g.DrawLine(new Pen(color1), user1LinePoint1, user1LinePoint2);
            g.DrawLine(new Pen(color2), user2LinePoint1, user2LinePoint2);

            Point user1TextLine1 = user1LinePoint1;
            Point user2TextLine1 = user2LinePoint1;
            user1TextLine1.X -= (int)(g.MeasureString("5", userCategoryFont).Width * 1.25);
            user2TextLine1.X -= (int)(g.MeasureString("5", userCategoryFont).Width * 1.25);
            user1TextLine1.Y -= (int)(g.MeasureString("5", userCategoryFont).Height * 0.75);
            user2TextLine1.Y -= (int)(g.MeasureString("5", userCategoryFont).Height * 0.75);
            g.DrawString("5", userCategoryFont, new SolidBrush(color1), user1TextLine1);
            g.DrawString("5", userCategoryFont, new SolidBrush(color2), user2TextLine1);

            user1LinePoint1.Y += 47;
            user1LinePoint2.Y += 47;
            user2LinePoint1.Y += 47;
            user2LinePoint2.Y += 47;
            user1TextLine1 = user1LinePoint1;
            user2TextLine1 = user2LinePoint1;
            g.DrawLine(new Pen(color1), user1LinePoint1, user1LinePoint2);
            g.DrawLine(new Pen(color2), user2LinePoint1, user2LinePoint2);
            user1TextLine1.X -= (int)(g.MeasureString("7", userCategoryFont).Width * 1.25);
            user2TextLine1.X -= (int)(g.MeasureString("7", userCategoryFont).Width * 1.25);
            user1TextLine1.Y -= (int)(g.MeasureString("7", userCategoryFont).Height * 0.75);
            user2TextLine1.Y -= (int)(g.MeasureString("7", userCategoryFont).Height * 0.75);
            g.DrawString("7", userCategoryFont, new SolidBrush(color1), user1TextLine1);
            g.DrawString("7", userCategoryFont, new SolidBrush(color2), user2TextLine1);

            user1LinePoint1.Y += 47;
            user1LinePoint2.Y += 47;
            user2LinePoint1.Y += 47;
            user2LinePoint2.Y += 47;
            user1TextLine1 = user1LinePoint1;
            user2TextLine1 = user2LinePoint1;
            g.DrawLine(new Pen(color1), user1LinePoint1, user1LinePoint2);
            g.DrawLine(new Pen(color2), user2LinePoint1, user2LinePoint2);
            user1TextLine1.X -= (int)(g.MeasureString("5", userCategoryFont).Width * 1.25);
            user2TextLine1.X -= (int)(g.MeasureString("5", userCategoryFont).Width * 1.25);
            user1TextLine1.Y -= (int)(g.MeasureString("5", userCategoryFont).Height * 0.75);
            user2TextLine1.Y -= (int)(g.MeasureString("5", userCategoryFont).Height * 0.75);
            g.DrawString("5", userCategoryFont, new SolidBrush(color1), user1TextLine1);
            g.DrawString("5", userCategoryFont, new SolidBrush(color2), user2TextLine1);

        }

		public static string filenamePrefix = HaikuStudyController.filenamePrefix;
        bool showEmbodiments = false;
        void setUpEmbodiments()
        {
            // Embodiment origins
            Dictionary<int, Point> origins = originsForUserBoxes();
            Program.user1Origin.X = origins[1].X + sizeOfEachHaikuArea.Width;
            Program.user1Origin.Y = origins[1].Y + sizeOfEachHaikuArea.Height + 60; // starts slightly below the screen
            Program.user2Origin.X = origins[2].X + sizeOfEachHaikuArea.Width;
            Program.user2Origin.Y = origins[2].Y + sizeOfEachHaikuArea.Height + 60; // starts slightly below the screen


            linePen1 = new Pen(Color.Purple);
            linePen2 = new Pen(Color.Green);
			//armImage = new Bitmap(filenamePrefix + "arm.png");
			//handImage = new Bitmap(filenamePrefix + "hand.png");
			pictureArmImage1 = new Bitmap (filenamePrefix + "arm1.png");
			pictureArmHandImage1 = new Bitmap (filenamePrefix + "hand1.png");
			pictureArmImage2 = new Bitmap (filenamePrefix + "arm2.png");
			pictureArmHandImage2 = new Bitmap (filenamePrefix + "hand2.png");

			colorArmImage1Transparent = new Bitmap (filenamePrefix + "arm1.png");
			colorArmHandImage1Transparent = new Bitmap (filenamePrefix + "hand1.png");
			colorArmImage2Transparent = new Bitmap (filenamePrefix + "arm2.png");
			colorArmHandImage2Transparent = new Bitmap (filenamePrefix + "hand2.png");

			Bitmap tempColorArmImage1 = new Bitmap (filenamePrefix + "arm1.png");
			Bitmap tempColorArmHandImage1 = new Bitmap (filenamePrefix + "hand1.png");
			Bitmap tempColorArmImage2 = new Bitmap (filenamePrefix + "arm2.png");
			Bitmap tempColorArmHandImage2 = new Bitmap (filenamePrefix + "hand2.png");

            int transparency = 150;
            //arm1
            for (int x = 0; x < tempColorArmImage1.Width; x++)
                for (int y = 0; y < tempColorArmImage1.Height; y++)
                    if (tempColorArmImage1.GetPixel(x, y).A > 0)
                    {
                        tempColorArmImage1.SetPixel(x, y, color1);
                        ((Bitmap)colorArmImage1Transparent).SetPixel(x, y, Color.FromArgb(transparency, color1));
                    }

            //hand1
            for (int x = 0; x < tempColorArmHandImage1.Width; x++)
                for (int y = 0; y < tempColorArmHandImage1.Height; y++)
                    if (tempColorArmHandImage1.GetPixel(x, y).A > 0)
                    {
                        tempColorArmHandImage1.SetPixel(x, y, color1);
                        ((Bitmap)colorArmHandImage1Transparent).SetPixel(x, y, Color.FromArgb(transparency, color1));
                    }

            //arm2
            for (int x = 0; x < tempColorArmImage2.Width; x++)
                for (int y = 0; y < tempColorArmImage2.Height; y++)
                    if (tempColorArmImage2.GetPixel(x, y).A > 0)
                    {
                        tempColorArmImage2.SetPixel(x, y, color2);
                        ((Bitmap)colorArmImage2Transparent).SetPixel(x, y, Color.FromArgb(transparency, color2));
                    }

            //hand2
            for (int x = 0; x < tempColorArmHandImage2.Width; x++)
                for (int y = 0; y < tempColorArmHandImage2.Height; y++)
                    if (tempColorArmHandImage2.GetPixel(x, y).A > 0)
                    {
                        tempColorArmHandImage2.SetPixel(x, y, color2);
                        ((Bitmap)colorArmHandImage2Transparent).SetPixel(x, y, Color.FromArgb(transparency, color2));
                    }
            
            colorArmImage1 = tempColorArmImage1;
            colorArmHandImage1 = tempColorArmHandImage1;
            colorArmImage2 = tempColorArmImage2;
            colorArmHandImage2 = tempColorArmHandImage2;
            
            telePointsOriginal1[0] = new PointF(0, 0);
            telePointsOriginal1[1] = new PointF(0, 24);
            telePointsOriginal1[2] = new PointF(18, 17);
            telePointsOriginal1[3] = new PointF(0, 0);
            telePointsOriginal2[0] = new PointF(0, 0);
            telePointsOriginal2[1] = new PointF(0, 24);
            telePointsOriginal2[2] = new PointF(18, 17);
            telePointsOriginal2[3] = new PointF(0, 0);

            for (int i = 0; i < 4; i++)
            {
                telePoints1[i] = new PointF(0, 0);
                telePoints2[i] = new PointF(0, 0);
            }
        }

        void setUpCartoonArmWithScaleFactor(double cartoonArmScaleFactor)
        {
            // set up cartoon arm polygon
            armPointsOriginal1[0] = new PointF(0 - 60, -6 + 4);
            armPointsOriginal1[1] = new PointF(0 - 60, -6 + 4);
            armPointsOriginal1[2] = new PointF(10 - 60, -11 + 4);
            armPointsOriginal1[3] = new PointF(25 - 60, -11 + 4);
            armPointsOriginal1[4] = new PointF(25 - 60, -6 + 4);
            armPointsOriginal1[5] = new PointF(60 - 60, -6 + 4);
            armPointsOriginal1[6] = new PointF(60 - 60, 2 + 4);
            armPointsOriginal1[7] = new PointF(30 - 60, 2 + 4);
            armPointsOriginal1[8] = new PointF(30 - 60, 24 + 4);
            armPointsOriginal1[9] = new PointF(25 - 60, 24 + 4);
            armPointsOriginal1[10] = new PointF(10 - 60, 24 + 4);
            armPointsOriginal1[11] = new PointF(0 - 60, 19 + 4);
            armPointsOriginal1[12] = new PointF(0 - 60, 19 + 4);
            armPointsOriginal1[13] = new PointF(0 - 60, 19 + 4);
            armPointsOriginal1[14] = new PointF(0 - 60, 19 + 4);

            armPointsOriginal2[0] = new PointF(0 - 60, -6 + 4);
            armPointsOriginal2[1] = new PointF(0 - 60, -6 + 4);
            armPointsOriginal2[2] = new PointF(10 - 60, -11 + 4);
            armPointsOriginal2[3] = new PointF(25 - 60, -11 + 4);
            armPointsOriginal2[4] = new PointF(25 - 60, -6 + 4);
            armPointsOriginal2[5] = new PointF(60 - 60, -6 + 4);
            armPointsOriginal2[6] = new PointF(60 - 60, 2 + 4);
            armPointsOriginal2[7] = new PointF(30 - 60, 2 + 4);
            armPointsOriginal2[8] = new PointF(30 - 60, 24 + 4);
            armPointsOriginal2[9] = new PointF(25 - 60, 24 + 4);
            armPointsOriginal2[10] = new PointF(10 - 60, 24 + 4);
            armPointsOriginal2[11] = new PointF(0 - 60, 19 + 4);
            armPointsOriginal2[12] = new PointF(0 - 60, 19 + 4);
            armPointsOriginal2[13] = new PointF(0 - 60, 19 + 4);
            armPointsOriginal2[14] = new PointF(0 - 60, 19 + 4);

            for (int i = 0; i < 15; i++)
            {
                armPointsOriginal1[i].X = (int)(armPointsOriginal1[i].X * cartoonArmScaleFactor);
                armPointsOriginal1[i].Y = (int)(armPointsOriginal1[i].Y * cartoonArmScaleFactor);
                armPointsOriginal2[i].X = (int)(armPointsOriginal2[i].X * cartoonArmScaleFactor);
                armPointsOriginal2[i].Y = (int)(armPointsOriginal2[i].Y * cartoonArmScaleFactor);

                armPoints1[i] = new PointF(0, 0);
                armPointsRotated1[i] = armPointsOriginal1[i];

                armPoints2[i] = new PointF(0, 0);
                armPointsRotated2[i] = armPointsOriginal2[i];
            }
        }

        void RotateArmPolygon()
        {
            for (int i = 0; i < 15; i++)
            {
                armPointsRotated1[i].X = armPointsOriginal1[i].X * (float)Math.Cos(mouseAngle1) - armPointsOriginal1[i].Y * (float)Math.Sin(mouseAngle1);
                armPointsRotated1[i].Y = armPointsOriginal1[i].X * (float)Math.Sin(mouseAngle1) + armPointsOriginal1[i].Y * (float)Math.Cos(mouseAngle1);

                armPointsRotated2[i].X = armPointsOriginal2[i].X * (float)Math.Cos(mouseAngle2) - armPointsOriginal2[i].Y * (float)Math.Sin(mouseAngle2);
                armPointsRotated2[i].Y = armPointsOriginal2[i].X * (float)Math.Sin(mouseAngle2) + armPointsOriginal2[i].Y * (float)Math.Cos(mouseAngle2);
            }
        }

        #region embodimentStuff
        Font textFont = new Font("Helvetica", 20f);
        SolidBrush brush1 = new SolidBrush(Color.MediumPurple);
        SolidBrush brush2 = new SolidBrush(Color.LimeGreen);
        SolidBrush blackBrush = new SolidBrush(Color.Black);
        Pen linePen1;
        Pen linePen2;

        //USER 1
        PointF[] telePointsOriginal1 = new PointF[4];
        PointF[] telePoints1 = new PointF[4];

        PointF[] armPointsOriginal1 = new PointF[15];
        PointF[] armPoints1 = new PointF[15];
        PointF[] armPointsRotated1 = new PointF[15];

        Image pictureArmImage1;
        Image pictureArmHandImage1;
        Image pictureArmImage2;
        Image pictureArmHandImage2;

        Image colorArmImage1;
        Image colorArmHandImage1;
        Image colorArmImage2;
        Image colorArmHandImage2;

        Image thincolorArmImage1;
        Image thincolorArmHandImage1;
        Image thincolorArmImage2;
        Image thincolorArmHandImage2;

        Image colorArmImage1Transparent;
        Image colorArmHandImage1Transparent;
        Image colorArmImage2Transparent;
        Image colorArmHandImage2Transparent;

        double mouseAngle1 = 0;
        double mouseDistance1 = 0;

        // arbitrary origin
        float user1dx;
        float user1dy;

        //USER 2
        PointF[] telePointsOriginal2 = new PointF[4];
        PointF[] telePoints2 = new PointF[4];

        PointF[] armPointsOriginal2 = new PointF[15];
        PointF[] armPoints2 = new PointF[15];
        PointF[] armPointsRotated2 = new PointF[15];

        double mouseAngle2 = 0;
        double mouseDistance2 = 0;

        // arbitrary origin
        float user2dx;
        float user2dy;
        #endregion


        //ignore, polhemus stuff
        private void displayXs()
        {
            int baseHeight = Program.tableHeight - Program.usableHeight;

            Point p = new Point(Program.tableWidth / 4, baseHeight + Program.usableHeight / 4);
            displayXAtLocation(p);

            p = new Point(Program.tableWidth * 3 / 4, baseHeight + Program.usableHeight / 4);
            displayXAtLocation(p);

            p = new Point(Program.tableWidth * 3 / 4, baseHeight + Program.usableHeight * 3 / 4);
            displayXAtLocation(p);

            p = new Point(Program.tableWidth / 4, baseHeight + Program.usableHeight * 3 / 4);
            displayXAtLocation(p);

            xsDisplayed = true;
        }

        //polhemus stuff
        private void displayXAtLocation(Point location)
        {
            PictureBox X = new PictureBox();
            X.Image = Properties.Resources.X;
            location.X -= 75 / 2;
            location.Y -= 75 / 2;
            X.Location = location;
            X.Size = new Size(75, 75);
            this.Controls.Add(X);
        }

       
        private bool showUserRectangles = false;
        public bool drawEmbodimentsBelow = false;
        public bool showMainMenu = true;
        private bool xsDisplayed = false;
        public bool calibrating = false;

        private bool alreadyStarted = false;
        private bool readyToStartNextCondition = true;
        private bool doLogging = false;
        private bool isInFullScreen = false;
        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case 'p':
                    wordBoxController.printOutCurrentBoxesToFiles();
                    break;
                case 'f':
                    if (isInFullScreen == false)
                    {
                        this.FormBorderStyle = FormBorderStyle.None;
                        this.Left = 0;
                        this.Top = 0;
                        this.Size = new Size(Program.tableWidth, Program.tableHeight);
                        isInFullScreen = true;
                    }
                    else
                    {
                        this.FormBorderStyle = FormBorderStyle.Sizable;
                        isInFullScreen = false;
                    }

                    this.Invalidate();
                    break;
                /*case 'c':
                    calibrating = true;
                    showMainMenu = false;
                    this.Invalidate();
                    displayXs();
                    break;*/
                case 'k':
                    if (Program.kinectEnabled)
                    {
                        kinectCalibration.StartCalibration();
                        calibratingKinect = true;
                        showMainMenu = false;
                        Invalidate();
                        displayXs();
                    }
                    break;
                case 't':
                    if (Program.kinectEnabled)
                        kinectClient.RecalculateTable();
                    break;
                case 'n':
                    sendCommand("next");
                    break;
                case 's':
                    sendCommand("start");
                    break;
                case 'd':
                    sendCommand("done");
                    break;
                case 'q':
                    sendCommand("quit");
                    break;
                default:
                    break;
            }
        }

        void sendCommand(string cmd)
        {
            control.Send(cmd);
            doCommand(cmd);
        }

        void doCommand(string cmd)
        {
            switch (cmd)
            {
                case "next":
                    if (readyToStartNextCondition == true && alreadyStarted == true)
                    {
                        doLogging = true;
                        showMainMenu = false;
                        showEmbodiments = true;
                        showUserRectangles = true;

                        addWordBoxes();

                        hideMouseCursor();
                        this.Invalidate();

                        readyToStartNextCondition = false;
                    }
                    break;
                case "start":
                    if (readyToStartNextCondition == true && alreadyStarted == false)
                    {
                        showEmbodiments = true;
                        showMainMenu = false;
                        studyController.startNextCondition();
                        alreadyStarted = true;
                        setUpCartoonsArms();
                    }

                    if (Program.kinectEnabled && kinectCalibration.calibrated)
                        SendCalibration(kinectCalibration.Matrix);

                    break;
                case "done":
                    if (readyToStartNextCondition == false)
                    {
                        if (doLogging == true)
                        {
                            studyController.printOutLogs();
                            studyController.printOutHaikuWordLogs(currentWordBoxes);
                            doLogging = false;
                        }
                        currentWordBoxes.Clear();

                        showUserRectangles = false;

                        if (studyController.conditions.Count > 0)
                        {
                            studyController.startNextCondition();
                            showEmbodiments = true;
                            setUpCartoonsArms();
                        }
                        else
                        {
                            showMainMenu = true;
                            showEmbodiments = false;
                        }

                        this.Invalidate();

                        readyToStartNextCondition = true;
                    }
                    break;
                case "quit":
                    quitting = true;
                    break;
                default:
                    break;
            }
        }

        void setUpCartoonsArms()
        {
            if (studyController.currentCondition == HaikuStudyCondition.CartoonArmsUnder)
                drawEmbodimentsBelow = true;
            else
                drawEmbodimentsBelow = false;

            if (studyController.currentCondition == HaikuStudyCondition.ThinCartoonArms)
                setUpCartoonArmWithScaleFactor(0.20);
            if (studyController.currentCondition == HaikuStudyCondition.CartoonArmsUnder
                || studyController.currentCondition == HaikuStudyCondition.CartoonArms)
                setUpCartoonArmWithScaleFactor(3.0);
        }
    }
}
