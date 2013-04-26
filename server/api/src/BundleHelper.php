<?php

class BundleHelper {
	
	const State_Start       = 'Start';
	const State_DownloadPre = 'DownloadPre';
	const State_Downloading = 'Downloading';
	const State_Uploading   = 'Uploading';
	const State_UploadPost  = 'UploadPost';
	
	private $_transactionId;
	private $_basePath;
	
	public function __construct($transactionId) {
		if(!BundleHelper::validateAlphaNumeric($transactionId)) {
			throw new ValidationException("transactionId $transactionId is not alpha numeric");
		}
		$this->_transactionId = $transactionId;
		$this->_basePath = CACHE_PATH;
	}

	private function getBundleDir() {
		$path = "{$this->_basePath}";
		if (!is_dir($path)) {
			if (!mkdir($path, 0755, true)) {
				throw new BundleHelperException("Failed to create repo dir: $path");
			}
		}
		return $path;
	}
	
	/**
	 * @return bool
	 */
	public function exists() {
		return file_exists($this->getBundleFileName());
	}

	/**
	 * Removes the bundle file and the meta file
	 */
	public function cleanUp() {
		if (file_exists($this->getBundleFileName())) {
			unlink($this->getBundleFileName());
		}
		if (file_exists($this->getMetaDataFileName())) {
			unlink($this->getMetaDataFileName());
		}
		return true;
	}
	
	// static helper functions
	/**
	 * Checks the hg output in $output. Returns true if error indicators are found.
	 * @param string $output
	 * @return bool
	 */
	public static function bundleOutputHasErrors($output) {
		if (strpos($output, "abort") !== false or
			strpos($output, "invalid") !== false or
			//strpos($data, "invalid") !== false or
			strpos($output, "exited with non-zero status 255") !== false) {
			return true;
		}
		return false;
	}
	
	
	
	
	public function getBundleBaseFilePath() {
		return $this->getBundleDir() . '/' .$this->_transactionId;
	}
	
	function getBundleFileName() {
		return $this->getBundleBaseFilePath() . '.bundle';
	}
	
	private function getMetaDataFileName() {
		return $this->getBundleBaseFilePath() . '.metadata';
	}

	static function validateAlphaNumeric($str) {
		// assert that the string contains only alphanumeric digits plus underscore
		if (preg_match('/^[a-zA-Z0-9_\-]+$/', $str) > 0) {
			return true;
		} else {
			return false;
		}
	}

	/**
	 * Get the start of window.
	 * @return int
	 */
	public function getOffset() {
		$metadata = $this->getMetadata();
		if (array_key_exists('offset', $metadata)) {
			return $metadata['offset'];
		} else {
			return 0;
		}
	}

	/**
	 * Set the start of window
	 * @param int $val
	 */
	public function setOffset($val) {
		$metadata = $this->getMetadata();
		$metadata['offset'] = intval($val);
		$this->setMetadata($metadata);
	}
	
	/**
	 * Gets the current state of this transaction
	 * @return string
	 */
	public function getState() {
		$result = $this->getProp('state');
		if (empty($result)) {
			$result = self::State_Start;
		}
		return $result;
	}

	/**
	 * Sets the current state of this transaction
	 * @param string $state
	 */
	public function setState($state) {
		$this->setProp('state', $state);
	}
	
	private function getMetadata() {
		$filename = $this->getMetaDataFileName();
		if (file_exists($filename)) {
			return unserialize(file_get_contents($filename));
		} else {
			return array();
		}
	}

	private function setMetadata($arr) {
		file_put_contents($this->getMetaDataFileName(), serialize($arr));
	}

	function getProp($key) {
		$metadata = $this->getMetadata();
		if (array_key_exists($key, $metadata)) {
			return $metadata[$key];
		} else {
			return "";
		}
	}

	function setProp($key, $value) {
		$metadata = $this->getMetadata();
		$metadata[$key] = $value;
		$this->setMetadata($metadata);
	}

	function hasProp($key) {
		$metadata = $this->getMetadata();
		return array_key_exists($key, $metadata);
	}
}

?>