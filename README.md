# imagebackup
This is a tool I made to automatically make multiple backups of the images I take while on the road.

I developed it for a debian machine which can run headless, either manually, but primarily as a daemon.

It supports:
* automatically importing files with a predefined set of extensions
* optionally renaming the imported files for uniqueness by prepending a date to the filename
* optionally gzipping the files (I mainly do this to maintain file metadata)
* optionally exporting the files to locally attached harddisks
* optionally exporting the files to remote hosts via SCP (requires SSH keys to work)

The disks I've personally setup to automatically mount in ~/storage, with symlinks from imagebackup/storage/$diskname to ~/storage/$diskname$/storage, which lets me do a quick directory check for whether or not the disk is mounted before trying to copy the files.

The card readers I've personally setup to just be automatically mounted, and I modified the config entry to point to the directory they're automatically mounted to.

The config file is automatically generated with default example values if it's missing, and same goes for all mandatory import-related directories and whatever local/remote spool directories are defined and active.