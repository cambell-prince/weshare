<?php

class Repository
{
	public $BasePath;
	
	public function __construct($repositoryBasePath) {
		$this->BasePath = $repositoryBasePath;
		// TODO Canonicalize this with trailing '/' CO 2013-04	
	}
	
	public function repositoryInfo() {
		$results = array();
		$this->repositoryFolderInfo($this->BasePath, $results);
		return $results;
	}
	
	private function repositoryFolderInfo($path, $results) {
		$prefix = $path . '/';
		$dir = dir($path);
		while (false !== ($file = $dir->read())){
			if ($file === '.' || $file === '..') continue;
			$file = $prefix . $file;
			if (is_dir($file)) {
				findFiles($file, $results);
			} else {
				$resourceInfo = $this->resouceInfo($file); // TODO suspect this needs to be the cumulative path, not just $file CP 2013-04
				$results[] = $resourceInfo;
			}
		}
	}
	
	private function resourceInfo($resourcePartialFilePath) {
		$resourceFilePath = $this->BasePath . $resourcePartialFilePath; 
		$md5 = md5_file($resourceFilePath);
		$fileInfo = stat($resourceFilePath);
		$time = $fileInfo['mtime']; // TODO Probably need to iso time this. CP 2013-04
		return array('path' => $resourcePartialFilePath, 'md5' => $md5, 'time' => $time);
	}
	
}