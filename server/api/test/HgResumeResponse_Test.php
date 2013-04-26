<?php

require_once(dirname(__FILE__) . '/testconfig.php');
require_once(SimpleTestPath . '/autorun.php');
require_once(SourcePath . "/WeShareResponse.php");

class TestOfWeShareResponse extends UnitTestCase {

	function testConstructor_DefaultParams_VersionIsNotEmpty() {
		$response = new WeShareResponse(WeShareResponse::SUCCESS);
		$this->assertNotNull($response->Version);
	}
}