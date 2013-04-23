using System;
using System.Collections.Generic;

// These classes were created to allow the project to compile.  They should either be deleted or
// enhanced to really work.

namespace WeShare.Transport
{
	public class RevisionNumber
	{
		public RevisionNumber(string local, string hash)
		{
			LocalRevisionNumber = local;
			Hash = hash;
		}
		public RevisionNumber(string combinedNumberAndHash)
		{
			string[] parts = combinedNumberAndHash.Split(new char[] { ':' });
			Hash = parts[1].Trim();
			LocalRevisionNumber = parts[0];

		}
		public string Hash { get; set; }
		public string LocalRevisionNumber { get; set; }
	}

	public class Revision
	{
		public string Branch { get; set; }
		public RevisionNumber Number;
	}

	public class HgRepository
	{
		public string Identifier { get; set; }
		public HgModelVersionBranch BranchingHelper;
		public bool IsInitialized { get; set; }
		public IEnumerable<Revision> GetAllRevisions()
		{
			return new List<Revision>();
		}
		public Revision GetTip()
		{
			throw new NotImplementedException();
		}
		public bool MakeBundle(string[] baseRevisions, string filePath)
		{
			return false;
		}
		public bool Unbundle(string bundlePath)
		{
			return false;
		}
		public void Init()
		{
			IsInitialized = true;
		}
	}

	public class HgModelVersionBranch
	{
		internal IEnumerable<Revision> GetBranches()
		{
			return new List<Revision>();
		}
	}
}
