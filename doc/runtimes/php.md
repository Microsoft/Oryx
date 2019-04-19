This document describes how **PHP** apps are detected and built. It includes
details on components and configuration of build and run images too.

# Contents

1. [Base Image](#base-image)
    * [System packages](#system-packages)
1. [Detect](#detect)
1. [Build](#build)
    * [Package manager](#package-manager)
1. [Run](#run)
1. [Version support](#version-support)

# Base image

PHP runtime images are layered on Docker's [official PHP
images](https://github.com/docker-library/php).

## System packages

The Apache HTTP Server  is used as the application server.
The following PHP extensions are installed & enabled in the runtime images:

* gd
* imagick
* mysqli
* opcache
* odbc
* sqlsrv
* pdo
* pdo_sqlsrv
* pdo_mysql
* pdo_pgsql
* pgsql
* ldap
* intl
* gmp
* zip
* bcmath
* mbstring
* pcntl
* calendar
* exif
* gettext
* imap
* tidy
* shmop
* soap
* sockets
* sysvmsg
* sysvsem
* sysvshm
* pdo_odbc
* wddx
* xmlrpc
* xsl

# Detect

The PHP toolset is run when a `composer.json` file exists in the root of the repository.

# Build

The following process is applied for each build:

1. Run custom script if specified by `PRE_BUILD_SCRIPT_PATH`.
1. Run `php composer.phar install`.
1. Run custom script if specified by `POST_BUILD_SCRIPT_PATH`.

## Package manager

The latest version of *Composer* is used to install dependencies.

# Run

The following process is applied to determine how to start an app:

1. If user has specified a start script, run it.
1. Else, run `apache2-foreground`.

[Composer]: https://getcomposer.org/

# Version support

The PHP project defines this [release schedule][]. Oryx supports all actively supported
releases (7.2, 7.3), in addition to 5.6 & 7.0.

We will update the `patch` version of a release at least once every 3 months,
replacing the previous `patch` version for that release.

[release schedule]: https://www.php.net/supported-versions.php
