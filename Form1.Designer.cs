namespace RacePoE
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
			this.characterNameInput = new System.Windows.Forms.TextBox();
			this.charNameLabel = new System.Windows.Forms.Label();
			this.LeageLabel = new System.Windows.Forms.Label();
			this.leagueComboBox = new System.Windows.Forms.ComboBox();
			this.overlayStart = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// characterNameInput
			// 
			this.characterNameInput.Location = new System.Drawing.Point(62, 46);
			this.characterNameInput.Name = "characterNameInput";
			this.characterNameInput.Size = new System.Drawing.Size(179, 20);
			this.characterNameInput.TabIndex = 0;
			// 
			// charNameLabel
			// 
			this.charNameLabel.AutoSize = true;
			this.charNameLabel.Location = new System.Drawing.Point(59, 30);
			this.charNameLabel.Name = "charNameLabel";
			this.charNameLabel.Size = new System.Drawing.Size(84, 13);
			this.charNameLabel.TabIndex = 1;
			this.charNameLabel.Text = "Character Name";
			// 
			// LeageLabel
			// 
			this.LeageLabel.AutoSize = true;
			this.LeageLabel.Location = new System.Drawing.Point(59, 81);
			this.LeageLabel.Name = "LeageLabel";
			this.LeageLabel.Size = new System.Drawing.Size(43, 13);
			this.LeageLabel.TabIndex = 3;
			this.LeageLabel.Text = "League";
			// 
			// leagueComboBox
			// 
			this.leagueComboBox.FormattingEnabled = true;
			this.leagueComboBox.Location = new System.Drawing.Point(62, 98);
			this.leagueComboBox.Name = "leagueComboBox";
			this.leagueComboBox.Size = new System.Drawing.Size(179, 21);
			this.leagueComboBox.TabIndex = 4;
			// 
			// overlayStart
			// 
			this.overlayStart.Location = new System.Drawing.Point(101, 136);
			this.overlayStart.Name = "overlayStart";
			this.overlayStart.Size = new System.Drawing.Size(97, 23);
			this.overlayStart.TabIndex = 5;
			this.overlayStart.Text = "Start";
			this.overlayStart.UseVisualStyleBackColor = true;
			this.overlayStart.Click += new System.EventHandler(this.overlayStart_Click);
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(314, 184);
			this.Controls.Add(this.overlayStart);
			this.Controls.Add(this.leagueComboBox);
			this.Controls.Add(this.LeageLabel);
			this.Controls.Add(this.charNameLabel);
			this.Controls.Add(this.characterNameInput);
			this.Name = "Form1";
			this.Text = "Race PoE";
			this.Load += new System.EventHandler(this.Form1_Load);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.Label charNameLabel;
		private System.Windows.Forms.Label LeageLabel;
		private System.Windows.Forms.ComboBox leagueComboBox;
		private System.Windows.Forms.Button overlayStart;
		private System.Windows.Forms.TextBox characterNameInput;
	}
}

