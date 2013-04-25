using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace WeShare
{
	/// <summary>
	/// Record the information for one filesystem change that needs to be pushed to
	/// the server.
	/// </summary>
	public class PushAction
	{
		/// <summary>
		/// Type of filesystem change to push to the server.
		/// </summary>
		public enum ActionType
		{
			Create,
			Delete,
			Rename,
			Change
		};
		public ActionType Action { get; private set; }
		/// <summary>
		/// Path of the filesystem element that has changed in some way.
		/// </summary>
		public string Path { get; private set; }
		/// <summary>
		/// For Rename, previous path of the filesystem element.
		/// </summary>
		public string OldPath { get; private set; }
		/// <summary>
		/// Flag whether the filesystem element is a directory.
		/// </summary>
		public bool IsDirectory { get; private set; }

		/// <summary>
		/// Constructor for Create, Delete, or Change.
		/// </summary>
		public PushAction(ActionType action, string path)
		{
			Action = action;
			Path = path;
			IsDirectory = Directory.Exists(path);
		}

		/// <summary>
		/// Constructor for Rename.
		/// </summary>
		public PushAction(string oldpath, string newpath)
		{
			Action = ActionType.Rename;
			Path = newpath;
			OldPath = oldpath;
			IsDirectory = Directory.Exists(newpath);
		}

		public void Push()
		{
			// TODO: implement this!
		}

		public override string ToString()
		{
			return String.Format("PushAction[{0}, {1}, {2}, {3}]", Action, Path, OldPath, IsDirectory);
		}
	}
}
