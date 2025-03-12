using System.Windows.Forms;

namespace RoboVision
{
    partial class MainForm
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
        private System.Windows.Forms.RichTextBox textBoxCoordinates;
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
            this.textBoxCoordinates = new System.Windows.Forms.RichTextBox();
            this.labelCurrentCamera = new System.Windows.Forms.Label();
            this.pictureBoxProcessed = new System.Windows.Forms.PictureBox();
            this.btnLoadModel = new System.Windows.Forms.Button();
            this.textTargetIP = new System.Windows.Forms.TextBox();
            this.textTargetPort = new System.Windows.Forms.TextBox();
            this.DesLabel = new System.Windows.Forms.Label();
            this.ListenPortLabel = new System.Windows.Forms.Label();
            this.sourcePortLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDisplay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxProcessed)).BeginInit();
            this.SuspendLayout();
            // 
            // btnDetectCamera
            // 
            this.btnDetectCamera.Location = new System.Drawing.Point(19, 159);
            this.btnDetectCamera.Name = "btnDetectCamera";
            this.btnDetectCamera.Size = new System.Drawing.Size(120, 47);
            this.btnDetectCamera.TabIndex = 0;
            this.btnDetectCamera.Text = "检测相机";
            this.btnDetectCamera.UseVisualStyleBackColor = true;
            this.btnDetectCamera.Click += new System.EventHandler(this.btnDetectCamera_Click);
            // 
            // btnSetParameters
            // 
            this.btnSetParameters.Location = new System.Drawing.Point(19, 212);
            this.btnSetParameters.Name = "btnSetParameters";
            this.btnSetParameters.Size = new System.Drawing.Size(120, 46);
            this.btnSetParameters.TabIndex = 1;
            this.btnSetParameters.Text = "设置相机参数";
            this.btnSetParameters.UseVisualStyleBackColor = true;
            this.btnSetParameters.Click += new System.EventHandler(this.btnSetParameters_Click);
            // 
            // btnCapture
            // 
            this.btnCapture.Location = new System.Drawing.Point(19, 264);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(120, 45);
            this.btnCapture.TabIndex = 2;
            this.btnCapture.Text = "拍摄图片";
            this.btnCapture.UseVisualStyleBackColor = true;
            this.btnCapture.Click += new System.EventHandler(this.btnCapture_Click);
            // 
            // btnProcess
            // 
            this.btnProcess.Location = new System.Drawing.Point(19, 366);
            this.btnProcess.Name = "btnProcess";
            this.btnProcess.Size = new System.Drawing.Size(120, 45);
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
            this.labelStatus.Location = new System.Drawing.Point(11, 463);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(97, 15);
            this.labelStatus.TabIndex = 5;
            this.labelStatus.Text = "状态：待操作";
            // 
            // textBoxCoordinates
            // 
            this.textBoxCoordinates.Location = new System.Drawing.Point(27, 520);
            this.textBoxCoordinates.Name = "textBoxCoordinates";
            this.textBoxCoordinates.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.textBoxCoordinates.Size = new System.Drawing.Size(1419, 220);
            this.textBoxCoordinates.TabIndex = 6;
            this.textBoxCoordinates.Text = "";
            // 
            // labelCurrentCamera
            // 
            this.labelCurrentCamera.AutoSize = true;
            this.labelCurrentCamera.Location = new System.Drawing.Point(12, 432);
            this.labelCurrentCamera.Name = "labelCurrentCamera";
            this.labelCurrentCamera.Size = new System.Drawing.Size(127, 15);
            this.labelCurrentCamera.TabIndex = 7;
            this.labelCurrentCamera.Text = "当前相机：未选择";
            // 
            // pictureBoxProcessed
            // 
            this.pictureBoxProcessed.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxProcessed.Location = new System.Drawing.Point(806, 20);
            this.pictureBoxProcessed.Name = "pictureBoxProcessed";
            this.pictureBoxProcessed.Size = new System.Drawing.Size(640, 480);
            this.pictureBoxProcessed.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxProcessed.TabIndex = 8;
            this.pictureBoxProcessed.TabStop = false;
            // 
            // btnLoadModel
            // 
            this.btnLoadModel.Location = new System.Drawing.Point(19, 315);
            this.btnLoadModel.Name = "btnLoadModel";
            this.btnLoadModel.Size = new System.Drawing.Size(120, 45);
            this.btnLoadModel.TabIndex = 9;
            this.btnLoadModel.Text = "导入模型";
            this.btnLoadModel.UseVisualStyleBackColor = true;
            this.btnLoadModel.Click += new System.EventHandler(this.btnLoadModel__Click);
            // 
            // textTargetIP
            // 
            this.textTargetIP.Location = new System.Drawing.Point(20, 89);
            this.textTargetIP.Name = "textTargetIP";
            this.textTargetIP.Size = new System.Drawing.Size(119, 25);
            this.textTargetIP.TabIndex = 10;
            this.textTargetIP.Text = "127.0.0.1";
            // 
            // textTargetPort
            // 
            this.textTargetPort.Location = new System.Drawing.Point(20, 120);
            this.textTargetPort.Name = "textTargetPort";
            this.textTargetPort.Size = new System.Drawing.Size(119, 25);
            this.textTargetPort.TabIndex = 11;
            this.textTargetPort.Text = "8001";
            // 
            // DesLabel
            // 
            this.DesLabel.AutoSize = true;
            this.DesLabel.Location = new System.Drawing.Point(9, 68);
            this.DesLabel.Name = "DesLabel";
            this.DesLabel.Size = new System.Drawing.Size(151, 15);
            this.DesLabel.TabIndex = 12;
            this.DesLabel.Text = "目标服务器IP与端口:";
            // 
            // ListenPortLabel
            // 
            this.ListenPortLabel.AutoSize = true;
            this.ListenPortLabel.Location = new System.Drawing.Point(9, 43);
            this.ListenPortLabel.Name = "ListenPortLabel";
            this.ListenPortLabel.Size = new System.Drawing.Size(144, 15);
            this.ListenPortLabel.TabIndex = 13;
            this.ListenPortLabel.Text = "程序监听端口：8001";
            // 
            // sourcePortLabel
            // 
            this.sourcePortLabel.AutoSize = true;
            this.sourcePortLabel.Location = new System.Drawing.Point(9, 20);
            this.sourcePortLabel.Name = "sourcePortLabel";
            this.sourcePortLabel.Size = new System.Drawing.Size(144, 15);
            this.sourcePortLabel.TabIndex = 14;
            this.sourcePortLabel.Text = "程序发送端口：8000";
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(1466, 752);
            this.Controls.Add(this.sourcePortLabel);
            this.Controls.Add(this.ListenPortLabel);
            this.Controls.Add(this.DesLabel);
            this.Controls.Add(this.textTargetPort);
            this.Controls.Add(this.textTargetIP);
            this.Controls.Add(this.btnLoadModel);
            this.Controls.Add(this.pictureBoxProcessed);
            this.Controls.Add(this.textBoxCoordinates);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.pictureBoxDisplay);
            this.Controls.Add(this.btnProcess);
            this.Controls.Add(this.btnCapture);
            this.Controls.Add(this.btnSetParameters);
            this.Controls.Add(this.btnDetectCamera);
            this.Controls.Add(this.labelCurrentCamera);
            this.Name = "MainForm";
            this.Text = "机器人拆垛码垛系统";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDisplay)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxProcessed)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private PictureBox pictureBoxProcessed;
        private Button btnLoadModel;
        private TextBox textTargetIP;
        private TextBox textTargetPort;
        private Label DesLabel;
        private Label ListenPortLabel;
        private Label sourcePortLabel;
    }
}

