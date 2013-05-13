namespace SAEHaiku
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.sdgManager1 = new Sdgt.SdgManager(this.components);
            this.SuspendLayout();
            // 
            // sdgManager1
            // 
            this.sdgManager1.EmulateSystemMouseMode = Sdgt.EmulateSystemMouseModes.FollowMouse;
            this.sdgManager1.Keyboards = null;
            this.sdgManager1.Mice = null;
            this.sdgManager1.MouseToFollow = 0;
            this.sdgManager1.ParkSystemMouseLocation = new System.Drawing.Point(350, 350);
            this.sdgManager1.RelativeTo = this;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1008, 730);
            this.KeyPreview = true;
            this.Name = "Form1";
            this.Text = "HaikuBuilder";
            this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.Form1_KeyPress);
            this.ResumeLayout(false);

        }

        #endregion

        private Sdgt.SdgManager sdgManager1;
    }
}

