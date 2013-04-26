<?php

require_once('src/config.php');
require_once('src/RestServer.php');
require_once('src/WeShareApi.php');

//$repoPath = sys_get_temp_dir() . "/WeShare_repoTestEnvironment";
$api = new WeShareAPI($repoSearchPaths);

// second param is debug mode
$restServer = new RestServer($api, false);

$restServer->url = $_SERVER['REQUEST_URI'];
$restServer->args = $_REQUEST;
$restServer->postData = file_get_contents("php://input");

$restServer->handle();

?>
