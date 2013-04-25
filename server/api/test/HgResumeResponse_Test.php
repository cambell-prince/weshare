<?php

require_once(dirname(__FILE__) . '/testconfig.php');
require_once(SimpleTestPath . '/autorun.php');
require_once(SourcePath . "/HgResumeResponse.php");

class TestOfHgResumeResponse extends UnitTestCase {

	function testConstructor_DefaultParams_VersionIsNotEmpty() {
		$response = new HgResumeResponse(HgResumeResponse::SUCCESS);
		$this->assertNotNull($response->Version);
	}
}