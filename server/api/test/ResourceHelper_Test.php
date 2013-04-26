<?php

require_once(dirname(__FILE__) . '/testconfig.php');
require_once(SimpleTestPath . '/autorun.php');
require_once(SourcePath . "/ResourceHelper.php");

class TestOfResourceHelper extends UnitTestCase {

	function testcleanUp_BundleFileExists_DeletesBundleFile() {
		$bundle = new ResourceHelper(__FUNCTION__);
		$bundle->cleanUp();
		$bundleFilename = $bundle->getBundleFileName();
		file_put_contents($bundleFilename, "bundle data");
		$bundle->cleanUp();
		$this->assertFalse(is_file($bundleFilename));
	}

	function testConstructor_TransIdIsAlphaNumeric_NoException() {
		$bundle = new ResourceHelper("thisIsAlphaNumeric");
	}

	function testConstructor_TransIdCodeInjection_ThrowsException() {
		$this->expectException();
		$bundle = new ResourceHelper("id; echo \"bad script!\"");
	}

	function testGetOffset_Unset_ReturnsZero() {
		$transId = __FUNCTION__;
		$bundle = new ResourceHelper($transId);
		$bundle->cleanUp();
		$this->assertEqual(0, $bundle->getOffset());
	}

	function testSetGetOffset_SetThenGet_GetReturnsValueThatWasSet() {
		$transId = __FUNCTION__;
		$bundle = new ResourceHelper($transId);
		$bundle->cleanUp();
		$sow = 5023;
		$bundle->setOffset($sow);
		$this->assertEqual($sow, $bundle->getOffset());
	}
	
	function testGetState_GetReturnsDefault() {
		$transId = __FUNCTION__;
		$bundle = new ResourceHelper($transId);
		$this->assertEqual(ResourceHelper::State_Start, $bundle->getState());
		$bundle->cleanUp();
	}

	function testSetGetState_GetReturnsSet() {
		$transId = __FUNCTION__;
		$bundle = new ResourceHelper($transId);
		$bundle->setState(ResourceHelper::State_Downloading);
		$this->assertEqual(ResourceHelper::State_Downloading, $bundle->getState());
		$bundle->cleanUp();
	}

	function testSetGetHasProp_SetMultipleProps_GetPropsOkAndVerifyHasPropsOk() {
		$transId = __FUNCTION__;
		$bundle = new ResourceHelper($transId);
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
