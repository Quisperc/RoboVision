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
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxDisplay)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxProcessed)).BeginInit();
            this.SuspendLayout();
            // 
            // btnDetectCamera
            // 
            this.btnDetectCamera.Location = new System.Drawing.Point(20, 21);
            this.btnDetectCamera.Name = "btnDetectCamera";
            this.btnDetectCamera.Size = new System.Drawing.Size(120, 47);
            this.btnDetectCamera.TabIndex = 0;
            this.btnDetectCamera.Text = "检测相机";
            this.btnDetectCamera.UseVisualStyleBackColor = true;
            this.btnDetectCamera.Click += new System.EventHandler(this.btnDetectCamera_Click);
            // 
            // btnSetParameters
            // 
            this.btnSetParameters.Location = new System.Drawing.Point(20, 87);
            this.btnSetParameters.Name = "btnSetParameters";
            this.btnSetParameters.Size = new System.Drawing.Size(120, 46);
            this.btnSetParameters.TabIndex = 1;
            this.btnSetParameters.Text = "设置相机参数";
            this.btnSetParameters.UseVisualStyleBackColor = true;
            this.btnSetParameters.Click += new System.EventHandler(this.btnSetParameters_Click);
            // 
            // btnCapture
            // 
            this.btnCapture.Location = new System.Drawing.Point(20, 152);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(120, 45);
            this.btnCapture.TabIndex = 2;
            this.btnCapture.Text = "拍摄图片";
            this.btnCapture.UseVisualStyleBackColor = true;
            this.btnCapture.Click += new System.EventHandler(this.btnCapture_Click);
            // 
            // btnProcess
            // 
            this.btnProcess.Location = new System.Drawing.Point(20, 291);
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
            this.labelStatus.Location = new System.Drawing.Point(11, 406);
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
            this.labelCurrentCamera.Location = new System.Drawing.Point(12, 369);
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
            this.btnLoadModel.Location = new System.Drawing.Point(20, 221);
            this.btnLoadModel.Name = "btnLoadModel";
            this.btnLoadModel.Size = new System.Drawing.Size(120, 45);
            this.btnLoadModel.TabIndex = 9;
            this.btnLoadModel.Text = "导入模型";
            this.btnLoadModel.UseVisualStyleBackColor = true;
            this.btnLoadModel.Click += new System.EventHandler(this.btnLoadModel__Click);
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(1466, 752);
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
    }
}

