namespace WeShare
{
	partial class SetupDlg
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
			this._btnClose = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// _btnClose
			// 
			this._btnClose.Location = new System.Drawing.Point(179, 221);
			this._btnClose.Name = "_btnClose";
			this._btnClose.Size = new System.Drawing.Size(75, 23);
			this._btnClose.TabIndex = 0;
			this._btnClose.Text = "Close";
			this._btnClose.UseVisualStyleBackColor = true;
			this._btnClose.Click += new System.EventHandler(this._btnClose_Click);
			// 
			// SetupDlg
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(284, 262);
			this.ControlBox = false;
			this.Controls.Add(this._btnClose);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "SetupDlg";
			this.Text = "WeShare Setup";
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button _btnClose;
	}
}