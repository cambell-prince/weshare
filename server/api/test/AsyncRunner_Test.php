<?php

require_once(dirname(__FILE__) . '/testconfig.php');
require_once(SimpleTestPath . '/autorun.php');
require_once(SourcePath . "/AsyncRunner.php");

class TestOfAsyncRunner extends UnitTestCase {

	function testRunIsComplete_FalseThenTrue() {
		$runner = new AsyncRunner('/tmp/testFile');
		$runner->run('echo foo');
		$this->assertFalse($runner->isComplete());
		$runner->synchronize();
		$this->assertTrue($runner->isComplete());
	}

	function testIsComplete_WithNoRun_Throws() {
		$runner = new AsyncRunner('/tmp/testFile');
		$runner->cleanUp();
		$this->expectException();
		$this->assertFalse($runner->isComplete());
	}

	function testIsRunning_FalseThenTrue() {
		$runner = new AsyncRunner('/tmp/testFile');
		$runner->cleanUp();
		$this->assertFalse($runner->isRunning());
		$runner->run('echo foo');
		$this->assertTrue($runner->isRunning());
	}

	function testCleanUp_FileRemoved() {
		$runner = new AsyncRunner('/tmp/testFile');
		$runner->run('echo foo');
		$this->assertTrue(file_exists('/tmp/testFile.isFinished'));
		$runner->cleanUp();
		$this->assertFalse(file_exists('/tmp/testFile.isFinished'));
	}
	
	function testGetOutput_NotComplete_Throws() {
		$this->expectException();
		$runner = new AsyncRunner('/tmp/testFile');
		$runner->run('echo abort');
		$runner->getOutput();
	}
	
	function testGetOutput_Complete_ReturnsOutput() {
		$runner = new AsyncRunner('/tmp/testFile');
		$runner->run('echo abort');
		$runner->synchronize();
		$data = $runner->getOutput();
		$this->assertPattern('/abort/', $data);
	}
	
}

?>
