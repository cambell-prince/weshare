<?php

require_once("WeShareResponse.php");
require_once("ResourceBundler.php");
require_once("BundleHelper.php");

class WeShareAPI {
	var $RepoBasePaths;

	// Note: API_VERSION is defined in config.php

	function __construct($searchPaths) {
		// $searchPaths is an array of paths
		if (is_array($searchPaths)) {
			$this->RepoBasePaths = $searchPaths;
		}
		else {
			$this->RepoBasePaths = array($searchPaths);
		}
	}

	/**
	 * Pushes a chunk of binary $data at $offset in the stream for $transId of $resourceId
	 * @param string $resourceId
	 * @param int $totalSize
	 * @param int $offset
	 * @param byte[] $data
	 * @param string $transactionId
	 * @return WeShareResponse
	 */
	function pushBundleChunk($resourceId, $bundleSize, $offset, $data, $transactionId) {
		$availability = $this->isAvailable();
		if ($availability->Code == WeShareResponse::NOTAVAILABLE) {
			return $availability;
		}

		// ------------------
		// Check the input parameters
		// ------------------
		// $resouceId
		$resourcePath = $this->getResourcePath($resourceId);
		if (!$resourcePath) {
			return new WeShareResponse(WeShareResponse::UNKNOWNID);
		}
		// $offset
		if ($offset < 0 or $offset >= $bundleSize) {
			return new WeShareResponse(WeShareResponse::FAIL, array('Error' => 'invalid offset'));
		}
		// $data
		$dataSize = mb_strlen($data, "8bit");
		if ($dataSize == 0) {
			return new WeShareResponse(WeShareResponse::FAIL, array('Error' => 'no data sent'));
		}
		if ($dataSize > $bundleSize - $offset) {
			return new WeShareResponse(WeShareResponse::FAIL, array('Error' => 'data sent is larger than remaining total transmit size'));
		}
		// $bundleSize
		if (intval($bundleSize) < 0) {
			return new WeShareResponse(WeShareResponse::FAIL, array('Error' => 'negative transmit size'));
		}

		// ------------------
		// Good to go ...
		// ------------------
		
		$bundleHelper = new BundleHelper($transId);
		switch ($bundleHelper->getState()) {
			case BundleHelper::State_Start:
				$bundleHelper->setState(BundleHelper::State_Uploading);
				// Fall through to State_Uploading
			case BundleHelper::State_Uploading:
				// if the data sent falls before the start of window, mark it as received and reply with correct startOfWindow
				// Fail if there is overlap or a mismatch between the start of window and the data offset
				$startOfWindow = $bundleHelper->getOffset();
				if ($offset != $startOfWindow) { // these are usually equal.  It could be a client programming error if they are not
					if ($offset < $startOfWindow) {
						return new WeShareResponse(WeShareResponse::RECEIVED, array('sow' => $startOfWindow, 'Note' => 'server received duplicate data'));
					} else {
						return new WeShareResponse(WeShareResponse::FAIL, array('sow' => $startOfWindow, 'Error' => "data sent ($dataSize) with offset ($offset) falls after server's start of window ($startOfWindow)"));
					}
				}
				// write chunk data to bundle file
				$bundleFile = fopen($bundle->getBundleFileName(), "a");
				fseek($bundleFile, $offset);
				fwrite($bundleFile, $data);
				fclose($bundleFile);
		
				$newSow = $offset + $dataSize;
				$bundle->setOffset($newSow);
				
				// for the final chunk; assemble the bundle and apply the bundle
				if ($newSow == $bundleSize) {
					$bundle->setState(BundleHelper::State_UploadPost);
					try {  // REVIEW Would be nice if the try / catch logic was universal. ie one policy for the api function. CP 2012-06
						$bundleFilePath = $bundle->getBundleFileName();
						$asyncRunner = new AsyncRunner($bundleFilePath);
						$hg->unbundle($bundleFilePath, $asyncRunner);
						for ($i = 0; $i < 4; $i++) {
							if ($asyncRunner->isComplete()) {
								if (BundleHelper::bundleOutputHasErrors($asyncRunner->getOutput())) {
									$responseValues = array('transId' => $transId);
									return new WeShareResponse(WeShareResponse::RESET, $responseValues);
								}
								$bundle->cleanUp();
								$asyncRunner->cleanUp();
								$responseValues = array('transId' => $transId);
								return new WeShareResponse(WeShareResponse::SUCCESS, $responseValues);
							}
							sleep(1);
						}
						$responseValues = array('transId' => $transId, 'sow' => $newSow);
						return new WeShareResponse(WeShareResponse::RECEIVED, $responseValues);
						// REVIEW Not sure what returning 'RECEIVED' will do to the client here, we've got all the data but need to wait for the unbundle to finish before sending success
					} catch (UnrelatedRepoException $e) {
						$bundle->setOffset(0);
						$responseValues = array('Error' => substr($e->getMessage(), 0, 1000));
						$responseValues['transId'] = $transId;
						return new WeShareResponse(WeShareResponse::FAIL, $responseValues);
					} catch (Exception $e) {
						// REVIEW The RESET response may not make sense in this context anymore.  Why would we want to tell the client to resend a bundle if it failed the first time?  My guess is never.  cjh 2013-03
						//echo $e->getMessage(); // FIXME
						$bundle->setOffset(0);
						$responseValues = array('Error' => substr($e->getMessage(), 0, 1000));
						$responseValues['transId'] = $transId;
						return new WeShareResponse(WeShareResponse::RESET, $responseValues);
					}
				} else {
					// received the chunk, but it's not the last one; we expect more chunks
					$responseValues = array('transId' => $transId, 'sow' => $newSow);
					return new WeShareResponse(WeShareResponse::RECEIVED, $responseValues);
				}
				break;
			case BundleHelper::State_UploadPost:
				$bundleFilePath = $bundle->getBundleFileName();
				$asyncRunner = new AsyncRunner($bundleFilePath);
				if ($asyncRunner->isComplete()) {
					if (BundleHelper::bundleOutputHasErrors($asyncRunner->getOutput())) {
						$responseValues = array('transId' => $transId);
						// REVIEW The RESET response may not make sense in this context anymore.  Why would we want to tell the client to resend a bundle if it failed the first time?  My guess is never.  cjh 2013-03
						return new WeShareResponse(WeShareResponse::RESET, $responseValues);
					}
					$bundle->cleanUp();
					$asyncRunner->cleanUp();
					$responseValues = array('transId' => $transId);
					return new WeShareResponse(WeShareResponse::SUCCESS, $responseValues);
				} else {
					$responseValues = array('transId' => $transId, 'sow' => $newSow);
					return new WeShareResponse(WeShareResponse::RECEIVED, $responseValues);
				}
				break;
		}
	}

	/**
	 * 
	 * Requests to pull a chunk of $chunkSize bytes at $offset in the bundle from $repoId using the 
	 * $transId to identify this transaction.
	 * @param string $repoId
	 * @param array[string] $baseHashes
	 * @param int $offset
	 * @param int $chunkSize
	 * @param string $transId
	 * @return WeShareResponse
	 */
	function pullBundleChunk($repoId, $baseHashes, $offset, $chunkSize, $transId) {
		return $this->pullBundleChunkInternal($repoId, $baseHashes, $offset, $chunkSize, $transId, false);
	}

	/**
	 * 
	 * @param string $repoId
	 * @param array[string] $baseHashes expects just hashes, NOT hashes with a branch name appended
	 * @param int $offset
	 * @param int $chunkSize
	 * @param string $transId
	 * @param bool $waitForBundleToFinish
	 * @throws Exception
	 * @return WeShareResponse
	 */
	function pullBundleChunkInternal($repoId, $baseHashes, $offset, $chunkSize, $transId, $waitForBundleToFinish) {
		try {
			if (!is_array($baseHashes)) {
				$baseHashes = array($baseHashes);
			}
			$availability = $this->isAvailable();
			if ($availability->Code == WeShareResponse::NOTAVAILABLE) {
				return $availability;
			}
	
			// ------------------
			// Check the input parameters
			// ------------------
			// $repoId
			$repoPath = $this->getRepoPath($repoId);
			if (!$repoPath) {
				return new WeShareResponse(WeShareResponse::UNKNOWNID);
			}
			// $offset
			if ($offset < 0) {
				return new WeShareResponse(WeShareResponse::FAIL, array('Error' => 'invalid offset'));
			}
			
			$hg = new ResourceBundler($repoPath); // REVIEW The hg based checks only need to be done once per transaction. Would be nice to move them inside the state switch CP 2012-06
			// $basehashes
			// TODO This might be bogus, the given baseHash may well be a baseHash that exists in a future push, and we don't have it right now. CP 2012-08
			if (!$hg->isValidBase($baseHashes)) {
				return new WeShareResponse(WeShareResponse::FAIL, array('Error' => 'invalid baseHash'));
			}

			// ------------------
			// Good to go ...
			// ------------------
			// If every requested baseHash is a branch tip then no pull is necessary
			sort($baseHashes);
			$branchTips = $hg->getBranchTips();
			sort($branchTips);
			if (count($baseHashes) == count($branchTips)) {
				$areEqual = true;
				for ($i = 0; $i < count($baseHashes); $i++) {
					if ($baseHashes[$i] != $branchTips[$i]) {
						$areEqual = false;
					}
				}
				if ($areEqual) {
					return new WeShareResponse(WeShareResponse::NOCHANGE);
				}
			}
			
			$bundle = new BundleHelper($transId);
			$asyncRunner = new AsyncRunner($bundle->getBundleBaseFilePath());

			$bundleCreatedInThisExecution = false;
			$bundleFilename = $bundle->getBundleFileName();
			
			if (!$bundle->exists()) {
				// if the client requests an offset greater than 0, but the bundle needed to be created on this request,
				// send the RESET response since the server's bundle cache has aparently expired.
				if ($offset > 0) {
					return new WeShareResponse(WeShareResponse::RESET); // TODO Add in error information in here saying that the bundle isn't present CP 2012-06
				}
				// At this point we can presume that $offset == 0 so this is the first pull request; make a new bundle
				if ($waitForBundleToFinish) {
					$hg->makeBundleAndWaitUntilFinished($baseHashes, $bundleFilename, $asyncRunner);
				} else {
					$hg->makeBundle($baseHashes, $bundleFilename, $asyncRunner);
				}
				$bundle->setProp("tip", $hg->getTip());
				$bundle->setProp("repoId", $repoId);
				$bundle->setState(BundleHelper::State_DownloadPre);
			}
			
			$response = new WeShareResponse(WeShareResponse::SUCCESS);
			switch ($bundle->getState()) {
				case BundleHelper::State_DownloadPre:
					if ($asyncRunner->isComplete()) {
						if (BundleHelper::bundleOutputHasErrors($asyncRunner->getOutput())) {
							$response = new WeShareResponse(WeShareResponse::FAIL);
							$response->Values = array('Error' => substr(file_get_contents($bundleTimeFile), 0, 1000));
							return $response;
						}
						$bundle->setState(BundleHelper::State_Downloading);
					}
					// TODO turn this into a for loop $i = 0, 1 CP 2012-06
					clearstatcache(); // clear filesize() cache so that we can get accurate answers to the filesize() function
					if ($this->canGetChunkBelowBundleSize($bundleFilename, $chunkSize, $offset)) {
						$data = $this->getChunk($bundleFilename, $chunkSize, $offset);
						$response->Values = array(
								'bundleSize' => filesize($bundleFilename),
								'chunkSize' => mb_strlen($data, "8bit"),
								'transId' => $transId);
						$response->Content = $data;
					} else {
						sleep(4);
						clearstatcache(); // clear filesize() cache
						// try a second time to get a chunk below bundle size
						if ($this->canGetChunkBelowBundleSize($bundleFilename, $chunkSize, $offset)) {
							$data = $this->getChunk($bundleFilename, $chunkSize, $offset);
							$response->Values = array(
									'bundleSize' => filesize($bundleFilename),
									'chunkSize' => mb_strlen($data, "8bit"),
									'transId' => $transId);
							$response->Content = $data;
						} else {
							$response = new WeShareResponse(WeShareResponse::INPROGRESS);
						}
					}
					break;
				case BundleHelper::State_Downloading:
					$data = $this->getChunk($bundleFilename, $chunkSize, $offset);
					$response->Values = array(
							'bundleSize' => filesize($bundleFilename),
							'chunkSize' => mb_strlen($data, "8bit"),
							'transId' => $transId);
					$response->Content = $data;
					if ($offset > filesize($bundleFilename)) {
						throw new ValidationException("offset $offset is greater than or equal to bundleSize " . filesize($bundleFilename));
					}
					break;
			}
			
			return $response;
			
		} catch (Exception $e) {
			$response = array('Error' => substr($e->getMessage(), 0, 1000));
			return new WeShareResponse(WeShareResponse::FAIL, $response);
		}
	}
	
	private static function canGetChunkBelowBundleSize($filename, $chunkSize, $offset) {
		if (is_file($filename) and $offset + $chunkSize < filesize($filename)) {
			return true;
		}
		return false;
	}
		
	private static function getChunk($filename, $chunkSize, $offset) {
		$data = "";
		if ($offset < filesize($filename)) {
			// read the specified chunk of the bundle file
			$fileHandle = fopen($filename, "r");
			fseek($fileHandle, $offset);
			$data = fread($fileHandle, $chunkSize); //fread can handle if there's less than $chunkSize data left to read
			fclose($fileHandle);
		}
		return $data;
	}


	function getRevisions($repoId, $offset, $quantity) {
		$availability = $this->isAvailable();
		if ($availability->Code == WeShareResponse::NOTAVAILABLE) {
			return $availability;
		}
		try {
			$repoPath = $this->getRepoPath($repoId);
			if ($repoPath) {
				$hg = new ResourceBundler($repoPath);
				$revisionList = $hg->getRevisions($offset, $quantity);
				$hgresponse = new WeShareResponse(WeShareResponse::SUCCESS, array(), implode("|",$revisionList));
			}
			else {
				$hgresponse = new WeShareResponse(WeShareResponse::UNKNOWNID);
			}
		} catch (Exception $e) {
			$response = array('Error' => substr($e->getMessage(), 0, 1000));
			$hgresponse = new WeShareResponse(WeShareResponse::FAIL, $response);
		}
		return $hgresponse;
	}

	function finishPushBundle($transId) {
		$bundle = new BundleHelper($transId);
		if ($bundle->cleanUp()) {
			return new WeShareResponse(WeShareResponse::SUCCESS);
		} else {
			return new WeShareResponse(WeShareResponse::FAIL);
		}
	}

	function finishPullBundle($transId) {
		$bundle = new BundleHelper($transId);
		if ($bundle->hasProp("tip") and $bundle->hasProp("repoId")) {
			$repoPath = $this->getRepoPath($bundle->getProp("repoId"));
			if (is_dir($repoPath)) { // a redundant check (sort of) to prevent tests from throwing that recycle the same transid
				$hg = new ResourceBundler($repoPath);
				// check that the repo has not been updated, since a pull was started
				if ($bundle->getProp("tip") != $hg->getTip()) {
					$bundle->cleanUp();
					return new WeShareResponse(WeShareResponse::RESET);
				}
			}
		}
		if ($bundle->cleanUp()) {
			return new WeShareResponse(WeShareResponse::SUCCESS);
		}
		return new WeShareResponse(WeShareResponse::FAIL);
	}

	function isAvailable() {
		if ($this->isAvailableAsBool()) {
			return new WeShareResponse(WeShareResponse::SUCCESS);
		}
		$message = file_get_contents($this->getMaintenanceFilePath());
		return new WeShareResponse(WeShareResponse::NOTAVAILABLE, array(), $message);
	}

	private function isAvailableAsBool() {
		$file = $this->getMaintenanceFilePath();
		if (file_exists($file) && filesize($file) > 0) {
			return false;
		}
		return true;
	}
	
	private function getMaintenanceFilePath() {
		return SourcePath . "/maintenance_message.txt";
	}
	
	private function getRepoPath($repoId) {
		if ($repoId) {
			foreach ($this->RepoBasePaths as $basePath) {
				$possibleRepoPath = "$basePath/$repoId";
				if (is_dir($possibleRepoPath)) {
					return $possibleRepoPath;
				}
			}
		}
		return "";
	}
}

?>
