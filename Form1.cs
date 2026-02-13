using System;
using System.Windows.Forms;
using System.Net;

namespace RacePoE
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			using (WebClient client = new WebClient())
			{
				// Set a User-Agent header to mimic a browser
				client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

				// Get HTML
				string response = client.DownloadString("https://www.pathofexile.com/ladders");

				// Find all h2 elements inside elements that are class ladderView
				var ladderViews = response.Split(new string[] { "<div class=\"ladderView\">" }, StringSplitOptions.None);
				for (int i = 1; i < ladderViews.Length; i++)
				{
					string[] leagueSections = ladderViews[i].Split(new string[] { "<h2>" }, StringSplitOptions.None);
					if (leagueSections.Length > 1)
					{
						string league = leagueSections[1].Split(new string[] { "</h2>" }, StringSplitOptions.None)[0];
						leagueComboBox.Items.Add(league);
					}
				}
				if (leagueComboBox.Items.Count > 0)
				{
					leagueComboBox.SelectedIndex = 1;
				}
			}
		}

		private void overlayStart_Click(object sender, EventArgs e)
		{
			// Get user input
			string characterName = characterNameInput.Text;
			string selectedLeague = leagueComboBox.SelectedItem.ToString();

			// Load the overlay form with the character name and league
			Overlayform overlay = new Overlayform(characterName, selectedLeague);

			// Hide the main form and show the overlay
			this.Hide();
		}
	}
}
