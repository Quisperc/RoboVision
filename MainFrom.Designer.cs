using System.Windows.Forms;

namespace RoboVision
{
    partial class MainFrom
    {
        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        //private void InitializeComponent()
        //{
        //    this.components = new System.ComponentModel.Container();
        //    this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        //    this.ClientSize = new System.Drawing.Size(800, 450);
        //    this.Text = "Form1";
        //}

        #endregion
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // 声明控件
        private System.Windows.Forms.Button btnDetectCamera;
        private System.Windows.Forms.Button btnSetParameters;
        private System.Windows.Forms.Button btnCapture;
        private System.Windows.Forms.Button btnProcess;
        private System.Windows.Forms.PictureBox pictureBoxDisplay;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.TextBox textBoxCoordinates;
        private System.Windows.Forms.Label labelCurrentCamera;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.btnDetectCamera = new System.Windows.Forms.Button();
            this.btnSetParameters = new System.Windows.Forms.Button();
            this.btnCapture = new System.Windows.Forms.Button();
            this.btnProcess = new System.Windows.Forms.Button();
            this.pictureBoxDisplay = new System.Windows.Forms.PictureBox();
            this.labelStatus = new System.Windows.Forms.Label();
            this.textBoxCoordinates = new System.Windows.Forms.TextBox();
            this.labelCurrentCamera = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDisplay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // btnDetectCamera
            // 
            this.btnDetectCamera.Location = new System.Drawing.Point(20, 20);
            this.btnDetectCamera.Name = "btnDetectCamera";
            this.btnDetectCamera.Size = new System.Drawing.Size(120, 30);
            this.btnDetectCamera.TabIndex = 0;
            this.btnDetectCamera.Text = "检测相机";
            this.btnDetectCamera.UseVisualStyleBackColor = true;
            this.btnDetectCamera.Click += new System.EventHandler(this.btnDetectCamera_Click);
            // 
            // btnSetParameters
            // 
            this.btnSetParameters.Location = new System.Drawing.Point(20, 80);
            this.btnSetParameters.Name = "btnSetParameters";
            this.btnSetParameters.Size = new System.Drawing.Size(120, 30);
            this.btnSetParameters.TabIndex = 1;
            this.btnSetParameters.Text = "设置相机参数";
            this.btnSetParameters.UseVisualStyleBackColor = true;
            this.btnSetParameters.Click += new System.EventHandler(this.btnSetParameters_Click);
            // 
            // btnCapture
            // 
            this.btnCapture.Location = new System.Drawing.Point(20, 120);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(120, 30);
            this.btnCapture.TabIndex = 2;
            this.btnCapture.Text = "拍摄图片";
            this.btnCapture.UseVisualStyleBackColor = true;
            this.btnCapture.Click += new System.EventHandler(this.btnCapture_Click);
            // 
            // btnProcess
            // 
            this.btnProcess.Location = new System.Drawing.Point(20, 160);
            this.btnProcess.Name = "btnProcess";
            this.btnProcess.Size = new System.Drawing.Size(120, 30);
            this.btnProcess.TabIndex = 3;
            this.btnProcess.Text = "处理图像";
            this.btnProcess.UseVisualStyleBackColor = true;
            this.btnProcess.Click += new System.EventHandler(this.btnProcess_Click);
            // 
            // pictureBoxDisplay
            // 
            this.pictureBoxDisplay.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxDisplay.Location = new System.Drawing.Point(160, 20);
            this.pictureBoxDisplay.Name = "pictureBoxDisplay";
            this.pictureBoxDisplay.Size = new System.Drawing.Size(640, 480);
            this.pictureBoxDisplay.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxDisplay.TabIndex = 4;
            this.pictureBoxDisplay.TabStop = false;
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(20, 201);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(97, 15);
            this.labelStatus.TabIndex = 5;
            this.labelStatus.Text = "状态：待操作";
            // 
            // textBoxCoordinates
            // 
            this.textBoxCoordinates.Location = new System.Drawing.Point(20, 232);
            this.textBoxCoordinates.Multiline = true;
            this.textBoxCoordinates.Name = "textBoxCoordinates";
            this.textBoxCoordinates.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxCoordinates.Size = new System.Drawing.Size(120, 100);
            this.textBoxCoordinates.TabIndex = 6;
            // 
            // labelCurrentCamera
            // 
            this.labelCurrentCamera.AutoSize = true;
            this.labelCurrentCamera.Location = new System.Drawing.Point(20, 56);
            this.labelCurrentCamera.Name = "labelCurrentCamera";
            this.labelCurrentCamera.Size = new System.Drawing.Size(127, 15);
            this.labelCurrentCamera.TabIndex = 7;
            this.labelCurrentCamera.Text = "当前相机：未选择";
            // 
            // pictureBox1
            // 
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Location = new System.Drawing.Point(806, 20);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(640, 480);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 8;
            this.pictureBox1.TabStop = false;
            // 
            // MainFrom
            // 
            this.ClientSize = new System.Drawing.Size(1466, 520);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.textBoxCoordinates);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.pictureBoxDisplay);
            this.Controls.Add(this.btnProcess);
            this.Controls.Add(this.btnCapture);
            this.Controls.Add(this.btnSetParameters);
            this.Controls.Add(this.btnDetectCamera);
            this.Controls.Add(this.labelCurrentCamera);
            this.Name = "MainFrom";
            this.Text = "机器人拆垛码垛系统";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDisplay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private PictureBox pictureBox1;
    }
}

