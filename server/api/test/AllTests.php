<?php
require_once(dirname(__FILE__) . '/testconfig.php');
require_once(SimpleTestPath .  '/autorun.php');

class AllTests extends TestSuite {
	function __construct() {
		parent::__construct();
		$this->addFile(TestPath . '/AsyncRunner_Test.php');
		$this->addFile(TestPath . '/BundleHelper_Test.php');
		$this->addFile(TestPath . '/HgRunner_Test.php');
		$this->addFile(TestPath . '/WeShareApi_Test.php');
		$this->addFile(TestPath . '/WeShareResponse_Test.php');
	}
}

?>
