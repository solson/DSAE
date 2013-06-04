using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace SAEHaiku
{

    public enum WordBoxCategory { Tree, Car, Cat, Dog, Lake, Book, Chair, Clothing, Coffee, Student, 
        Joiner, None, Planet, Horse };
    public partial class WordBox : UserControl
    {

        static private Font textFont = new Font("Helvetica", 14f * (Program.usableHeight / 768f));
        static public WordBox CreateWordBox(string withWord, float withRotationAngle, WordBoxCategory withCategory)
        {
            WordBox newWordBox = new WordBox();
            newWordBox.textString = withWord;
            newWordBox.rotationAngle = withRotationAngle;
            newWordBox.resizeToFitCurrentWord();

            newWordBox.category = withCategory;
            return newWordBox;
        }


        WordBoxController boxController;
        public void setWordBoxController(WordBoxController newBoxController)
        {
            boxController = newBoxController;
        }

        public int ownedByUser = 0;

        public Point originalLocation;
        private int originalOrientation;
        public void setOriginalLocationAndOrientation(Point newOriginalLocation, int newOriginalOrientation)
        {
            originalLocation = newOriginalLocation;
            originalOrientation = newOriginalOrientation;
            this.Location = newOriginalLocation;
            this.rotationAngle = newOriginalOrientation;
        }
        
        
        Point draggingOffset;
        public void moveToLocation(Point windowLocation)
        {
            windowLocation.X -= draggingOffset.X;
            windowLocation.Y -= draggingOffset.Y;
            this.Location = windowLocation;
        }

        public WordBoxCategory category { get; set; }
        public string textString;
        public float rotationAngle;
        public Size wordBoxSize;
        public void setWord(string newWord)
        {
            textString = newWord;
        }

        private void resizeToFitCurrentWord()
        {
            Graphics g = this.CreateGraphics();
            wordBoxSize = g.MeasureString(textString, textFont).ToSize();

            Size containerSize = Size.Empty;
            //0 or 180, width and height of the string
            //90 or 270, height and width, swapped, of the string
            //factors of 45, max (width+height for both)

            if (rotationAngle == 0 || rotationAngle == 180)
            {
                containerSize.Width = wordBoxSize.Width;
                containerSize.Height = wordBoxSize.Height;
            }
            else if (rotationAngle == 90 || rotationAngle == 270)
            {
                containerSize.Width = wordBoxSize.Height;
                containerSize.Height = wordBoxSize.Width;
            }
            else if (rotationAngle == 45 || rotationAngle == 135 || rotationAngle == 225)
            {
                containerSize.Height = wordBoxSize.Width + wordBoxSize.Height / 3;
                containerSize.Width = wordBoxSize.Width + wordBoxSize.Height / 3;
            }
            else if (rotationAngle == 30 || rotationAngle == 210 )
            {
                containerSize.Height = wordBoxSize.Width + wordBoxSize.Height/4;
                containerSize.Width = wordBoxSize.Width + wordBoxSize.Height / 2;
            }
            else if (rotationAngle == 120 || rotationAngle == 300)
            {
                containerSize.Height = wordBoxSize.Width + wordBoxSize.Height / 2;
                containerSize.Width = wordBoxSize.Width + wordBoxSize.Height;
            }
            else if (rotationAngle == 15 || rotationAngle == 195)
            {
                containerSize.Height = wordBoxSize.Height + wordBoxSize.Width/3;
                containerSize.Width = wordBoxSize.Width + wordBoxSize.Height/3;
            }
            
            this.Size = containerSize;
        }

        private WordBox()
        {
            InitializeComponent();

            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            this.BackColor = Color.Transparent;
        }

        public void dropped()  
        {
            
            //when you let go, snap the word to the correct orientation
            if (boxController.shouldBoxSnap(this))
            {
                ownedByUser = boxController.thisBoxIsOwnedByWhichUser(this);

                Position pos = boxController.boxShouldSnapToWhichPosition(this);
                switch (pos)
                {
                    case Position.One:
                        {
                            rotationAngle = 0;
                            break;
                        }
                    case Position.Two:
                        {
                            rotationAngle = 90;
                            break;
                        }
                    case Position.Three:
                        {
                            rotationAngle = 180;
                            break;
                        }
                    case Position.Four:
                        {
                            rotationAngle = 270;
                            break;
                        }
                }
                //GetDrop();
                
            }

            else if (boxController.boxesShouldSnapBack == true)
            {
                ownedByUser = 0;
                
                //snap back to original location
                //this.Location = originalLocation;
                this.rotationAngle = originalOrientation;
                //this.GetDrop();

                this.GetSetOriginalLocation();
            }
            else
            {
                ownedByUser = 0;
            }

            //resizeToFitCurrentWord();
            resetColor();
            this.Invalidate();
        }

        private delegate void GetSetOriginalLocationDelegate();
        private void GetSetOriginalLocation()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new GetSetOriginalLocationDelegate(GetSetOriginalLocation));
            }
            else
            {
                this.Location = originalLocation;
            }
        }

        Color wordBackColor = Color.White;
        public void setBackgroundColorForCursorNumber(int cursorNumber)
        {
            if (cursorNumber == 1)
            {
                wordBackColor = Color.MediumPurple;
            }
            else if (cursorNumber == 2)
            {
                wordBackColor = Color.LimeGreen;
            }
        }

        public void resetColor()
        {
            wordBackColor = Color.White;
        }

        public void beginDragging(Point withCursorPoint, int cursorNumber)
        {
            withCursorPoint.X -= this.Location.X;
            withCursorPoint.Y -= this.Location.Y;

            draggingOffset = withCursorPoint;

            setBackgroundColorForCursorNumber(cursorNumber);
        }

        public Rectangle displayRectangle()
        {
            Rectangle rect = new Rectangle(this.Location, this.Size);
            return rect;
        }

        public void paintToGraphics(Graphics g)
        {
            g.TranslateTransform(this.Location.X, this.Location.Y);
            g.TranslateTransform(this.Size.Width / 2, this.Size.Height / 2);
            g.RotateTransform(rotationAngle);
            
            Point originOfWordBox = Point.Empty;
            originOfWordBox.X -= wordBoxSize.Width / 2;
            originOfWordBox.Y -= wordBoxSize.Height / 2;

            g.FillRectangle(new SolidBrush(wordBackColor), originOfWordBox.X-1, originOfWordBox.Y-1, wordBoxSize.Width+2, wordBoxSize.Height+2);
            g.DrawString(textString, textFont, new SolidBrush(Color.Black), originOfWordBox);
            g.ResetTransform();
        }
    }
}
