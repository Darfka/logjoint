﻿namespace LogJoint.UI
{
	partial class OptionsDialog
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
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OptionsDialog));
			this.okButton = new System.Windows.Forms.Button();
			this.cancelButton = new System.Windows.Forms.Button();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.memAndPerformanceTabPage = new System.Windows.Forms.TabPage();
			this.memAndPerformanceSettingsView = new LogJoint.UI.MemAndPerformanceSettingsView();
			this.tabControl1.SuspendLayout();
			this.memAndPerformanceTabPage.SuspendLayout();
			this.SuspendLayout();
			// 
			// okButton
			// 
			this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.okButton.Location = new System.Drawing.Point(350, 409);
			this.okButton.Margin = new System.Windows.Forms.Padding(4);
			this.okButton.Name = "okButton";
			this.okButton.Size = new System.Drawing.Size(94, 29);
			this.okButton.TabIndex = 1;
			this.okButton.Text = "OK";
			this.okButton.UseVisualStyleBackColor = true;
			this.okButton.Click += new System.EventHandler(this.okButton_Click);
			// 
			// cancelButton
			// 
			this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.cancelButton.Location = new System.Drawing.Point(465, 409);
			this.cancelButton.Margin = new System.Windows.Forms.Padding(4);
			this.cancelButton.Name = "cancelButton";
			this.cancelButton.Size = new System.Drawing.Size(94, 29);
			this.cancelButton.TabIndex = 2;
			this.cancelButton.Text = "Cancel";
			this.cancelButton.UseVisualStyleBackColor = true;
			this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
			// 
			// tabControl1
			// 
			this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.tabControl1.Controls.Add(this.memAndPerformanceTabPage);
			this.tabControl1.Location = new System.Drawing.Point(2, 2);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(569, 397);
			this.tabControl1.TabIndex = 3;
			// 
			// memAndPerformanceTabPage
			// 
			this.memAndPerformanceTabPage.Controls.Add(this.memAndPerformanceSettingsView);
			this.memAndPerformanceTabPage.Location = new System.Drawing.Point(4, 26);
			this.memAndPerformanceTabPage.Name = "memAndPerformanceTabPage";
			this.memAndPerformanceTabPage.Padding = new System.Windows.Forms.Padding(7);
			this.memAndPerformanceTabPage.Size = new System.Drawing.Size(561, 367);
			this.memAndPerformanceTabPage.TabIndex = 0;
			this.memAndPerformanceTabPage.Text = "Resources and performance";
			this.memAndPerformanceTabPage.UseVisualStyleBackColor = true;
			// 
			// memAndPerformanceSettingsView
			// 
			this.memAndPerformanceSettingsView.Dock = System.Windows.Forms.DockStyle.Fill;
			this.memAndPerformanceSettingsView.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
			this.memAndPerformanceSettingsView.Location = new System.Drawing.Point(7, 7);
			this.memAndPerformanceSettingsView.Margin = new System.Windows.Forms.Padding(5);
			this.memAndPerformanceSettingsView.Name = "memAndPerformanceSettingsView";
			this.memAndPerformanceSettingsView.Size = new System.Drawing.Size(547, 353);
			this.memAndPerformanceSettingsView.TabIndex = 0;
			// 
			// OptionsDialog
			// 
			this.AcceptButton = this.okButton;
			this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.CancelButton = this.cancelButton;
			this.ClientSize = new System.Drawing.Size(574, 450);
			this.Controls.Add(this.tabControl1);
			this.Controls.Add(this.cancelButton);
			this.Controls.Add(this.okButton);
			this.Font = new System.Drawing.Font("Tahoma", 8.25F);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.Margin = new System.Windows.Forms.Padding(4);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "OptionsDialog";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Options";
			this.tabControl1.ResumeLayout(false);
			this.memAndPerformanceTabPage.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button okButton;
		private System.Windows.Forms.Button cancelButton;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage memAndPerformanceTabPage;
		internal MemAndPerformanceSettingsView memAndPerformanceSettingsView;
	}
}