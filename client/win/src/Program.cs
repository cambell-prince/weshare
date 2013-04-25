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
		/// <summary>
		/// Queue of filesystem changes to push to the server as we have time.
		/// </summary>
		static Queue<PushAction> _pushes;
		/// <summary>
		/// Timer to periodically check the queue.
		/// </summary>
		static System.Timers.Timer _timer;
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

			// Set the watchers we need to monitor changes in the WeShare folder (and subfolders).
			_watchers = new Dictionary<string, FileSystemWatcher>();
			CreateWatchers(_sharedir);
			// Create the queue of filesystem changes to push to the server.
			_pushes = new Queue<PushAction>();

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
				_timer = new System.Timers.Timer(1000.0);	// cycle once per second
				_timer.Elapsed += new System.Timers.ElapsedEventHandler(Timer_Elapsed);
				_timer.AutoReset = true;
				_timer.SynchronizingObject = null;
				_timer.Start();
				Application.Run();
			}
		}

		static void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			_timer.Stop();
			if (_pushes.Count > 0)
			{
				// Push this change to the server.
				var action = _pushes.Dequeue();
				action.Push();
				Debug.WriteLine(String.Format("Idle: pushing {0}", action));
			}
			else
			{
				// TODO: Pull a change that the server knows about (if any).
			}
			_timer.Start();
		}

		private static void CreateWatchers(string directory)
		{
			if (!_watchers.ContainsKey(directory))
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
			}
			foreach (var dir in Directory.EnumerateDirectories(directory))
			{
				if (dir == directory)
					continue;
				CreateWatchers(dir);
			}
		}

		private static void OnShowSetup(object sender, EventArgs e)
		{
			using (var setup = new SetupDlg())
			{
				setup.ShowDialog();
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

		private static void OnChanged(object source, FileSystemEventArgs e)
		{
			if (File.Exists(e.FullPath))
			{
				//Debug.WriteLine(String.Format("File: {0} {1}", e.FullPath, e.ChangeType));
				_pushes.Enqueue(new PushAction(PushAction.ActionType.Change, e.FullPath));
			}
		}

		private static void OnCreated(object source, FileSystemEventArgs e)
		{
			if (Directory.Exists(e.FullPath))
			{
				//Debug.WriteLine(String.Format("Directory: {0} {1}", e.FullPath, e.ChangeType));
				CreateWatchers(e.FullPath);
			}
			else
			{
				//Debug.WriteLine(String.Format("File: {0} {1}", e.FullPath, e.ChangeType));
			}
			_pushes.Enqueue(new PushAction(PushAction.ActionType.Create, e.FullPath));
		}

		private static void OnDeleted(object source, FileSystemEventArgs e)
		{
			if (_watchers.ContainsKey(e.FullPath))
			{
				//Debug.WriteLine(String.Format("Directory: {0} {1}", e.FullPath, e.ChangeType));
				_watchers[e.FullPath].Dispose();
				_watchers.Remove(e.FullPath);
			}
			else
			{
				//Debug.WriteLine(String.Format("File: {0} {1}", e.FullPath, e.ChangeType));
			}
			_pushes.Enqueue(new PushAction(PushAction.ActionType.Delete, e.FullPath));
		}

		private static void OnRenamed(object source, RenamedEventArgs e)
		{
			if (Directory.Exists(e.FullPath))
			{
				//Debug.WriteLine(String.Format("Directory: {0} renamed to {1}", e.OldFullPath, e.FullPath));
				var defunct = new List<string>();
				foreach (var dir in _watchers.Keys)
				{
					if (dir == e.OldFullPath ||
						dir.StartsWith(e.OldFullPath + Path.DirectorySeparatorChar))
					{
						defunct.Add(dir);
					}
				}
				foreach (var dir in defunct)
				{
					_watchers[dir].Dispose();
					_watchers.Remove(dir);
				}
				CreateWatchers(e.FullPath);
			}
			else
			{
				//Debug.WriteLine(String.Format("File: {0} renamed to {1}", e.OldFullPath, e.FullPath));
			}
			_pushes.Enqueue(new PushAction(e.OldFullPath, e.FullPath));
		}
	}
}
