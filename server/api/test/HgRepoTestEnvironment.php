<?php

function recursiveDelete($str){
	if(is_file($str)){
		//print "deleting $str\n";
		return @unlink($str);
	}
	elseif (substr($str, -1, 1) == '.') {
		return;
	}
	elseif(is_dir($str)){
		$str = rtrim($str, '/');
		$pattern1 = $str . '/*';
		$pattern2 = $str . '/.*';
		$scan = glob("{" . "$pattern1,$pattern2" ."}", GLOB_BRACE);
		//print count($scan) . " items found to delete for $str:\n";
		//print_r($scan);
		foreach($scan as $index=>$path){
			recursiveDelete($path);
		}
		//print "deleting $str\n";
		return @rmdir($str);
	}
}

class HgRepoTestEnvironment {
	var $Path;
	var $BasePath;
	var $RepoId;

	function __construct() {
		$this->BasePath = sys_get_temp_dir() . "/WeShare_repoTestEnvironment";
		recursiveDelete($this->BasePath);
		if (!is_dir($this->BasePath)) {
			mkdir($this->BasePath);
		}
	}

	function dispose() {
		recursiveDelete($this->BasePath);
		$maintFile = SourcePath . "/maintenance_message.txt";
		if (file_exists($maintFile)) {
			unlink($maintFile);
		}
	}

	function makeRepo($zipfile) {
		$zip = new ZipArchive();
		$zip->open($zipfile);
		$this->RepoId = pathinfo($zipfile, PATHINFO_FILENAME);
		$this->Path = $this->BasePath . "/" . $this->RepoId;
		$zip->extractTo($this->Path);
		$zip->close();
	}
}

?>