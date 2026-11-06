namespace WebCrawler
{
    partial class FindForm
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
            this.labelFind = new System.Windows.Forms.Label();
            this.textBoxFind = new System.Windows.Forms.TextBox();
            this.labelStatus = new System.Windows.Forms.Label();
            this.buttonFindNext = new System.Windows.Forms.Button();
            this.buttonClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // labelFind
            // 
            this.labelFind.AutoSize = true;
            this.labelFind.Location = new System.Drawing.Point(12, 16);
            this.labelFind.Name = "labelFind";
            this.labelFind.Size = new System.Drawing.Size(58, 15);
            this.labelFind.TabIndex = 0;
            this.labelFind.Text = "Find what:";
            // 
            // textBoxFind
            // 
            this.textBoxFind.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxFind.Location = new System.Drawing.Point(80, 12);
            this.textBoxFind.Name = "textBoxFind";
            this.textBoxFind.Size = new System.Drawing.Size(228, 23);
            this.textBoxFind.TabIndex = 0;
            this.textBoxFind.TextChanged += new System.EventHandler(this.textBoxFind_TextChanged);
            // 
            // labelStatus
            // 
            this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelStatus.ForeColor = System.Drawing.SystemColors.GrayText;
            this.labelStatus.Location = new System.Drawing.Point(12, 42);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(296, 15);
            this.labelStatus.TabIndex = 3;
            // 
            // buttonFindNext
            // 
            this.buttonFindNext.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFindNext.Location = new System.Drawing.Point(138, 64);
            this.buttonFindNext.Name = "buttonFindNext";
            this.buttonFindNext.Size = new System.Drawing.Size(88, 26);
            this.buttonFindNext.TabIndex = 1;
            this.buttonFindNext.Text = "Find Next";
            this.buttonFindNext.UseVisualStyleBackColor = true;
            this.buttonFindNext.Click += new System.EventHandler(this.buttonFindNext_Click);
            // 
            // buttonClose
            // 
            this.buttonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonClose.Location = new System.Drawing.Point(232, 64);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(76, 26);
            this.buttonClose.TabIndex = 2;
            this.buttonClose.Text = "Close";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);
            // 
            // FindForm
            // 
            this.AcceptButton = this.buttonFindNext;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.buttonClose;
            this.ClientSize = new System.Drawing.Size(320, 102);
            this.Controls.Add(this.buttonClose);
            this.Controls.Add(this.buttonFindNext);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.textBoxFind);
            this.Controls.Add(this.labelFind);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Find";
            this.Load += new System.EventHandler(this.FindForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelFind;
        private System.Windows.Forms.TextBox textBoxFind;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Button buttonFindNext;
        private System.Windows.Forms.Button buttonClose;
    }
}
