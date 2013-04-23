using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;

using WeShare.Properties;

namespace WeShare
{
	static class Program
	{
		/// <summary>
		/// The full path to the shared directory.
		/// </summary>
		static string _sharedir;
		/// <summary>
		/// Provides asynchronous monitoring of the shared directory and its contents.
		/// </summary>
		static Dictionary<string, FileSystemWatcher> _watchers;

		static SetupDlg _setup;
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

		[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
		private static void RunCore(string[] args)
		{
			// TODO: settle on "correct" value for this directory path.
			_sharedir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WeShare");
			if (!Directory.Exists(_sharedir))
				Directory.CreateDirectory(_sharedir);

			// Set the watchers we need to minitor changes in the WeShare folder (and subfolders).
			_watchers = new Dictionary<string, FileSystemWatcher>();
			CreateWatchers(_sharedir);

			using (var trayIcon = new NotifyIcon())
			{
				trayIcon.Text = Application.ProductName;
				trayIcon.Icon = Resources.WeShareTrayIcon;
				trayIcon.BalloonTipText = "WeShare is sharing?";

				var trayMenu = new ContextMenu();
				trayMenu.MenuItems.Add("Show Setup", OnShowSetup);
				trayMenu.MenuItems.Add("Exit", OnExit);
				trayIcon.ContextMenu = trayMenu;
				trayIcon.Visible = true;
				trayIcon.MouseClick += new MouseEventHandler(trayIcon_MouseClick);

				Application.Idle += new EventHandler(Application_Idle);
				Application.Run();
			}
		}

		private static void CreateWatchers(string directory)
		{
			var watcher = new FileSystemWatcher(directory);
			watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName |
				NotifyFilters.DirectoryName;
			watcher.Changed += new FileSystemEventHandler(OnChanged);
			watcher.Created += new FileSystemEventHandler(OnCreated);
			watcher.Deleted += new FileSystemEventHandler(OnDeleted);
			watcher.Renamed += new RenamedEventHandler(OnRenamed);
			watcher.EnableRaisingEvents = true;
			_watchers.Add(directory, watcher);
			foreach (var dir in Directory.EnumerateDirectories(directory))
			{
				if (dir == directory)
					continue;
				CreateWatchers(dir);
			}
		}

		private static void OnShowSetup(object sender, EventArgs e)
		{
			using (_setup = new SetupDlg())
				_setup.ShowDialog();
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
			// TODO: next step in updating to/from the network...
		}

		private static void OnChanged(object source, FileSystemEventArgs e)
		{
			if (File.Exists(e.FullPath))
			{
				Debug.WriteLine(String.Format("File: {0} {1}", e.FullPath, e.ChangeType));
			}
		}

		private static void OnCreated(object source, FileSystemEventArgs e)
		{
			if (Directory.Exists(e.FullPath))
			{
				Debug.WriteLine(String.Format("Directory: {0} {1}", e.FullPath, e.ChangeType));
				CreateWatchers(e.FullPath);
			}
			else
			{
				Debug.WriteLine(String.Format("File: {0} {1}", e.FullPath, e.ChangeType));
			}
		}

		private static void OnDeleted(object source, FileSystemEventArgs e)
		{
			if (_watchers.ContainsKey(e.FullPath))
			{
				Debug.WriteLine(String.Format("Directory: {0} {1}", e.FullPath, e.ChangeType));
				_watchers[e.FullPath].Dispose();
				_watchers.Remove(e.FullPath);
			}
			else
			{
				Debug.WriteLine(String.Format("File: {0} {1}", e.FullPath, e.ChangeType));
			}
		}

		private static void OnRenamed(object source, RenamedEventArgs e)
		{
			if (Directory.Exists(e.FullPath))
			{
				Debug.WriteLine(String.Format("Directory: {0} renamed to {1}", e.OldFullPath, e.FullPath));
				foreach (string dir in _watchers.Keys)
				{
					if (dir == e.OldFullPath ||
						dir.StartsWith(e.OldFullPath + Path.DirectorySeparatorChar))
					{
						_watchers[dir].Dispose();
						_watchers.Remove(dir);
					}
				}
				CreateWatchers(e.FullPath);
			}
			else
			{
				Debug.WriteLine(String.Format("File: {0} renamed to {1}", e.OldFullPath, e.FullPath));
			}
		}
	}
}
