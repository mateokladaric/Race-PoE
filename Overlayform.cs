using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RacePoE
{
	public partial class Overlayform : Form
	{
		// pinvoke findwindow
		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		//pinvoke setwindowpos
		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

		//pinvoke getforegroundwindow
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		// pinvoke showwindow
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		// pinvoke getwindowrect
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		// pinvoke hotkeys
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left, Top, Right, Bottom;
			public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
		}

		// Consts
		private const uint SWP_NOSIZE = 0x0001;
		private const uint SWP_NOMOVE = 0x0002;
		private const uint SWP_NOACTIVATE = 0x0010;
		private const int SW_SHOWNOACTIVATE = 4;
		private const int SW_HIDE = 0;
		private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

		// Hotkey consts
		private const int WM_HOTKEY = 0x0312;
		private const int HOTKEY_SCALE_UP = 1;
		private const int HOTKEY_SCALE_DOWN = 2;
		private const uint VK_ADD = 0x6B;
		private const uint VK_SUBTRACT = 0x6D;

		// Scale
		private float _scale = 1.0f;
		private const float ScaleStep = 0.1f;
		private const float ScaleMin = 0.5f;
		private const float ScaleMax = 2.0f;

		// Fonts
		private readonly Font _playerFont = new Font("Fontin", 22, FontStyle.Bold);
		private readonly Font _neighborFont = new Font("Fontin", 13);
		private readonly Font _statusFont = new Font("Fontin", 13);
		private readonly Font _rankFont = new Font("Fontin", 11, FontStyle.Bold);
		private readonly Font _xpRateFont = new Font("Fontin", 12, FontStyle.Bold);
		private bool _isVisible = false;

		// Theme colors — black & gold
		private static readonly Color PanelBg = Color.FromArgb(240, 6, 6, 10);
		private static readonly Color PanelBorder = Color.FromArgb(150, 170, 145, 40);
		private static readonly Color GoldText = Color.FromArgb(255, 225, 195, 130);
		private static readonly Color BrightGold = Color.FromArgb(255, 245, 200, 60);
		private static readonly Color MutedText = Color.FromArgb(140, 155, 155, 155);
		private static readonly Color DimText = Color.FromArgb(120, 140, 135, 110);
		private static readonly Color SeparatorCol = Color.FromArgb(50, 160, 140, 40);
		private static readonly Color HighlightBg = Color.FromArgb(35, 180, 155, 30);
		private static readonly Color XpRateGreen = Color.FromArgb(255, 80, 210, 80);

		// FPS tracking
		private int _frameCount = 0;
		private double _fps = 0;
		private readonly System.Diagnostics.Stopwatch _fpsStopwatch = new System.Diagnostics.Stopwatch();
		private readonly System.Threading.Timer _animationTimer = null;

		// Animation
		private float _animX = 0;
		private int _animDirection = 1;

		// XP/hr tracking
		private long _xpStartValue = -1;
		private DateTime _xpStartTime;

		// Screen tracking
		private Rectangle _currentScreenBounds;

		// Ladder tracking
		private LadderTracker _ladderTracker;
		private CancellationTokenSource _ladderCts;
		private readonly System.Windows.Forms.Timer _refreshTimer = new System.Windows.Forms.Timer();

		// public
		public string CharacterName { get; set; }
		public string LeagueName { get; set; }

		protected override bool ShowWithoutActivation => true;

		public Overlayform(string characterName, string leagueName)
		{
			InitializeComponent();
			this.CharacterName = characterName;
			this.LeagueName = leagueName;
			this.FormBorderStyle = FormBorderStyle.None;
			this.BackColor = Color.FromArgb(1, 1, 1);
			this.TransparencyKey = Color.FromArgb(1, 1, 1);
			this.TopMost = true;
			this.StartPosition = FormStartPosition.Manual;
			this.Bounds = Screen.PrimaryScreen.Bounds;
			this._currentScreenBounds = Screen.PrimaryScreen.Bounds;
			this.ShowInTaskbar = false;
			this.DoubleBuffered = true;
			this.Show();
		}

		private void Overlayform_Load(object sender, EventArgs e)
		{
			// Register numpad +/- as global hotkeys
			RegisterHotKey(this.Handle, HOTKEY_SCALE_UP, 0, VK_ADD);
			RegisterHotKey(this.Handle, HOTKEY_SCALE_DOWN, 0, VK_SUBTRACT);

			// Start ladder tracker
			_ladderTracker = new LadderTracker(CharacterName, LeagueName);
			_ladderCts = new CancellationTokenSource();
			Task.Run(() => _ladderTracker.RunLoop(_ladderCts.Token));

			// Refresh overlay display every 500ms
			_refreshTimer.Interval = 500;
			_refreshTimer.Tick += (s, ev) => { if (_isVisible) this.Invalidate(); };
			_refreshTimer.Start();

			Task.Run(() =>
			{
				while (true)
				{
					var poeWindow = FindWindow(null, "Path of Exile");

					// If PoE is no longer running, shut everything down
					if (poeWindow == IntPtr.Zero)
					{
						this.Invoke((MethodInvoker)delegate
						{
							Application.Exit();
						});
						return;
					}

					bool poeIsForeground = GetForegroundWindow() == poeWindow;

					if (!poeIsForeground)
					{
						if (_isVisible)
						{
							this.Invoke((MethodInvoker)delegate
							{
								ShowWindow(this.Handle, SW_HIDE);
								_isVisible = false;
							});
						}
						System.Threading.Thread.Sleep(300);
						continue;
					}

					this.Invoke((MethodInvoker)delegate
					{
						// Match overlay to whichever screen PoE is on
						if (GetWindowRect(poeWindow, out RECT poeRect))
						{
							var poeScreen = Screen.FromRectangle(poeRect.ToRectangle());
							if (poeScreen.Bounds != _currentScreenBounds)
							{
								_currentScreenBounds = poeScreen.Bounds;
								this.Bounds = _currentScreenBounds;
							}
						}

						if (!_isVisible)
						{
							ShowWindow(this.Handle, SW_SHOWNOACTIVATE);
							_isVisible = true;
							this.Invalidate();
						}

						// Keep overlay topmost without activating
						SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0,
							SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
					});

					System.Threading.Thread.Sleep(50);
				}
			});
		}

		protected override void WndProc(ref Message m)
		{
			if (m.Msg == WM_HOTKEY)
			{
				int id = m.WParam.ToInt32();
				if (id == HOTKEY_SCALE_UP)
				{
					_scale = Math.Min(_scale + ScaleStep, ScaleMax);
					this.Invalidate();
				}
				else if (id == HOTKEY_SCALE_DOWN)
				{
					_scale = Math.Max(_scale - ScaleStep, ScaleMin);
					this.Invalidate();
				}
			}
			base.WndProc(ref m);
		}

		private string FormatXpRate(double xpPerHour)
		{
			if (xpPerHour >= 1_000_000_000)
				return $"+{xpPerHour / 1_000_000_000:F1}B/hr";
			if (xpPerHour >= 1_000_000)
				return $"+{xpPerHour / 1_000_000:F1}M/hr";
			if (xpPerHour >= 1_000)
				return $"+{xpPerHour / 1_000:F1}K/hr";
			return $"+{xpPerHour:F0}/hr";
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

			// Apply scale transform — all drawing scales uniformly from top-left
			g.ScaleTransform(_scale, _scale);

			if (_ladderTracker == null)
				return;

			float panelX = 14;
			float panelY = 14;
			float pad = 16;

			if (_ladderTracker.LastError != null)
			{
				string errMsg = _ladderTracker.LastError;
				float errW = Math.Max(g.MeasureString(errMsg, _statusFont).Width + pad * 2 + 10, 360);
				DrawPanel(g, panelX, panelY, errW, 44);
				using (var b = new SolidBrush(Color.FromArgb(255, 200, 80, 70)))
					g.DrawString(errMsg, _statusFont, b, panelX + pad, panelY + 12);
				return;
			}

			var result = _ladderTracker.LastResult;

			if (result == null)
			{
				string status = _ladderTracker.IsSearching
					? "Searching ladder..."
					: $"'{CharacterName}' not found";
				float statusW = g.MeasureString(status, _statusFont).Width + pad * 2 + 10;
				DrawPanel(g, panelX, panelY, statusW, 44);
				using (var b = new SolidBrush(GoldText))
					g.DrawString(status, _statusFont, b, panelX + pad, panelY + 12);
				return;
			}

			// Track XP/hr
			var p = result.Player;
			if (_xpStartValue < 0)
			{
				_xpStartValue = p.Experience;
				_xpStartTime = DateTime.UtcNow;
			}

			string xpRateStr = null;
			double elapsedHours = (DateTime.UtcNow - _xpStartTime).TotalHours;
			if (elapsedHours > 1.0 / 60.0)
			{
				double xpPerHour = (p.Experience - _xpStartValue) / elapsedHours;
				if (xpPerHour > 0)
					xpRateStr = FormatXpRate(xpPerHour);
			}

			// Measure content to size the panel
			string playerName = p.CharacterName;
			string playerInfo = $"Lv {p.Level}   {p.ClassName}   ";
			string playerXp = $"XP {p.Experience:N0}";
			string xpRateDisplay = xpRateStr != null ? $"  ({xpRateStr})" : "";
			var nameSize = g.MeasureString(playerName, _playerFont);
			var infoSize = g.MeasureString(playerInfo, _statusFont);
			var xpSize = g.MeasureString(playerXp, _statusFont);
			var xpRateSize = xpRateStr != null ? g.MeasureString(xpRateDisplay, _xpRateFont) : SizeF.Empty;
			string rankStr = $"#{p.Rank}";
			float rankWidth = g.MeasureString(rankStr, _rankFont).Width + 10;

			float detailLineWidth = rankWidth + infoSize.Width + xpSize.Width + xpRateSize.Width;
			float contentWidth = Math.Max(nameSize.Width + rankWidth, detailLineWidth) + 20;

			// Also measure neighbors to ensure panel is wide enough
			if (result.PlayerBefore != null)
			{
				var pb = result.PlayerBefore;
				string nbText = $"#{pb.Rank}  {pb.CharacterName}   Lv {pb.Level}  {pb.ClassName}   XP {pb.Experience:N0}";
				contentWidth = Math.Max(contentWidth, g.MeasureString(nbText, _neighborFont).Width + 10);
			}
			if (result.PlayerAfter != null)
			{
				var pa = result.PlayerAfter;
				string naText = $"#{pa.Rank}  {pa.CharacterName}   Lv {pa.Level}  {pa.ClassName}   XP {pa.Experience:N0}";
				contentWidth = Math.Max(contentWidth, g.MeasureString(naText, _neighborFont).Width + 10);
			}

			float rowNeighbor = 28;
			float rowPlayer = nameSize.Height + infoSize.Height + 8;
			float contentHeight = rowPlayer;
			if (result.PlayerBefore != null) contentHeight += rowNeighbor + 12;
			if (result.PlayerAfter != null) contentHeight += 12 + rowNeighbor;

			float panelW = contentWidth + pad * 2;
			float panelH = contentHeight + pad * 2;

			// Draw panel background
			DrawPanel(g, panelX, panelY, panelW, panelH);

			// Clip all content to the panel shape so nothing pokes out
			var savedClip = g.Clip;
			using (var clipPath = RoundedRectPath(panelX, panelY, panelW, panelH, 8))
			{
				g.SetClip(clipPath);

				float x = panelX + pad;
				float y = panelY + pad;

				// --- Neighbor above ---
				if (result.PlayerBefore != null)
				{
					DrawNeighborRow(g, result.PlayerBefore, x, y);
					y += rowNeighbor;

					using (var pen = new Pen(SeparatorCol, 1))
						g.DrawLine(pen, x, y + 4, x + contentWidth, y + 4);
					y += 12;
				}

				// --- Current player highlight bar ---
				using (var highlight = new SolidBrush(HighlightBg))
					g.FillRectangle(highlight, panelX, y - 6, panelW, rowPlayer + 12);

				// Gold accent on left edge
				using (var accent = new SolidBrush(BrightGold))
					g.FillRectangle(accent, panelX, y - 6, 3, rowPlayer + 12);

				// Rank badge
				using (var b = new SolidBrush(BrightGold))
					g.DrawString(rankStr, _rankFont, b, x, y + 6);

				// Player name
				using (var b = new SolidBrush(GoldText))
					g.DrawString(playerName, _playerFont, b, x + rankWidth, y - 2);
				y += nameSize.Height + 2;

				// Level / Class (dim)
				float detailX = x + rankWidth;
				g.DrawString(playerInfo, _statusFont, Brushes.White, detailX, y);
				detailX += infoSize.Width;

				// XP value (white)
				g.DrawString(playerXp, _statusFont, Brushes.White, detailX, y);
				detailX += xpSize.Width;

				// XP/hr rate (green)
				if (xpRateStr != null)
				{
					using (var b = new SolidBrush(XpRateGreen))
						g.DrawString(xpRateDisplay, _xpRateFont, b, detailX, y - 1);
				}

				y += infoSize.Height + 6;

				// --- Neighbor below ---
				if (result.PlayerAfter != null)
				{
					using (var pen = new Pen(SeparatorCol, 1))
						g.DrawLine(pen, x, y + 2, x + contentWidth, y + 2);
					y += 12;

					DrawNeighborRow(g, result.PlayerAfter, x, y);
				}

				g.Clip = savedClip;
			}
		}

		private void DrawNeighborRow(Graphics g, LadderEntry entry, float x, float y)
		{
			string rank = $"#{entry.Rank}";
			string text = $"  {entry.CharacterName}   Lv {entry.Level}  {entry.ClassName}   XP {entry.Experience:N0}";

			using (var rb = new SolidBrush(DimText))
				g.DrawString(rank, _rankFont, rb, x, y + 2);

			float rankW = g.MeasureString(rank, _rankFont).Width;

			using (var b = new SolidBrush(MutedText))
				g.DrawString(text, _neighborFont, b, x + rankW, y);
		}

		private void DrawPanel(Graphics g, float x, float y, float w, float h)
		{
			using (var bgBrush = new SolidBrush(PanelBg))
				FillRoundedRect(g, bgBrush, x, y, w, h, 8);

			using (var borderPen = new Pen(PanelBorder, 1.2f))
				DrawRoundedRect(g, borderPen, x, y, w, h, 8);
		}

		private static GraphicsPath RoundedRectPath(float x, float y, float w, float h, float r)
		{
			var path = new GraphicsPath();
			float d = r * 2;
			path.AddArc(x, y, d, d, 180, 90);
			path.AddArc(x + w - d, y, d, d, 270, 90);
			path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
			path.AddArc(x, y + h - d, d, d, 90, 90);
			path.CloseFigure();
			return path;
		}

		private static void FillRoundedRect(Graphics g, Brush brush, float x, float y, float w, float h, float r)
		{
			using (var path = RoundedRectPath(x, y, w, h, r))
				g.FillPath(brush, path);
		}

		private static void DrawRoundedRect(Graphics g, Pen pen, float x, float y, float w, float h, float r)
		{
			using (var path = RoundedRectPath(x, y, w, h, r))
				g.DrawPath(pen, path);
		}

		protected override CreateParams CreateParams
		{
			get
			{
				const int WS_EX_TOOLWINDOW = 0x00000080;
				const int WS_EX_TRANSPARENT = 0x00000020;
				const int WS_EX_LAYERED = 0x00080000;
				const int WS_EX_NOACTIVATE = 0x08000000;
				CreateParams cp = base.CreateParams;
				cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE;
				return cp;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				UnregisterHotKey(this.Handle, HOTKEY_SCALE_UP);
				UnregisterHotKey(this.Handle, HOTKEY_SCALE_DOWN);
				_ladderCts?.Cancel();
				_refreshTimer?.Dispose();
				_playerFont?.Dispose();
				_statusFont?.Dispose();
				_neighborFont?.Dispose();
				_rankFont?.Dispose();
				_xpRateFont?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
