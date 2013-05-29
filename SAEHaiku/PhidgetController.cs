using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Phidgets;
using Phidgets.Events;
using System.Windows.Forms;

namespace SAEHaiku
{
    public class PhidgetController
    {
        PolhemusController polhemusController;

        static InterfaceKit polhemusKit;
        static InterfaceKit vibrationKit;

        static Analog analogKit1;
        static InterfaceKit penKit;
        
        //Servo servo;
        //Timer updateTimer;

        
        public PhidgetController(PolhemusController inPolhemusController)
        {
            polhemusController = inPolhemusController;

            if (Program.kinectEnabled)
            {
                penKit = new InterfaceKit();
                setUpInterfaceKit(penKit);
            }

            Analog analog1 = new Analog();
            setUpAnalog(analog1);
            
            /*
            for (int i = 0; i < analog1.outputs.Count; i++)
            {
                analog1.outputs[i].Voltage = analog1.outputs[i].VoltageMax;
                analog1.outputs[i].Enabled = true;
            }*/
            
        }

        void analog1_Attach(object sender, AttachEventArgs e)
        {
            analogKit1 = (Analog)sender;
            Console.WriteLine("AnalogOutput {0} attached!", e.Device.SerialNumber.ToString());
            analogKit1.outputs[0].Enabled = true;
            analogKit1.outputs[1].Enabled = true;
            analogKit1.outputs[2].Enabled = true;
            analogKit1.outputs[3].Enabled = true;

            analogKit1.outputs[0].Voltage = 0;
            analogKit1.outputs[1].Voltage = 0;
            analogKit1.outputs[2].Voltage = 0;
            analogKit1.outputs[3].Voltage = 0;
        }


        void setUpAnalog(Analog analog)
        {
            analog.Attach += new AttachEventHandler(analog1_Attach);
            analog.Error += new ErrorEventHandler(analog_Error);

            analog.open();
            Console.WriteLine("Waiting for analog output to be attached...");
            analog.waitForAttachment();
        }

        void analog_Error(object sender, ErrorEventArgs e)
        {
            Console.WriteLine("ERROR WITH ANALOG: " + e.Description);
        }

        void setUpInterfaceKit(InterfaceKit ifKit)
        {
            //Hook the basica event handlers
            ifKit.Attach += new AttachEventHandler(ifKit_Attach);
            ifKit.Detach += new DetachEventHandler(ifKit_Detach);
            ifKit.Error += new ErrorEventHandler(ifKit_Error);

            //Hook the phidget spcific event handlers
            ifKit.InputChange += new InputChangeEventHandler(ifKit_InputChange);
            ifKit.OutputChange += new OutputChangeEventHandler(ifKit_OutputChange);
            ifKit.SensorChange += new SensorChangeEventHandler(ifKit_SensorChange);

            //Open the object for device connections
            ifKit.open();

            //Wait for an InterfaceKit phidget to be attached
            Console.WriteLine("Waiting for InterfaceKit to be attached...");
            ifKit.waitForAttachment();
        }

        static public void turnOnVibration()
        {
            if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate
                || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.MouseVibration)
            {
                analogKit1.outputs[1].Voltage = 5;
            }

            if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesBeltVibrate
                || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.PocketVibration
                || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.KinectPictureArmsPocketVibrate)
            {
                analogKit1.outputs[0].Voltage = 5;
            }

            /*
            if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate)
            {
                vibrationKit.outputs[0] = true;
                vibrationKit.outputs[1] = true;
            }
            else if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesBeltVibrate)
            {
                vibrationKit.outputs[2] = true;
                vibrationKit.outputs[3] = true;
            }
            */
        }

        static public void turnOffVibration()
        {
            if (analogKit1 != null)
            {
                analogKit1.outputs[0].Voltage = 0;
                analogKit1.outputs[1].Voltage = 0;
                analogKit1.outputs[2].Voltage = 0;
                analogKit1.outputs[3].Voltage = 0;
            }
        
            /*
            vibrationKit.outputs[0] = false;
            vibrationKit.outputs[1] = false;
            vibrationKit.outputs[2] = false;
            vibrationKit.outputs[3] = false;
            */
        }

        //Attach event handler...Display the serial number of the attached InterfaceKit 
        //to the console
        static void ifKit_Attach(object sender, AttachEventArgs e)
        {
            Console.WriteLine("InterfaceKit {0} attached!",
                               e.Device.SerialNumber.ToString());
            /*
            if (e.Device.SerialNumber.ToString() == "13508")
                polhemusKit = (InterfaceKit)sender;
            else
            {
                vibrationKit = (InterfaceKit)sender;
                turnOffVibration();
            }*/


        }

        //Detach event handler...Display the serial number of the detached InterfaceKit 
        //to the console
        static void ifKit_Detach(object sender, DetachEventArgs e)
        {
          //  Console.WriteLine("InterfaceKit {0} detached!",
            //                    e.Device.SerialNumber.ToString());
        }

        //Error event handler...Display the error description to the console
        static void ifKit_Error(object sender, ErrorEventArgs e)
        {
            Console.WriteLine(e.Description);
        }

        //Input Change event handler...Display the input index and the new value to the 
        //console
        void ifKit_InputChange(object sender, InputChangeEventArgs e)
        {
            if (Program.mainForm == null)
                return;

            bool isPressed = !((InterfaceKit)sender).inputs[e.Index];

            if (isPressed)
                Program.mainForm.handleMouseDown(false);
            else
                Program.mainForm.handleMouseUp(false);

            /*
            if(((InterfaceKit)sender).SerialNumber.ToString() == "13508")
                //then Polhemus
            {
                switch (e.Index + 1)
                {
                    case 1:
                        if (polhemusController.isUser1Touching == true)
                        {
                            polhemusController.isUser1Touching = false;
                            //tell table player 1 clicked
                            polhemusController.stationDidClick(1);
                        }
                        else
                        {
                            polhemusController.isUser1Touching = true;
                        }
                        break;
                    case 2:
                        if (polhemusController.isUser2Touching == true)
                        {
                            polhemusController.isUser2Touching = false;
                            //tell table player 2 clicked
                            polhemusController.stationDidClick(2);
                        }
                        else
                        {
                            polhemusController.isUser2Touching = true;
                        }
                        break;
                }
            }*/
        }

        //Output change event handler...Display the output index and the new valu to 
        //the console
        static void ifKit_OutputChange(object sender, OutputChangeEventArgs e)
        {
            //Console.WriteLine("Output index {0} value {0}", e.Index, e.Value.ToString());
        }

        //Sensor Change event handler...Display the sensor index and it's new value to 
        //the console
        static void ifKit_SensorChange(object sender, SensorChangeEventArgs e)
        {
            //if (e.Index == 0)
            //    Console.WriteLine("Sensor index {0} value {1}", e.Index, e.Value);
            //Console.Out.Flush();
        }

        /*
        public void stop()
        {
            if(servo != null)
                servo.close();
        }
         
        public void setUpServos()
        {
            if (servo == null)
            {
                servo = new Servo();

                servo.open();
                servo.waitForAttachment();

                servo.servos[0].Position = servo.servos[0].PositionMin;
                servo.servos[1].Position = servo.servos[1].PositionMin;

                updateTimer = new Timer();
                updateTimer.Interval = 75;

                updateTimer.Tick += new EventHandler(updateTimer_Tick);
            }
        }

        void updateTimer_Tick(object sender, EventArgs e)
        {
            move();
        }

        bool direction = true;
        private void move()
        {
            if (direction)
            {
                if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrateOne
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrate
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate)    
                    servo.servos[0].Position = servo.servos[0].PositionMax;

                if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrateTwo
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrate
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate)    
                    servo.servos[1].Position = servo.servos[1].PositionMax;
            }
            else
            {
                if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrateOne
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrate
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate)    
                    servo.servos[0].Position = servo.servos[0].PositionMin;

                if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrateTwo
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrate
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate)    
                    servo.servos[1].Position = servo.servos[1].PositionMin;
            }
                
            direction = !direction;
        }

        public void actuate()
        {
            if (updateTimer.Enabled == false)
            {
                if(Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrateOne
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrate
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate)    
                    servo.servos[0].Engaged = true;
                
                if (Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrateTwo
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesVibrate
                    || Program.mainForm.studyController.currentCondition == HaikuStudyCondition.LinesMouseVibrate)
                    servo.servos[1].Engaged = true;

                updateTimer.Start();
                move();
            }
        }

        public void stopActuate()
        {
            if (updateTimer.Enabled == true)
            {
                servo.servos[0].Engaged = false;
                servo.servos[1].Engaged = false;
                updateTimer.Stop();
            }
        }*/
    }
}
