<?php

require_once('AsyncRunner.php');

class ResourceBundler {
	var $repoPath;
	var $logState;
	const DEFAULT_HG = "/var/vcs/public";

	function __construct($repoPath = DEFAULT_HG) {
		if (is_dir($repoPath)) {
			$this->repoPath = $repoPath;
		} else {
			throw new ValidationException("repo '$repoPath' doesn't exist!");
		}
		$logState = false;
	}

	private function logEvent($message) {
		$logFilename = $this->repoPath . "/ResourceBundler.log";
		$timestamp = date("YMd \TH.i");
		if (!$this->logState) {
			$this->logState = true;
			file_put_contents($logFilename, "\n$timestamp\n", FILE_APPEND | LOCK_EX);
		}
		file_put_contents($logFilename, "$message\n", FILE_APPEND | LOCK_EX);
	}

	function unbundle($filepath, $asyncRunner) {
		if (!is_file($filepath)) {
			throw new HgException("bundle file '$filepath' is not a file!");
		}
		chdir($this->repoPath); // NOTE: I tried with -R and it didn't work for me. CP 2012-06
		
		// run hg incoming to make sure this bundle is related to the repo
		$cmd = "hg incoming $filepath";
		$this->logEvent("cmd: $cmd");
		$asyncRunner->run($cmd);
		$asyncRunner->synchronize();
		$output = $asyncRunner->getOutput();
		if (preg_match('/abort:.*unknown parent/', $output)) {
			throw new UnrelatedRepoException("Project is unrelated!  (unrelated bundle pushed to repo)");
		}
		if (preg_match('/parent:\s*-1:/', $output)) {
			throw new UnrelatedRepoException("Project is unrelated!  (unrelated bundle pushed to repo)");
		}
		if (preg_match('/abort:.*not a Mercurial bundle/', $output)) {
			throw new Exception("Project cannot be updated!  (corrupt bundle pushed to repo)");
		}
		
		$cmd = "hg unbundle $filepath";
		$this->logEvent("cmd: $cmd");
		$asyncRunner->run($cmd);
	}

	function update($asyncRunner, $revision = "") {
		chdir($this->repoPath);
		$cmd = "hg update $revision";
		$this->logEvent("cmd: $cmd");
		$asyncRunner->run($cmd);
	}

	/**
	 * @param string $baseHashes[] expects the data to be just the "hash" and NOT the branch information preceded by a colon
	 * @param string $bundleFilePath
	 * @param AsyncRunner $asyncRunner
	 */
	function makeBundle($baseHashes, $bundleFilePath, $asyncRunner) {
		chdir($this->repoPath); // NOTE: I tried with -R and it didn't work for me. CP 2012-06
		if (count($baseHashes) == 1 && $baseHashes[0] == "0") {
			$cmd = "hg bundle --all $bundleFilePath";
		} else {
			$cmd = "hg bundle ";
			$baseHashes = (array)$baseHashes; //I can't figure out why this is sometimes not an array
			foreach($baseHashes as $hash)
			{
				$cmd .= "--base $hash ";
			}
			$cmd .= $bundleFilePath;
		}
		$asyncRunner->run($cmd);
	}
	
	/**
	 * helper function, mostly for tests
	 * @param string $baseHashes[]
	 * @param string $bundleFilePath
	 * @param string $asyncRunner
	 */
	function makeBundleAndWaitUntilFinished($baseHashes, $bundleFilePath, $asyncRunner) {
		$this->makeBundle($baseHashes, $bundleFilePath, $asyncRunner);
		$asyncRunner->synchronize();
	}
	
	/**
	 * 
	 * @return string a baseHash (without branch information)
	 */
	
	function getTip() {
		$revisionArray = $this->getRevisions(0, 1);
		return substr($revisionArray[0], 0, strpos($revisionArray[0], ':'));
	}

	function getBranchTips() {
		chdir($this->repoPath);
		exec('hg branches', $output, $returnval);
		$revisionArray = array();
		foreach($output as $branch) {
			if($branch == '') {
				$branchName = 'default';
			} else {	
				$branchName = substr($branch, 0, strpos($branch, ' '));
			}
			//append the first revision for the branch to the array
			$revisionArray = array_merge($revisionArray, $this->getRevisionsInternal(0, 1, $branchName));
		}
		$revisions = array();
		foreach($revisionArray as $hashandbranch) {
			$revisions[] = substr($hashandbranch, 0, strpos($hashandbranch, ":"));
		}
		return $revisions;
	}

	//this method will return an array containing revision hash branch pairs e.g. 'fb7a8f23394d:default'
	function getRevisions($offset, $quantity) {
		return $this->getRevisionsInternal($offset, $quantity, NULL);
	}
	
	//this method will return an array containing revision hash branch pairs e.g. 'fb7a8f23394d:default'
	function getRevisionsInternal($offset, $quantity, $branch) {
		if ($quantity < 1) {
			throw new ValidationException("quantity parameter much be larger than 0");
		}
		chdir($this->repoPath);
		// I believe ':' is illegal in branch names, it is in tag, so we will use that to split the hash and branch
		$cmd = 'hg log';
		if(!is_null($branch)) {
			$cmd .= ' -b ' . $branch; 
		}
		$cmd .= ' --template "{node|short}:{branch}\n"';
		exec($cmd, $output, $returnval);
		if (count($output) == 0) {
			exec('hg tip --template "{rev}:{branch}\n"', $output2, $returnval);
			
			if (count($output2) == 1 and strpos($output2[0], "-1") === 0) { // starts with -1
				//in the case of a tip result like '-1:default' we will return '0:default' to signal the empty repo 
				$output2[0] = preg_replace('/^-1/', '0', $output2[0]);
				return $output2; //
			}
			throw new HgException("command '$cmd' failed!\n");
		}
		return array_slice($output, $offset, $quantity);
	}

	function isValidBase($hashes) {
		if (!is_array($hashes)) {
			$hashes = array($hashes);
		}	
		if (count($hashes) == 1 && $hashes[0] == "0") { // special case indicating revision 0
			return true;
		}
		$foundHash = 0;
		$q = 200;
		$i = 0;
		while($foundHash < count($hashes)) {
			$revisions = $this->getRevisions($i, $q);
			if (count($revisions) == 0) {
				return false;
			}
			foreach($revisions as $hashandbranch) {
				$rev = substr($hashandbranch, 0, strpos($hashandbranch, ":"));
				if (array_search($rev, $hashes, false) !== false) {
					$foundHash++;
					if($foundHash >= count($hashes)) {
						break;
					}
				}
			}
			$i += $q;
		}
		return true;
	}

	// helper function used in tests
	function addAndCheckInFile($filePath) {
		chdir($this->repoPath);
		$cmd = "hg add $filePath";
		exec(escapeshellcmd($cmd) . " 2> /dev/null", $output, $returnval);
		if ($returnval != 0) {
			throw new HgException("command '$cmd' failed!\n");
		}
		$cmd = "hg commit -u 'system' -m 'added file $filePath'";
		exec(escapeshellcmd($cmd) . " 2> /dev/null", $output, $returnval);
		if ($returnval != 0) {
			throw new HgException("command '$cmd' failed!\n");
		}
	}
}

?>