<?php

require_once('HgExceptions.php');

class AsyncRunner
{
	private $_baseFilePath;
	
	public function __construct($baseFilePath) {
		$this->_baseFilePath = $baseFilePath;
	}
	
	/**
	 * @param string $command The unescaped system command to run
	 */
	public function run($command) {
		$lockFilePath = $this->getLockFilePath();
		$command = escapeshellcmd($command);
		// The following command redirects all output (include output of the time command) to $finishFilename
		// The trailing ampersand makes the command run in the background
		// We touch the $finishFilename before execution to indicate that the command has started execution
		$command = "touch $lockFilePath; /usr/bin/time --append --output=$lockFilePath --format=\"AsyncCompleted: %E\" $command > $lockFilePath 2>&1 &";
		exec($command);
	}

	/**
	 * @return bool
	 */
	public function isRunning() {
		return file_exists($this->getLockFilePath());
	}
	
	/**
	 * @return bool
	 */
	public function isComplete() {
		$lockFilePath = $this->getLockFilePath();
		if (!file_exists($lockFilePath)) {
			throw new AsyncRunnerException("Lock file '$lockFilePath' not found, process is not running");
		}
		$data = file_get_contents($this->getLockFilePath());
		if (strpos($data, "AsyncCompleted") !== false) {
			return true;
		}
		return false;
	}
	
	public function getOutput() {
		if (!$this->isComplete()) {
			throw new AsyncRunnerException("Command on '$this->_baseFilePath' not yet complete.");
		}
		return file_get_contents($this->getLockFilePath());
	}
	
	public function cleanUp() {
		if (file_exists($this->getLockFilePath())) {
			unlink($this->getLockFilePath());
		}
	}
	
	private function getLockFilePath() {
		return $this->_baseFilePath . '.isFinished';
	}
	
	public function synchronize() {
		for ($i = 0; $i < 200; $i++) {
			if ($this->isComplete()) {
				return;
			}
			usleep(500000);
		}
		throw new AsyncRunnerException("Error: Long running process exceeded 100 seconds while waiting to synchronize");
	}
}

?>