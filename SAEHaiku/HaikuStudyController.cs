using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace SAEHaiku
{
    public enum HaikuStudyPosition { SideBySide, OppositeSides, Corner }; //opposite corners?
    public enum HaikuStudyCondition {
        Pens, OnePens, TwoPens,
        Cursors, Lines, LinesGrow, CartoonArms, ThinCartoonArms, CartoonArmsUnder,
        LinesSlowed, LinesVibrate, LinesVibrateOne, LinesVibrateTwo, LinesMouseVibrate, LinesBeltVibrate, 
        LinesSlowedLess, LinesSlowOne, LinesSlowTwo, LinesBlocking,
        PictureArms, PictureArmsTurnOff,
        ColorArms, ColorArmsTransparent, ThinColorArms,
        TransArms1, TransArms2, MouseVibration, PocketVibration, Slowed, Blocking
    };

    public class HaikuStudyController
    {
        private void loadConditions()
        {

            //conditions.Enqueue(HaikuStudyCondition.Pens);
            conditions.Enqueue(HaikuStudyCondition.ThinColorArms);
            conditions.Enqueue(HaikuStudyCondition.ColorArms);
            conditions.Enqueue(HaikuStudyCondition.ColorArmsTransparent);
            conditions.Enqueue(HaikuStudyCondition.PictureArms);

            
            conditions.Enqueue(HaikuStudyCondition.Slowed);
            conditions.Enqueue(HaikuStudyCondition.Blocking);
            conditions.Enqueue(HaikuStudyCondition.MouseVibration);
            conditions.Enqueue(HaikuStudyCondition.PocketVibration); 
            
            
            
            
                          
            List<HaikuStudyCondition> conds = conditions.ToArray<HaikuStudyCondition>().ToList<HaikuStudyCondition>();
            for (int i = 0; i < conds.Count; i++)
            {
                orderOfConditions += conds[i];
                orderOfConditions += "-";
            }
            orderOfConditions = orderOfConditions.Substring(0, orderOfConditions.Length - 1);

            Console.WriteLine(orderOfConditions);
        }


        private List<WordBoxCategory> nextCategories()
        {
            List<WordBoxCategory> nextCategories = new List<WordBoxCategory>();

            switch (conditions.Count)
            {
                    
                case 5:
                    nextCategories.Add(WordBoxCategory.Planet);
                    nextCategories.Add(WordBoxCategory.Horse);
                break;
                case 4:
                    nextCategories.Add(WordBoxCategory.Book);
                    nextCategories.Add(WordBoxCategory.Clothing);
                break;
                case 3:
                    nextCategories.Add(WordBoxCategory.Cat);
                    nextCategories.Add(WordBoxCategory.Coffee);
                break;
                case 2:
                    nextCategories.Add(WordBoxCategory.Tree);
                    nextCategories.Add(WordBoxCategory.Car);
                break;
                case 1:
                    nextCategories.Add(WordBoxCategory.Student);
                    nextCategories.Add(WordBoxCategory.Dog);
                break;
                case 0:
                    nextCategories.Add(WordBoxCategory.Chair);
                    nextCategories.Add(WordBoxCategory.Lake);
                break;

                default:
                    nextCategories.Add(WordBoxCategory.Tree);
                    nextCategories.Add(WordBoxCategory.Car);
                break;
            }
            
            return nextCategories;
        }

        public WordBoxCategory user1Category;
        public WordBoxCategory user2Category;
        public void startNextCondition()
        {
            wordBoxController.clearCurrentWordBoxes();
            currentCondition = conditions.Dequeue();

            if (currentCondition == HaikuStudyCondition.LinesSlowed
                || currentCondition == HaikuStudyCondition.LinesSlowedLess
                || currentCondition == HaikuStudyCondition.LinesSlowOne
                || currentCondition == HaikuStudyCondition.LinesSlowTwo
                || currentCondition == HaikuStudyCondition.LinesBlocking
                || currentCondition == HaikuStudyCondition.Blocking
                || currentCondition == HaikuStudyCondition.Slowed)
                isSpeedPenalty = true;
            else
                isSpeedPenalty = false;

            if ((currentCondition == HaikuStudyCondition.LinesVibrate
                || currentCondition == HaikuStudyCondition.LinesVibrateOne
                || currentCondition == HaikuStudyCondition.LinesVibrateTwo
                || currentCondition == HaikuStudyCondition.LinesMouseVibrate
                || currentCondition == HaikuStudyCondition.LinesBeltVibrate
                || currentCondition == HaikuStudyCondition.PocketVibration
                || currentCondition == HaikuStudyCondition.MouseVibration)
                && Program.isDebug == false)
            {
                isActuatePenalty = true;
                //Program.mainForm.phidgetController.setUpServos();
            }
            else
                isActuatePenalty = false;

            currentCategories.Clear();
            currentCategories = nextCategories();

            user1Category = currentCategories[1];
            user2Category = currentCategories[0];
        }


        public bool isInSetUpMode;
        public HaikuStudyPosition currentPosition;
        public HaikuStudyCondition currentCondition;
        //public HaikuStudyType studyType;
        public WordBoxController wordBoxController;

        static public Rectangle positionOneAreaRectangle;
        static public Rectangle positionTwoAreaRectangle;

        public List<WordBoxCategory> currentCategories;
        public Queue<HaikuStudyCondition> conditions;

        public bool isSpeedPenalty = false;
        public bool isActuatePenalty = false;
        public bool canTurnOffArm = false;

        int sessionID = 0;
		private string filenamePrefix = @"C:\Users\Scott\Documents\GitHub\SociallyAwkwardEmbodiments\SAEHaiku\";
        public HaikuStudyController(HaikuStudyPosition position)//, HaikuStudyType thisStudyType)
        {
            //studyType = thisStudyType;
            timestamps = new List<DateTime>();
            mouse1Locations = new List<Point>();
            mouse2Locations = new List<Point>();
            mouse1States = new List<bool>();
            mouse2States = new List<bool>();
            currentPosition = position;
            currentCategories = new List<WordBoxCategory>();

            string[] lines = System.IO.File.ReadAllLines(filenamePrefix + "SessionID.txt");
            sessionID = Convert.ToInt32(lines[0]);
            System.IO.StreamWriter sessionFileWriter = new System.IO.StreamWriter(filenamePrefix + "SessionID.txt", false);
            sessionFileWriter.WriteLine(sessionID + 1);
            sessionFileWriter.Flush();
            sessionFileWriter.Close();

            conditions = new Queue<HaikuStudyCondition>(); 
            loadConditions();
        }

        List<Point> mouse1Locations;
        List<Point> mouse2Locations;
        List<bool> mouse1States;
        List<bool> mouse2States;
        List<DateTime> timestamps;
        public void logMouseLocations(Point mouse1, Point mouse2, bool is1Down, bool is2Down, bool areBlocked)
        {
            timestamps.Add(DateTime.Now);
            mouse1Locations.Add(mouse1);
            mouse2Locations.Add(mouse2);
            mouse1States.Add(is1Down);
            mouse2States.Add(is2Down);

            MainLogEntry logEntry = new MainLogEntry(mouse1, mouse2, is1Down, is2Down, orderOfConditions, sessionID, currentCondition, areBlocked);
            mainLogEntries.Add(logEntry);
        }

        public void logPickOrDropEvent(Point mouse1, Point mouse2, bool is1Down, bool is2Down, WordBox box, bool pickUp, int user)
        {
            int isUser1Invading = 0, isUser2Invading = 0;

            if (positionTwoAreaRectangle.Contains(mouse1))
                isUser1Invading = 1;
            if (positionOneAreaRectangle.Contains(mouse2))
                isUser2Invading = 1;

            if (user == 1)
            {
                PickOrDropEvent pdEvent = new PickOrDropEvent(orderOfConditions, sessionID, currentCondition, pickUp, mouse1, user, isUser1Invading, box);
                HaikuStudyController.pickOrDropEventsLog.Add(pdEvent);
            }
            else if (user == 2)
            {
                PickOrDropEvent pdEvent = new PickOrDropEvent(orderOfConditions, sessionID, currentCondition, pickUp, mouse2, user, isUser2Invading, box);
                HaikuStudyController.pickOrDropEventsLog.Add(pdEvent);
            }

        }

        public bool areCrossing(Point location1, Point location2)
        {
            Utilities.Segment line1 = new Utilities.Segment();
            line1.Start = Program.user1Origin;
            line1.End = location1;
            Utilities.Segment line2 = new Utilities.Segment();
            line2.Start = Program.user2Origin;
            line2.End = location2;
            PointF? tempIntersectionPoint = Utilities.areSegmentsIntersecting(line1, line2);
            Point intersectionPoint = Point.Empty;

            if (tempIntersectionPoint != null)
                return true;

            return false;
        }

        public class CrossingEvent
        {
            public double timestamp;
            public int sessionID;
            public HaikuStudyCondition currentCondition;
            public double secondsSinceStart;

            double durationOfCrossing;
            double maxCrossAmount;

            public static string header()
            {
                return "timestamp" + ",sessionID" + ",currentCondition" + ",secondsSinceStart" + ",durationOfCrossing" + ",maxCrossAmount";
            }

            public string outputString()
            {
                return timestamp + "," + sessionID + "," + currentCondition + "," + secondsSinceStart + "," + durationOfCrossing + "," + maxCrossAmount;
            }

            public CrossingEvent(string conditionOrder, int newSessionID, HaikuStudyCondition newCondition, double duration, double maxCrossDist)
            {
                timestamp = (DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds;
                sessionID = newSessionID;
                currentCondition = newCondition;

                secondsSinceStart = (DateTime.Now - Utilities.experimentBeganAtTime).TotalSeconds;

                durationOfCrossing = duration;
                maxCrossAmount = maxCrossDist;
            }
        }

        public class PickOrDropEvent
        {
            public double timestamp;
            public int sessionID;
            public HaikuStudyCondition currentCondition;
            public double secondsSinceStart;

            int user;
            int pickUpEvent;
            int dropEvent;
            Point locationOfEvent;

            int isAnInvasion;

            WordBox box;

            public static string header()
            {
                return "timestamp" + ",sessionID" + ",currentCondition" + ",secondsSinceStart"
                    + ",user" + ",pickUpEvent" + ",dropEvent" + ",locationOfEventX" + ",locationOfEventY" + ",isAnInvasion"
                    + ",wordboxString" + ",wordboxOriginalLocationX" + ",wordboxOriginalLocationY"
                    + ",wordboxNewLocationX" + ",wordboxNewLocationY";
            }

            public string outputString()
            {
                return timestamp + "," + sessionID + "," + currentCondition + "," + secondsSinceStart
                    + "," + user
                    + "," + pickUpEvent + "," + dropEvent + "," + locationOfEvent.X + "," + locationOfEvent.Y + "," + isAnInvasion
                    + "," + box.textString + "," + box.originalLocation.X + "," + box.originalLocation.Y
                    + "," + box.Location.X + "," + box.Location.Y;
            }

            public PickOrDropEvent(string conditionOrder, int newSessionID, HaikuStudyCondition newCondition, bool isPickUp, Point location, int userNum, int isInvasion,
                WordBox inBox)
            {
                timestamp = (DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds;
                sessionID = newSessionID;
                currentCondition = newCondition;

                secondsSinceStart = (DateTime.Now - Utilities.experimentBeganAtTime).TotalSeconds;

                if (isPickUp)
                {
                    pickUpEvent = 1;
                    dropEvent = 0;
                }
                else
                {
                    pickUpEvent = 0;
                    dropEvent = 1;
                }

                locationOfEvent = location;

                user = userNum;
                isAnInvasion = isInvasion;

                box = WordBox.CreateWordBox("", -1.0f, WordBoxCategory.None);
                box.Location = new Point(-1, -1);
                box.originalLocation = new Point(-1, -1);

                if (inBox != null)
                {
                    box.Location = inBox.Location;
                    box.originalLocation = inBox.originalLocation;
                    box.setWord(inBox.textString);
                }
            }
        }

        
        public class MainLogEntry
        {
            public double timestamp;
            public int sessionID;
            public HaikuStudyCondition currentCondition;
            Point user1Origin, user2Origin, mouse1, mouse2;
            public int is1Dragging;
            public int is2Dragging;

            public double lengthOfEmbodiment1, lengthOfEmbodiment2;
            public double distanceMouse1ToOrigin2, distanceMouse2ToOrigin1;
            public int areCrossing, beginCrossing, endCrossing;
            public double durationOfCrossing;
            public Point crossingPoint;
            public double howMuch1Crossing, howMuch2Crossing;
            public double distanceBetweenEndPoints, distanceBetweenMouse1Embodiment2, distanceBetweenMouse2Embodiment1;
            public int isUser1OnOtherSide, isUser2OnOtherSide;
            public double secondsSinceBeginning;
            public string orderOfConditions;
            public int areTheyBlocked;

            int isUser1Invading, isUser2Invading;
            int isUser1OnAPaper, isUser2OnAPaper;
            static private bool wereCrossing = false;
            static private bool wereBlocked = false;

            public static string header()
            {
                return "timestamp" + "," + "sessionID" + "," + "currentCondition" + ","
                    + "user1OriginX" + "," + "user1OriginY" + "," + "mouse1X" + "," + "mouse1Y" + "," + "is1Dragging" + ","
                    + "user2OriginX" + "," + "user2OriginY" + "," + "mouse2X" + "," + "mouse2Y" + "," + "is2Dragging" + ","
                    + "lengthOfEmbodiment1" + "," + "lengthOfEmbodiment2" + ","
                    + "distanceMouse1ToOrigin2" + "," + "distanceMouse2ToOrigin1" + ","
                    + "areCrossing" + "," + "beginCrossing" + "," + "endCrossing" + "," + "durationOfCrossing" + ","
                    + "crossingPointX" + "," + "crossingPointY" + "," + "howMuch1Crossing" + "," + "howMuch2Crossing" + ","
                    + "distanceBetweenEndPoints" + "," + "distanceBetweenMouse1AndEmbodiment2" + "," + "distanceBetweenMouse2AndEmbodiment1"
                    + "," + "isUser1OnOtherSide" + "," + "isUser2OnOtherSide" + "," + "secondsSinceBeginning"
                    + "," + "orderOfConditions" + "," + "totalDistanceTravelled1" + "," + "totalDistanceTravelled2"
                    + "," + "isUser1Invading" + "," + "isUser2Invading"
                    + "," + "isUser1OnAPaper" + "," + "isUser2OnAPaper"
                    + "," + "areTheyBlocked";
            }

            public string outputString()
            {
                return timestamp + "," + sessionID + "," + currentCondition + ","
                    + user1Origin.X + "," + user1Origin.Y + "," + mouse1.X + "," + mouse1.Y + "," + is1Dragging + ","
                    + user2Origin.X + "," + user2Origin.Y + "," + mouse2.X + "," + mouse2.Y + "," + is2Dragging + ","
                    + lengthOfEmbodiment1 + "," + lengthOfEmbodiment2 + ","
                    + distanceMouse1ToOrigin2 + "," + distanceMouse2ToOrigin1 + ","
                    + areCrossing + "," + beginCrossing + "," + endCrossing + "," + durationOfCrossing + ","
                    + crossingPoint.X + "," + crossingPoint.Y + "," + howMuch1Crossing + "," + howMuch2Crossing + ","
                    + distanceBetweenEndPoints + "," + distanceBetweenMouse1Embodiment2 + "," + distanceBetweenMouse2Embodiment1 + ","
                    + isUser1OnOtherSide + "," + isUser2OnOtherSide + "," + secondsSinceBeginning
                    + "," + orderOfConditions + "," + user1TotalDistance + "," + user2TotalDistance
                    + "," + isUser1Invading + "," + isUser2Invading
                    + "," + isUser1OnAPaper + "," + isUser2OnAPaper
                    + "," + areTheyBlocked;
            }

            public static Point user1LastLocation = Point.Empty;
            public static Point user2LastLocation = Point.Empty;
            public static double user1TotalDistance = 0;
            public static double user2TotalDistance = 0;
            static double startSecondsOfCrossing = 0;
            public MainLogEntry(Point mouse1Location, Point mouse2Location, bool is1Down, bool is2Down, 
                string conditionOrder, int newSessionID, HaikuStudyCondition newCondition, bool areBlocked)
            {
                timestamp = (DateTime.Now - new DateTime (1970, 1, 1)).TotalMilliseconds;
                sessionID = newSessionID;
                currentCondition = newCondition;
                secondsSinceBeginning = (DateTime.Now - Utilities.experimentBeganAtTime).TotalSeconds;

                mouse1 = mouse1Location;
                mouse2 = mouse2Location;
                if (positionTwoAreaRectangle.Contains(mouse1Location))
                {
                    isUser1Invading = 1;
                    isUser1OnAPaper = 1;
                }
                else if (positionTwoAreaRectangle.Contains(mouse2Location))
                    isUser2OnAPaper = 1;

                if (positionOneAreaRectangle.Contains(mouse2Location))
                {
                    isUser2Invading = 1;
                    isUser2OnAPaper = 1;
                }
                else if (positionOneAreaRectangle.Contains(mouse1Location))
                    isUser1OnAPaper = 1;

                if (is1Down == true) is1Dragging = 1;
                else is1Dragging = 0;

                if (is2Down == true) is2Dragging = 1;
                else is2Dragging = 0;

                Point origin1 = Program.user1Origin;
                Point origin2 = Program.user2Origin;
                orderOfConditions = conditionOrder;
                lengthOfEmbodiment1 = Utilities.distanceBetweenPoints(mouse1Location, origin1);
                lengthOfEmbodiment2 = Utilities.distanceBetweenPoints(mouse2Location, origin2);

                distanceMouse1ToOrigin2 = Utilities.distanceBetweenPoints(mouse1Location, origin2);
                distanceMouse2ToOrigin1 = Utilities.distanceBetweenPoints(mouse2Location, origin1);

                //Are they crossing?
                double distance1Crossing = 0.0;
                double distance2Crossing = 0.0;
                Utilities.Segment line1 = new Utilities.Segment();
                line1.Start = origin1;
                line1.End = mouse1Location;
                Utilities.Segment line2 = new Utilities.Segment();
                line2.Start = origin2;
                line2.End = mouse2Location;

                durationOfCrossing = 0;

                crossingPoint = Point.Empty;
                PointF? tempIntersectionPoint = Utilities.areSegmentsIntersecting(line1, line2);
                Point intersectionPoint = Point.Empty;
                if (tempIntersectionPoint == null)
                {
                    beginCrossing = 0;
                    areCrossing = 0;

                    if (wereCrossing == true)
                    {
                        //not crossing. this is the first time we see them uncrossed
                        endCrossing = 1;    
                        wereCrossing = false;
                    }
                    else
                    {
                        //not crossing, reset
                        beginCrossing = 0;
                        endCrossing = 0;
                    }
                }
                else
                {
                    areCrossing = 1;
                    if (wereCrossing == false)
                    {
                        //first time we see this cross
                        beginCrossing = 1;
                        endCrossing = 0;
                        startSecondsOfCrossing = (DateTime.Now - Utilities.experimentBeganAtTime).TotalSeconds;
                        wereCrossing = true;
                    }
                    else
                    {
                        //still crossing, not first time
                        beginCrossing = 0;
                        endCrossing = 0;
                    }

                    intersectionPoint = new Point((int)tempIntersectionPoint.Value.X, (int)tempIntersectionPoint.Value.Y);
                    crossingPoint = intersectionPoint;
                    distance1Crossing = Utilities.distanceBetweenPoints(mouse1Location, intersectionPoint);
                    distance2Crossing = Utilities.distanceBetweenPoints(mouse2Location, intersectionPoint);
                    //Console.WriteLine("crossing with distances " + distance1Crossing + " " + distance2Crossing);
                }

                howMuch1Crossing = distance1Crossing;
                howMuch2Crossing = distance2Crossing;
                if (distance1Crossing > maxCrossAmount)
                    maxCrossAmount = distance1Crossing;
                if (distance2Crossing > maxCrossAmount)
                    maxCrossAmount = distance2Crossing;

                //Distance between the two endpoints
                distanceBetweenEndPoints = Utilities.distanceBetweenPoints(mouse1Location, mouse2Location);

                //Minimum distance between mouse1 and embodiment2
                distanceBetweenMouse1Embodiment2 = Utilities.getMinimumDistanceBetweenLineAndPoint(line2, mouse1Location.X, mouse1Location.Y);

                //Minimum distance between mouse2 and embodiment1
                distanceBetweenMouse2Embodiment1 = Utilities.getMinimumDistanceBetweenLineAndPoint(line1, mouse2Location.X, mouse2Location.Y);

                if (startSecondsOfCrossing > 0)
                    durationOfCrossing = (DateTime.Now - Utilities.experimentBeganAtTime).TotalSeconds - startSecondsOfCrossing;

                isUser1OnOtherSide = isUser2OnOtherSide = 0;
                if (mouse1Location.X > Program.tableWidth / 2.0)
                    isUser1OnOtherSide = 1;
                if (mouse2Location.X < Program.tableHeight / 2.0)
                    isUser2OnOtherSide = 1;
                
                if (user1LastLocation != Point.Empty)
                    user1TotalDistance += Utilities.distanceBetweenPoints(mouse1Location, user1LastLocation);
                if (user2LastLocation != Point.Empty)
                    user2TotalDistance += Utilities.distanceBetweenPoints(mouse2Location, user2LastLocation);

                user1LastLocation = mouse1Location;
                user2LastLocation = mouse2Location;

                if (endCrossing == 1)
                {
                    CrossingEvent cross = new CrossingEvent(conditionOrder, sessionID, currentCondition, durationOfCrossing, maxCrossAmount);
                    HaikuStudyController.crossingEventsLog.Add(cross);
                    startSecondsOfCrossing = 0;
                    maxCrossAmount = 0;
                }

                if (areBlocked == true)
                {
                    if (wereBlocked == false)
                        startSecondsOfCrossing = (DateTime.Now - Utilities.experimentBeganAtTime).TotalSeconds;

                    areTheyBlocked = 1;
                    wereBlocked = true;
                }
                else if (wereBlocked == true)
                {
                    durationOfCrossing = (DateTime.Now - Utilities.experimentBeganAtTime).TotalSeconds - startSecondsOfCrossing;
                    CrossingEvent cross = new CrossingEvent(conditionOrder, sessionID, currentCondition, durationOfCrossing, maxCrossAmount);
                    HaikuStudyController.crossingEventsLog.Add(cross);
                    startSecondsOfCrossing = 0;
                    wereBlocked = false;
                }

            }
            
            static private double maxCrossAmount = 0;
        }

        

        string orderOfConditions = "";

        public static List<PickOrDropEvent> pickOrDropEventsLog = new List<PickOrDropEvent>();
        public static List<CrossingEvent> crossingEventsLog = new List<CrossingEvent>();
        public static List<MainLogEntry> mainLogEntries = new List<MainLogEntry>();
        public void printOutLogs()
        {
            System.IO.StreamWriter mainLogFile = new System.IO.StreamWriter(sessionID + " " + (DateTime.Now- new DateTime(1970, 1, 1)).TotalMilliseconds + " " + currentCondition.ToString() + ".txt");
            mainLogFile.WriteLine(MainLogEntry.header());
            for (int i = 0; i < mainLogEntries.Count; i++)
                mainLogFile.WriteLine(mainLogEntries[i].outputString());


            System.IO.StreamWriter crossingsLogFile = new System.IO.StreamWriter(sessionID + " " + (DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds + " " + currentCondition.ToString() + " Crosses.txt");
            crossingsLogFile.WriteLine(CrossingEvent.header());
            for (int i = 0; i < crossingEventsLog.Count; i++)
                crossingsLogFile.WriteLine(crossingEventsLog[i].outputString());


            System.IO.StreamWriter pickOrDropLogFile = new System.IO.StreamWriter(sessionID + " " + (DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds + " " + currentCondition.ToString() + " PickOrDropEvents.txt");
            pickOrDropLogFile.WriteLine(PickOrDropEvent.header());
            for (int i = 0; i < pickOrDropEventsLog.Count; i++)
                pickOrDropLogFile.WriteLine(pickOrDropEventsLog[i].outputString());


            mainLogFile.Flush();
            mainLogFile.Close();

            crossingsLogFile.Flush();
            crossingsLogFile.Close();

            pickOrDropLogFile.Flush();
            pickOrDropLogFile.Close();


            clearTempLogs();
        }

        public void printOutHaikuWordLogs(List<WordBox> currentWordBoxes)
        {
            List<WordBox> user1WordBoxes = new List<WordBox>();
            List<WordBox> user2WordBoxes = new List<WordBox>();

            foreach (WordBox box in currentWordBoxes)
            {
                if (box.ownedByUser == 1)
                    user1WordBoxes.Add(box);
                else if (box.ownedByUser == 2)
                    user2WordBoxes.Add(box);
            }

            System.IO.StreamWriter haikuLog1 = new System.IO.StreamWriter(sessionID + " " + (DateTime.Now-new DateTime (1970, 1, 1)).TotalMilliseconds + " " + " user1 " + currentCondition.ToString() + " Haiku.txt");
            haikuLog1.WriteLine("sessionID" +"," + "currentCondition" + "," + "user" + "," + "userCategory" + ","
                + "word" + "," + "wordCategory" + "," + "wordOriginalX" + "," + "wordOriginalY" + "," + "reachedToOtherSide");

            System.IO.StreamWriter haikuLog2 = new System.IO.StreamWriter(sessionID + " " + (DateTime.Now - new DateTime(1970, 1, 1)).TotalMilliseconds + " " + " user2 " + currentCondition.ToString() + " Haiku.txt");
            haikuLog2.WriteLine("sessionID" + "," + "currentCondition" + "," + "user" + "," + "userCategory" + ","
                + "word" + "," + "wordCategory" + "," + "wordOriginalX" + "," + "wordOriginalY" + "," + "reachedToOtherSide");

            foreach (WordBox box in user1WordBoxes)
            {
                bool reached = false;
                if (user1Category == box.category)
                    reached = true;
                haikuLog1.WriteLine(sessionID + "," + currentCondition + "," + box.ownedByUser + "," + user1Category.ToString() + ","
                    + box.textString + "," + box.category + "," + box.originalLocation.X + "," + box.originalLocation.Y + "," + reached);
            }
            haikuLog1.Flush();
            haikuLog1.Close();

            foreach (WordBox box in user2WordBoxes)
            {
                bool reached = false;
                if (user2Category == box.category)
                    reached = true;
                haikuLog2.WriteLine(sessionID + "," + currentCondition + "," + box.ownedByUser + "," + user2Category.ToString() + ","
                    + box.textString + "," + box.category + "," + box.originalLocation.X + "," + box.originalLocation.Y + "," + reached);
            }
            haikuLog2.Flush();
            haikuLog2.Close();
        }

       

        private void clearTempLogs()
        {
            timestamps.Clear();
            mouse1Locations.Clear();
            mouse2Locations.Clear();
            mouse1States.Clear();
            mouse2States.Clear();

            MainLogEntry.user1LastLocation = Point.Empty;
            MainLogEntry.user2LastLocation = Point.Empty;
            MainLogEntry.user1TotalDistance = 0;
            MainLogEntry.user2TotalDistance = 0;

            mainLogEntries.Clear();
            crossingEventsLog.Clear();
            pickOrDropEventsLog.Clear();
        }

        

        public List<WordBox> currentWordBoxes()
        {
            List<WordBox> categoryOne = wordBoxController.getBoxesForCategory(currentCategories[0], 1);
            List<WordBox> categoryTwo = wordBoxController.getBoxesForCategory(currentCategories[1], 2);
            List<WordBox> joinerWordBoxes = wordBoxController.getBoxesForCategory(WordBoxCategory.Joiner, 0);

            List<WordBox> boxesToReturn = new List<WordBox>();
            foreach (WordBox box in categoryOne)
                boxesToReturn.Add(box);
            foreach (WordBox box in categoryTwo)
                boxesToReturn.Add(box);
            foreach (WordBox box in joinerWordBoxes)
                boxesToReturn.Add(box);

            return boxesToReturn;
        }

               
    }
}
