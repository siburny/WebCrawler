﻿namespace WebCrawler
{
	partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            this.mainToolStrip = new System.Windows.Forms.ToolStrip();
            this.toolStripButtonPlay = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonPause = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonStop = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripButtonShowNextError = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonSettings = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonHighlight = new System.Windows.Forms.ToolStripButton();
            this.urlDataGridView = new System.Windows.Forms.DataGridView();
            this.refreshTimer = new System.Windows.Forms.Timer(this.components);
            this.panel1 = new System.Windows.Forms.Panel();
            this.checkButton = new System.Windows.Forms.Button();
            this.urlLabel = new System.Windows.Forms.Label();
            this.urlTextBox = new System.Windows.Forms.TextBox();
            this.statusImageList = new System.Windows.Forms.ImageList(this.components);
            this.statusStripMain = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelTotalURLs = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripButtonLoadURLList = new System.Windows.Forms.ToolStripButton();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.statusDataGridViewImageColumn = new System.Windows.Forms.DataGridViewImageColumn();
            this.idDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.statusDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.depthDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.urlDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.mimeTypeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.contentLengthDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.TimeTaken = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.notesDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.HighlightColor = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.mainToolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.urlDataGridView)).BeginInit();
            this.panel1.SuspendLayout();
            this.statusStripMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainToolStrip
            // 
            this.mainToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButtonLoadURLList,
            this.toolStripSeparator2,
            this.toolStripButtonPlay,
            this.toolStripButtonPause,
            this.toolStripButtonStop,
            this.toolStripSeparator1,
            this.toolStripButtonShowNextError,
            this.toolStripButtonSettings,
            this.toolStripButtonHighlight});
            this.mainToolStrip.Location = new System.Drawing.Point(0, 0);
            this.mainToolStrip.Name = "mainToolStrip";
            this.mainToolStrip.Size = new System.Drawing.Size(1167, 25);
            this.mainToolStrip.TabIndex = 0;
            this.mainToolStrip.Text = "toolStrip1";
            // 
            // toolStripButtonPlay
            // 
            this.toolStripButtonPlay.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonPlay.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonPlay.Image")));
            this.toolStripButtonPlay.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonPlay.Name = "toolStripButtonPlay";
            this.toolStripButtonPlay.Size = new System.Drawing.Size(23, 22);
            this.toolStripButtonPlay.Text = "Play";
            this.toolStripButtonPlay.Click += new System.EventHandler(this.toolStripButtonPlay_Click);
            // 
            // toolStripButtonPause
            // 
            this.toolStripButtonPause.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonPause.Enabled = false;
            this.toolStripButtonPause.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonPause.Image")));
            this.toolStripButtonPause.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonPause.Name = "toolStripButtonPause";
            this.toolStripButtonPause.Size = new System.Drawing.Size(23, 22);
            this.toolStripButtonPause.Text = "Pause";
            this.toolStripButtonPause.Click += new System.EventHandler(this.toolStripButtonPause_Click);
            // 
            // toolStripButtonStop
            // 
            this.toolStripButtonStop.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonStop.Enabled = false;
            this.toolStripButtonStop.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonStop.Image")));
            this.toolStripButtonStop.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonStop.Name = "toolStripButtonStop";
            this.toolStripButtonStop.Size = new System.Drawing.Size(23, 22);
            this.toolStripButtonStop.Text = "Stop";
            this.toolStripButtonStop.Click += new System.EventHandler(this.toolStripButtonStop_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // toolStripButtonShowNextError
            // 
            this.toolStripButtonShowNextError.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonShowNextError.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonShowNextError.Name = "toolStripButtonShowNextError";
            this.toolStripButtonShowNextError.Size = new System.Drawing.Size(96, 22);
            this.toolStripButtonShowNextError.Text = "Show Next Error";
            this.toolStripButtonShowNextError.Click += new System.EventHandler(this.toolStripButtonShowNextError_Click);
            // 
            // toolStripButtonSettings
            // 
            this.toolStripButtonSettings.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolStripButtonSettings.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.toolStripButtonSettings.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonSettings.Image")));
            this.toolStripButtonSettings.ImageTransparentColor = System.Drawing.Color.Transparent;
            this.toolStripButtonSettings.Name = "toolStripButtonSettings";
            this.toolStripButtonSettings.Size = new System.Drawing.Size(23, 22);
            this.toolStripButtonSettings.Text = "toolStripButton1";
            this.toolStripButtonSettings.Click += new System.EventHandler(this.toolStripButtonSettings_Click);
            // 
            // toolStripButtonHighlight
            // 
            this.toolStripButtonHighlight.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonHighlight.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonHighlight.Image")));
            this.toolStripButtonHighlight.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonHighlight.Name = "toolStripButtonHighlight";
            this.toolStripButtonHighlight.Size = new System.Drawing.Size(121, 22);
            this.toolStripButtonHighlight.Text = "Show Next Highlight";
            this.toolStripButtonHighlight.Click += new System.EventHandler(this.toolStripButtonHighlight_Click);
            // 
            // urlDataGridView
            // 
            this.urlDataGridView.AllowUserToAddRows = false;
            this.urlDataGridView.AllowUserToDeleteRows = false;
            this.urlDataGridView.AllowUserToResizeRows = false;
            this.urlDataGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.urlDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.urlDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.urlDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.statusDataGridViewImageColumn,
            this.idDataGridViewTextBoxColumn,
            this.statusDataGridViewTextBoxColumn,
            this.depthDataGridViewTextBoxColumn,
            this.urlDataGridViewTextBoxColumn,
            this.mimeTypeDataGridViewTextBoxColumn,
            this.contentLengthDataGridViewTextBoxColumn,
            this.TimeTaken,
            this.notesDataGridViewTextBoxColumn,
            this.HighlightColor});
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.urlDataGridView.DefaultCellStyle = dataGridViewCellStyle3;
            this.urlDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.urlDataGridView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.urlDataGridView.Location = new System.Drawing.Point(0, 51);
            this.urlDataGridView.MultiSelect = false;
            this.urlDataGridView.Name = "urlDataGridView";
            this.urlDataGridView.ReadOnly = true;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.urlDataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle4;
            this.urlDataGridView.RowHeadersVisible = false;
            this.urlDataGridView.RowTemplate.Height = 18;
            this.urlDataGridView.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.urlDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.urlDataGridView.Size = new System.Drawing.Size(1167, 476);
            this.urlDataGridView.TabIndex = 1;
            this.urlDataGridView.TabStop = false;
            this.urlDataGridView.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.urlDataGridView_CellDoubleClick);
            this.urlDataGridView.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.urlDataGridView_CellFormatting);
            this.urlDataGridView.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler(this.urlDataGridView_DataBindingComplete);
            this.urlDataGridView.RowPrePaint += new System.Windows.Forms.DataGridViewRowPrePaintEventHandler(this.urlDataGridView_RowPrePaint);
            // 
            // refreshTimer
            // 
            this.refreshTimer.Enabled = true;
            this.refreshTimer.Tick += new System.EventHandler(this.refreshTimer_Tick);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.checkButton);
            this.panel1.Controls.Add(this.urlLabel);
            this.panel1.Controls.Add(this.urlTextBox);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 25);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1167, 26);
            this.panel1.TabIndex = 3;
            // 
            // checkButton
            // 
            this.checkButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.checkButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkButton.Location = new System.Drawing.Point(1044, 1);
            this.checkButton.Name = "checkButton";
            this.checkButton.Size = new System.Drawing.Size(111, 23);
            this.checkButton.TabIndex = 2;
            this.checkButton.Text = "Check Website";
            this.checkButton.UseVisualStyleBackColor = true;
            this.checkButton.Click += new System.EventHandler(this.toolStripButtonPlay_Click);
            // 
            // urlLabel
            // 
            this.urlLabel.AutoSize = true;
            this.urlLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.urlLabel.Location = new System.Drawing.Point(12, 6);
            this.urlLabel.Name = "urlLabel";
            this.urlLabel.Size = new System.Drawing.Size(36, 13);
            this.urlLabel.TabIndex = 1;
            this.urlLabel.Text = "URL:";
            // 
            // urlTextBox
            // 
            this.urlTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.urlTextBox.Location = new System.Drawing.Point(50, 3);
            this.urlTextBox.Name = "urlTextBox";
            this.urlTextBox.Size = new System.Drawing.Size(988, 20);
            this.urlTextBox.TabIndex = 0;
            this.urlTextBox.Text = "http://www.expeditions.com/";
            this.urlTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.urlTextBox_KeyDown);
            // 
            // statusImageList
            // 
            this.statusImageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("statusImageList.ImageStream")));
            this.statusImageList.TransparentColor = System.Drawing.Color.Transparent;
            this.statusImageList.Images.SetKeyName(0, "success");
            this.statusImageList.Images.SetKeyName(1, "failure");
            this.statusImageList.Images.SetKeyName(2, "redirect");
            this.statusImageList.Images.SetKeyName(3, "external");
            // 
            // statusStripMain
            // 
            this.statusStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabelTotalURLs});
            this.statusStripMain.Location = new System.Drawing.Point(0, 527);
            this.statusStripMain.Name = "statusStripMain";
            this.statusStripMain.Size = new System.Drawing.Size(1167, 22);
            this.statusStripMain.TabIndex = 4;
            this.statusStripMain.Text = "statusStrip1";
            // 
            // toolStripStatusLabelTotalURLs
            // 
            this.toolStripStatusLabelTotalURLs.Name = "toolStripStatusLabelTotalURLs";
            this.toolStripStatusLabelTotalURLs.Size = new System.Drawing.Size(0, 17);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // toolStripButtonLoadURLList
            // 
            this.toolStripButtonLoadURLList.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonLoadURLList.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonLoadURLList.Name = "toolStripButtonLoadURLList";
            this.toolStripButtonLoadURLList.Size = new System.Drawing.Size(82, 22);
            this.toolStripButtonLoadURLList.Text = "Load URL List";
            this.toolStripButtonLoadURLList.Click += new System.EventHandler(this.toolStripButtonLoadURLList_Click);
            // 
            // openFileDialog
            // 
            this.openFileDialog.FileName = "openFileDialog";
            this.openFileDialog.RestoreDirectory = true;
            // 
            // statusDataGridViewImageColumn
            // 
            this.statusDataGridViewImageColumn.HeaderText = "Status";
            this.statusDataGridViewImageColumn.Image = ((System.Drawing.Image)(resources.GetObject("statusDataGridViewImageColumn.Image")));
            this.statusDataGridViewImageColumn.Name = "statusDataGridViewImageColumn";
            this.statusDataGridViewImageColumn.ReadOnly = true;
            this.statusDataGridViewImageColumn.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.statusDataGridViewImageColumn.Width = 50;
            // 
            // idDataGridViewTextBoxColumn
            // 
            this.idDataGridViewTextBoxColumn.DataPropertyName = "Id";
            this.idDataGridViewTextBoxColumn.HeaderText = "ID";
            this.idDataGridViewTextBoxColumn.Name = "idDataGridViewTextBoxColumn";
            this.idDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // statusDataGridViewTextBoxColumn
            // 
            this.statusDataGridViewTextBoxColumn.DataPropertyName = "Status";
            this.statusDataGridViewTextBoxColumn.HeaderText = "Status";
            this.statusDataGridViewTextBoxColumn.Name = "statusDataGridViewTextBoxColumn";
            this.statusDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // depthDataGridViewTextBoxColumn
            // 
            this.depthDataGridViewTextBoxColumn.DataPropertyName = "Depth";
            this.depthDataGridViewTextBoxColumn.HeaderText = "Depth";
            this.depthDataGridViewTextBoxColumn.Name = "depthDataGridViewTextBoxColumn";
            this.depthDataGridViewTextBoxColumn.ReadOnly = true;
            this.depthDataGridViewTextBoxColumn.Width = 50;
            // 
            // urlDataGridViewTextBoxColumn
            // 
            this.urlDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.urlDataGridViewTextBoxColumn.DataPropertyName = "Url";
            this.urlDataGridViewTextBoxColumn.HeaderText = "Url";
            this.urlDataGridViewTextBoxColumn.Name = "urlDataGridViewTextBoxColumn";
            this.urlDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // mimeTypeDataGridViewTextBoxColumn
            // 
            this.mimeTypeDataGridViewTextBoxColumn.DataPropertyName = "MimeType";
            this.mimeTypeDataGridViewTextBoxColumn.HeaderText = "Mime Type";
            this.mimeTypeDataGridViewTextBoxColumn.Name = "mimeTypeDataGridViewTextBoxColumn";
            this.mimeTypeDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // contentLengthDataGridViewTextBoxColumn
            // 
            this.contentLengthDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.contentLengthDataGridViewTextBoxColumn.DataPropertyName = "ContentLength";
            this.contentLengthDataGridViewTextBoxColumn.HeaderText = "Content Length";
            this.contentLengthDataGridViewTextBoxColumn.Name = "contentLengthDataGridViewTextBoxColumn";
            this.contentLengthDataGridViewTextBoxColumn.ReadOnly = true;
            this.contentLengthDataGridViewTextBoxColumn.Width = 97;
            // 
            // TimeTaken
            // 
            this.TimeTaken.DataPropertyName = "TimeTaken";
            dataGridViewCellStyle2.Format = "N0";
            dataGridViewCellStyle2.NullValue = null;
            this.TimeTaken.DefaultCellStyle = dataGridViewCellStyle2;
            this.TimeTaken.HeaderText = "Time Taken";
            this.TimeTaken.Name = "TimeTaken";
            this.TimeTaken.ReadOnly = true;
            // 
            // notesDataGridViewTextBoxColumn
            // 
            this.notesDataGridViewTextBoxColumn.DataPropertyName = "Notes";
            this.notesDataGridViewTextBoxColumn.HeaderText = "Notes";
            this.notesDataGridViewTextBoxColumn.Name = "notesDataGridViewTextBoxColumn";
            this.notesDataGridViewTextBoxColumn.ReadOnly = true;
            this.notesDataGridViewTextBoxColumn.Width = 300;
            // 
            // HighlightColor
            // 
            this.HighlightColor.DataPropertyName = "HighlightColor";
            this.HighlightColor.HeaderText = "HighlightColor";
            this.HighlightColor.Name = "HighlightColor";
            this.HighlightColor.ReadOnly = true;
            this.HighlightColor.Visible = false;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1167, 549);
            this.Controls.Add(this.urlDataGridView);
            this.Controls.Add(this.statusStripMain);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.mainToolStrip);
            this.Name = "MainForm";
            this.Text = "Web Crawler";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.mainToolStrip.ResumeLayout(false);
            this.mainToolStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.urlDataGridView)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.statusStripMain.ResumeLayout(false);
            this.statusStripMain.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ToolStrip mainToolStrip;
		private System.Windows.Forms.ToolStripButton toolStripButtonPlay;
		private System.Windows.Forms.ToolStripButton toolStripButtonPause;
		private System.Windows.Forms.ToolStripButton toolStripButtonStop;
		private System.Windows.Forms.DataGridView urlDataGridView;
		private System.Windows.Forms.Timer refreshTimer;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Button checkButton;
		private System.Windows.Forms.Label urlLabel;
		private System.Windows.Forms.TextBox urlTextBox;
		private System.Windows.Forms.ImageList statusImageList;
		private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
		private System.Windows.Forms.StatusStrip statusStripMain;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelTotalURLs;
		private System.Windows.Forms.ToolStripButton toolStripButtonShowNextError;
		private System.Windows.Forms.ToolStripButton toolStripButtonSettings;
		private System.Windows.Forms.ToolStripButton toolStripButtonHighlight;
        private System.Windows.Forms.ToolStripButton toolStripButtonLoadURLList;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.DataGridViewImageColumn statusDataGridViewImageColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn idDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn statusDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn depthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn urlDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn mimeTypeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn contentLengthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn TimeTaken;
        private System.Windows.Forms.DataGridViewTextBoxColumn notesDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn HighlightColor;
    }
}

