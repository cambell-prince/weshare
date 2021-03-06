<?php

require_once(dirname(__FILE__) . '/testconfig.php');
require_once(SimpleTestPath . '/autorun.php');
require_once(SourcePath . "/BundleHelper.php");

class TestOfBundleHelper extends UnitTestCase {

	function testcleanUp_BundleFileExists_DeletesBundleFile() {
		$bundle = new BundleHelper(__FUNCTION__);
		$bundle->cleanUp();
		$bundleFilename = $bundle->getBundleFileName();
		file_put_contents($bundleFilename, "bundle data");
		$bundle->cleanUp();
		$this->assertFalse(is_file($bundleFilename));
	}

	function testConstructor_TransIdIsAlphaNumeric_NoException() {
		$bundle = new BundleHelper("thisIsAlphaNumeric");
	}

	function testConstructor_TransIdCodeInjection_ThrowsException() {
		$this->expectException();
		$bundle = new BundleHelper("id; echo \"bad script!\"");
	}

	function testGetOffset_Unset_ReturnsZero() {
		$transId = __FUNCTION__;
		$bundle = new BundleHelper($transId);
		$bundle->cleanUp();
		$this->assertEqual(0, $bundle->getOffset());
	}

	function testSetGetOffset_SetThenGet_GetReturnsValueThatWasSet() {
		$transId = __FUNCTION__;
		$bundle = new BundleHelper($transId);
		$bundle->cleanUp();
		$sow = 5023;
		$bundle->setOffset($sow);
		$this->assertEqual($sow, $bundle->getOffset());
	}
	
	function testGetState_GetReturnsDefault() {
		$transId = __FUNCTION__;
		$bundle = new BundleHelper($transId);
		$this->assertEqual(BundleHelper::State_Start, $bundle->getState());
		$bundle->cleanUp();
	}

	function testSetGetState_GetReturnsSet() {
		$transId = __FUNCTION__;
		$bundle = new BundleHelper($transId);
		$bundle->setState(BundleHelper::State_Downloading);
		$this->assertEqual(BundleHelper::State_Downloading, $bundle->getState());
		$bundle->cleanUp();
	}

	function testSetGetHasProp_SetMultipleProps_GetPropsOkAndVerifyHasPropsOk() {
		$transId = __FUNCTION__;
		$bundle = new BundleHelper($transId);
		$bundle->cleanUp();
		$this->assertFalse($bundle->hasProp("tip"));
		$bundle->setProp("tip", "7890");
		$this->assertTrue($bundle->hasProp("tip"));
		$bundle->setProp("repoId", "myRepo");
		$this->assertEqual("7890", $bundle->getProp("tip"));
		$this->assertEqual("myRepo", $bundle->getProp("repoId"));
	}
}

?>
