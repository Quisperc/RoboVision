namespace RailwayRoadSectionDetection
{
    partial class ParameterInputForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label labelResolution;
        private System.Windows.Forms.ComboBox comboBoxResolutions;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelResolution = new System.Windows.Forms.Label();
            this.comboBoxResolutions = new System.Windows.Forms.ComboBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelResolution
            // 
            this.labelResolution.AutoSize = true;
            this.labelResolution.Location = new System.Drawing.Point(20, 20);
            this.labelResolution.Name = "labelResolution";
            this.labelResolution.Size = new System.Drawing.Size(89, 17);
            this.labelResolution.TabIndex = 0;
            this.labelResolution.Text = "选择分辨率：";
            // 
            // comboBoxResolutions
            // 
            this.comboBoxResolutions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxResolutions.FormattingEnabled = true;
            this.comboBoxResolutions.Location = new System.Drawing.Point(120, 20);
            this.comboBoxResolutions.Name = "comboBoxResolutions";
            this.comboBoxResolutions.Size = new System.Drawing.Size(140, 25);
            this.comboBoxResolutions.TabIndex = 1;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(20, 60);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 30);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "确定";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(160, 60);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 30);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // ParameterInputForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 111);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.comboBoxResolutions);
            this.Controls.Add(this.labelResolution);
            this.Name = "ParameterInputForm";
            this.Text = "设置相机参数";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
