# Photograpy Backup Assistant
This is a tool I made to automatically import and make multiple backups of the images I take while on the road.

I developed it for a debian machine which can run headless, either manually, but primarily as a daemon.

It supports:
* automatically importing files with a predefined set of extensions
* optionally renaming the imported files for uniqueness by prepending a date to the filename
* optionally exporting the files to locally attached harddisks
* optionally exporting the files to remote hosts via SCP (requires SSH keys to work)

It requires:
* dotnet 8.0

It uses the following nugets:
* MetadataExtractor 2.8.1
* Microsoft.Extensions.Hosting 8.0.0
* Microsoft.Extensions.Hosting.Abstractions 8.0.0
* SSH.NET 2024.1.0
* System.Text.Json 8.0.4 (pre-8.0.4 are vulnerable to a DoS, thus they're specifically imported)

The config file is automatically generated with default example values if it's missing, where the only functionality that's activated by default is the migration of the images from the default import directory into the imported directory. To activate the "export to HDs" and "transfer to remote hosts" functionality, change the active property to true.

The general path through the directory structure with all functionality activated is as follows:
* import/*.<predefined extensions> -> spool/import/incoming (copy, then delete when verified as successfully copied)

* spool/import/incoming -> spool/import/imported (move)
* spool/import/imported -> spool/external/incoming (copy, if any are defined and activated) 
* spool/import/imported -> spool/remote/incoming (copy, if any are defined and activated)
* spool/import/imported (delete if any external and/or remote are defined and active, untouched otherwise)

* spool/external/incoming -> spool/external/<drive1> (move when verified as copied successfully in previous step)
* spool/external/incoming -> spool/external/<drive2> (move when verified as copied successfully in previous step)
* spool/external/<drive1> -> storage/<drive1> (copy, then delete when verified as successfully copied)
* spool/external/<drive2> -> storage/<drive2> (copy, then delete when verified as successfully copied)

* spool/remote/incoming -> spool/remote/<host1> (move when verified as copied successfully in previous step)
* spool/remote/incoming -> spool/remote/<host2> (move when verified as copied successfully in previous step)
* spool/remote/<host1> -> <username>@<host1>/<directory> when SSH connection is established, delete after upload is done
* spool/remote/<host2> -> <username>@<host2>/<directory> when SSH connection is established, delete after upload is done
