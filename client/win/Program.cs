using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using WeShare.Properties;

namespace WeShare
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			bool createdNew;
			using (new Mutex(true, "WeShare", out createdNew))
			{
				if (createdNew)
					RunCore(args);
			}
		}

		private static void RunCore(string[] args)
		{
			var trayMenu = new ContextMenu();
			trayMenu.MenuItems.Add("Exit", OnExit);

			using (var trayIcon = new NotifyIcon())
			{
				trayIcon.Text = Application.ProductName;
				trayIcon.Icon = Resources.WeShareTrayIcon;
				trayIcon.BalloonTipText = "WeShare is sharing?";

				trayIcon.ContextMenu = trayMenu;
				trayIcon.Visible = true;
				trayIcon.MouseClick += new MouseEventHandler(trayIcon_MouseClick);

				Application.Idle += new EventHandler(Application_Idle);
				Application.Run();
			}
		}

		private static void OnExit(object sender, EventArgs e)
		{
			Application.Exit();
		}

		static void trayIcon_MouseClick(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left)
				return;
		}

		static void Application_Idle(object sender, EventArgs e)
		{
			// TODO: something or other...
		}
	}
}
