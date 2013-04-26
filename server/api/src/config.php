<?php

$rootPath = dirname(__FILE__);

define('SourcePath', $rootPath);

define("API_VERSION", 3);
define('CACHE_PATH', "/var/cache/WeShare");

$repoSearchPaths = array("/var/vcs/public", "/var/vcs/private");
//$repoSearchPaths = array("/var/vcs/public");

?>
