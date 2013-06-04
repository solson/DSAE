using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace SAEHaiku
{
    public enum Position { One, Two, Three, Four };
    //one == bottom, two == right, three == top, four == left

    public class WordBoxController
    {
        public bool boxesShouldSnapBack = true;

        HaikuStudyController studyController;

        private List<int> wordBoxOrientations;
        public Dictionary<HaikuStudyPosition, List<int>> validOrientations;

        public WordBoxController(HaikuStudyController newStudyController)
        {
            studyController = newStudyController;

            currentWordBoxes = new Dictionary<WordBoxCategory, List<WordBox>>();

            int[] orientations = { 0, 180, 90, 270, 45, 135, 225, 30, 210, 120, 300, 15, 195 };
            wordBoxOrientations = new List<int>(orientations.AsEnumerable<int>());

            validOrientations = new Dictionary<HaikuStudyPosition, List<int>>();

            validOrientations[HaikuStudyPosition.SideBySide] = new List<int>();
            validOrientations[HaikuStudyPosition.SideBySide].Add(0);
            validOrientations[HaikuStudyPosition.SideBySide].Add(45);
            validOrientations[HaikuStudyPosition.SideBySide].Add(30);
            validOrientations[HaikuStudyPosition.SideBySide].Add(15);
            validOrientations[HaikuStudyPosition.SideBySide].Add(300);

            validOrientations[HaikuStudyPosition.Corner] = new List<int>();
            validOrientations[HaikuStudyPosition.Corner].Add(0);
            validOrientations[HaikuStudyPosition.Corner].Add(45);
            validOrientations[HaikuStudyPosition.Corner].Add(30);
            validOrientations[HaikuStudyPosition.Corner].Add(15);
            validOrientations[HaikuStudyPosition.Corner].Add(300);

            validOrientations[HaikuStudyPosition.OppositeSides] = new List<int>();
            for (int i = 0; i < orientations.Length; i++)
                validOrientations[HaikuStudyPosition.OppositeSides].Add(orientations[i]);
        }

        public void clearCurrentWordBoxes()
        {
            currentWordBoxes.Clear();
        }

        public bool shouldBoxSnap(WordBox box)
        {
            
            if (HaikuStudyController.positionOneAreaRectangle.IntersectsWith(new Rectangle(box.Location, box.Size)))
                return true;
            if (HaikuStudyController.positionTwoAreaRectangle.IntersectsWith(new Rectangle(box.Location, box.Size)))
                return true;
            return false;
        }

        public int thisBoxIsOwnedByWhichUser(WordBox box)
        {
            if (HaikuStudyController.positionOneAreaRectangle.IntersectsWith(new Rectangle(box.Location, box.Size)))
                return 1;
            if (HaikuStudyController.positionTwoAreaRectangle.IntersectsWith(new Rectangle(box.Location, box.Size)))
                return 2;

            return 0;
        }

        public Position boxShouldSnapToWhichPosition(WordBox box)
        {
            switch (studyController.currentPosition)
            {
                case HaikuStudyPosition.SideBySide:
                    {
                        return Position.One;
                    }
                case HaikuStudyPosition.Corner:
                    {
                        if (HaikuStudyController.positionOneAreaRectangle.IntersectsWith(new Rectangle(box.Location, box.Size)))
                            return Position.One;
                        if (HaikuStudyController.positionTwoAreaRectangle.IntersectsWith(new Rectangle(box.Location, box.Size)))
                            return Position.Four;
                        break;
                    }
                case HaikuStudyPosition.OppositeSides:
                    {
                        if (HaikuStudyController.positionOneAreaRectangle.IntersectsWith(new Rectangle(box.Location, box.Size)))
                            return Position.One;
                        if (HaikuStudyController.positionTwoAreaRectangle.IntersectsWith(new Rectangle(box.Location, box.Size)))
                            return Position.Three;
                        break;
                    }
            }
            return Position.One;
        }

        private void addWordBoxToCategory(WordBox newWordBox, WordBoxCategory toCategory)
        {
            if (!currentWordBoxes.ContainsKey(toCategory))
                currentWordBoxes[toCategory] = new List<WordBox>();

            currentWordBoxes[toCategory].Add(newWordBox);
        }

        Dictionary<WordBoxCategory, List<WordBox>> currentWordBoxes;
        public List<WordBox> getBoxesForCategory(WordBoxCategory category, int categoryNumber)
        {
            if (!currentWordBoxes.ContainsKey(category))
            {
                loadWordsForCategory(category, categoryNumber);
            }
            return currentWordBoxes[category];
        }

		private string filenamePrefix = HaikuStudyController.filenamePrefix + @"SAEWords\";
        private Random random = new Random();
        public void loadWordsForCategory(WordBoxCategory category, int categoryNumber)
        {
            string filename = filenameForCategory(category);
            string[] lines = System.IO.File.ReadAllLines(filenamePrefix + filename + ".txt");
            
            foreach (string line in lines)
            {
                Point location = Point.Empty;
                
                int index = random.Next() % validOrientations[studyController.currentPosition].Count;
                WordBox wordbox;
                
                if (studyController.isInSetUpMode == false)
                {
                    string[] texts = line.Split(',');
                    wordbox = WordBox.CreateWordBox(texts[0], (float)Convert.ToDouble(texts[1]), category);
                    if (category == WordBoxCategory.Horse || category == WordBoxCategory.Planet
                        || lastCategoryLoaded == WordBoxCategory.Horse)
                    {
                        location.X = (int)((Convert.ToInt32(texts[2]) / 1280.0) * Program.tableWidth);
                        location.Y = (int)((Convert.ToInt32(texts[3]) / 960.0) * Program.usableHeight) + (Program.tableHeight - Program.usableHeight);
                    }
                    else
                    {
                        location.X = (int)((Convert.ToInt32(texts[2]) / 1024.0) * Program.tableWidth);
                        location.Y = (int)((Convert.ToInt32(texts[3]) / 768.0) * Program.usableHeight) + (Program.tableHeight - Program.usableHeight);
                    }
                    wordbox.Location = location;
                    wordbox.setOriginalLocationAndOrientation(location, (int)wordbox.rotationAngle);
                }
                else
                {
                    wordbox = WordBox.CreateWordBox(line, validOrientations[studyController.currentPosition][index], category);

                    if (category == WordBoxCategory.Joiner)
                    {
                        location.X = 500; 
                        location.Y = 500;
                    }
                    else if (categoryNumber == 1)
                    {
                        //top left
                        location.X = 50;
                        location.Y = 50;
                    }
                    else if (categoryNumber == 2)
                    {
                        //top right
                        location.X = 700;
                        location.Y = 50;
                    }
                    wordbox.Location = location;
                    wordbox.setOriginalLocationAndOrientation(location, validOrientations[studyController.currentPosition][index]);
                }

                wordbox.setWordBoxController(this);

                addWordBoxToCategory(wordbox, category);
            }

            lastCategoryLoaded = category;
        }

        private string filenameForCategory(WordBoxCategory category)
        {
            string filename = null;
            switch (category)
            {
                case WordBoxCategory.Car:
                    filename = "Car"; break;
                case WordBoxCategory.Tree:
                    filename = "Tree"; break;
                case WordBoxCategory.Book:
                    filename = "Book"; break;
                case WordBoxCategory.Cat:
                    filename = "Cat"; break;
                case WordBoxCategory.Chair:
                    filename = "Chair"; break;
                case WordBoxCategory.Clothing:
                    filename = "Clothing"; break;
                case WordBoxCategory.Coffee:
                    filename = "Coffee"; break;
                case WordBoxCategory.Dog:
                    filename = "Dog"; break;
                case WordBoxCategory.Lake:
                    filename = "Lake"; break;
                case WordBoxCategory.Student:
                    filename = "Student"; break;
                case WordBoxCategory.Planet:
                    filename = "Planet"; break;
                case WordBoxCategory.Horse:
                    filename = "Horse"; break;
                case WordBoxCategory.Joiner:
                    filename = joinerPrefix() + "Joiner"; break;
                default:
                    filename = ""; break;
            }

            //if (studyController.isInSetUpMode == false)
            //    filename = filename + "Boxes" + studyController.currentPosition;
            //else if (studyController.isInSetUpMode == true)
                filename = filename + "Words";

            return filename;
        }

        WordBoxCategory lastCategoryLoaded;
        private string joinerPrefix()
        {
            switch (lastCategoryLoaded)
            {
                case WordBoxCategory.Car:
                    return "PolhemusPens";
                case WordBoxCategory.Coffee:
                    return "Cursors";
                case WordBoxCategory.Lake:
                    return "Pentographs";
                case WordBoxCategory.Clothing:
                    return "CartoonArms";
                case WordBoxCategory.Dog:
                    return "RealVirtualArm";
                default:
                    break;
            }
            return "";
        }

        public void printOutCurrentBoxesToFiles()
        {
            foreach (WordBoxCategory category in currentWordBoxes.Keys)
            {
                List<WordBox> thisCategoriesBoxes = currentWordBoxes[category];
                string filename = filenameForCategory(category);

                //print out the boxes
                System.IO.StreamWriter file = new System.IO.StreamWriter(filenamePrefix + filename + ".txt");
                foreach (WordBox box in thisCategoriesBoxes)
                    file.WriteLine(box.textString + "," + box.rotationAngle + "," + box.Location.X + "," + box.Location.Y);

                file.Close();
            }
        }
    }
}
