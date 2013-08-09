namespace WebCrawler
{
	partial class InfoForm
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
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.txtLinkFrom = new System.Windows.Forms.TextBox();
			this.txtLinkTo = new System.Windows.Forms.TextBox();
			this.btnClose = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.txtLink = new System.Windows.Forms.TextBox();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 32);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(65, 13);
			this.label1.TabIndex = 0;
			this.label1.Text = "Linked From";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(12, 231);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(48, 13);
			this.label2.TabIndex = 1;
			this.label2.Text = "Links To";
			// 
			// txtLinkFrom
			// 
			this.txtLinkFrom.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
									| System.Windows.Forms.AnchorStyles.Right)));
			this.txtLinkFrom.Location = new System.Drawing.Point(83, 35);
			this.txtLinkFrom.Multiline = true;
			this.txtLinkFrom.Name = "txtLinkFrom";
			this.txtLinkFrom.ReadOnly = true;
			this.txtLinkFrom.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.txtLinkFrom.Size = new System.Drawing.Size(919, 190);
			this.txtLinkFrom.TabIndex = 2;
			this.txtLinkFrom.WordWrap = false;
			// 
			// txtLinkTo
			// 
			this.txtLinkTo.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
									| System.Windows.Forms.AnchorStyles.Left)
									| System.Windows.Forms.AnchorStyles.Right)));
			this.txtLinkTo.Location = new System.Drawing.Point(83, 231);
			this.txtLinkTo.Multiline = true;
			this.txtLinkTo.Name = "txtLinkTo";
			this.txtLinkTo.ReadOnly = true;
			this.txtLinkTo.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.txtLinkTo.Size = new System.Drawing.Size(919, 194);
			this.txtLinkTo.TabIndex = 3;
			this.txtLinkTo.WordWrap = false;
			// 
			// btnClose
			// 
			this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnClose.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btnClose.Location = new System.Drawing.Point(899, 431);
			this.btnClose.Name = "btnClose";
			this.btnClose.Size = new System.Drawing.Size(103, 29);
			this.btnClose.TabIndex = 4;
			this.btnClose.Text = "Close";
			this.btnClose.UseVisualStyleBackColor = true;
			this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(12, 9);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(27, 13);
			this.label3.TabIndex = 5;
			this.label3.Text = "Link";
			// 
			// txtLink
			// 
			this.txtLink.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
									| System.Windows.Forms.AnchorStyles.Right)));
			this.txtLink.Location = new System.Drawing.Point(83, 9);
			this.txtLink.Name = "txtLink";
			this.txtLink.ReadOnly = true;
			this.txtLink.Size = new System.Drawing.Size(919, 20);
			this.txtLink.TabIndex = 6;
			this.txtLink.WordWrap = false;
			// 
			// InfoForm
			// 
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(1014, 472);
			this.Controls.Add(this.txtLink);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.btnClose);
			this.Controls.Add(this.txtLinkTo);
			this.Controls.Add(this.txtLinkFrom);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "InfoForm";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Link Information";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox txtLinkFrom;
		private System.Windows.Forms.TextBox txtLinkTo;
		private System.Windows.Forms.Button btnClose;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox txtLink;
	}
}