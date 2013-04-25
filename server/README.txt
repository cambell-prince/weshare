Source code can be pulled from the Hg repository located at:
http://hg.palaso.org/hgresume

Installation
-----------------------
1) Clone the hgresume repo to the htdocs directory on the apache server where you will be installing the resumable API
e.g. the repo base folder would be /var/www/[site_name]/htdocs

2) Once you clone and update to the latest revision, you should have a folder structure like:
/var/www/[site_name]/htdocs/api

3) The api folder contains the PHP code for the API, while the contrib folder contains additional files needed for installation

4) The api/[version]/src/config.php file contains two variables that should be set to the directories that the API uses.
CACHE_PATH by default points to /var/cache/hgresume
REPOPATH by default points to /var/vcs/public

5) create the CACHE_PATH directory and change the ownership and permissions to www-data:www-data 750

6) verify that the REPOPATH directory is readable by www-data

7) the RedmineResumable.pm in the contrib folder can be referenced by the apache2 configuration in order to set up authentication.  An example of this is as follows:

In the <VirtualHost> element, have a PerlRequire directive that points to the RedmineResumable.pm in the contrib directory

Have a <Location /> element inside the <VirtualHost> element that specifies the PerlAccessHandler and PerlAuthenHandler like this:

PerlAccessHandler Apache::Authn::RedmineResumable::access_handler
PerlAuthenHandler Apache::Authn::RedmineResumable::authen_handler

PerlSetVar dsn "DBI:mysql:database=blahblahblah"
PerlSetVar db_user username
PerlSetVar db_pass password

Require valid-user








Maintenance Mode
---------------------
If there is ever a reason to shutdown the API temporarily, an admin or a server process can place a text file named maintenance_message.txt in the src directory with an appropriate explanation of why the API has been suspended.  All clients connecting to the API will receive the maintenance message.
