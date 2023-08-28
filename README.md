# imagebackup
This is a tool I made to automatically make multiple backups of the images I take while on the road.

I developed it for a debian machine which can run headless, thus the actual library package names can vary.

It supports making bacups to (multiple) local harddisks, as well as (multiple) remote hosts, all via SSH/SCP (you will need to setup ssh keys for this to work), and running either manually, or probably more preferably, as a crontab.

It requires the following things:

For import you will require:
* Perl
  * libimage-exiftool-perl (Image::ExifTool)
  * libjson-parse-perl (JSON::Parse)
  * liblockfile-simple-perl (LockFile::Simple)
  * libnet-openssh-perl (for remote backup) (Net::OpenSSH)
  * libnet-telnet-perl (for remote backup) (Net::Telnet)
* ssh key auth (for remote backup)

The disks I've personally setup to automatically mount in ~/storage, with symlinks from imagebackup/storage/$diskname$ to ~/storage/$diskname$/storage, which lets me do a quick directory check for whether or not the disk is mounted before trying to copy the files.

The card readers I've personally setup to just be automatically mounted, and I modified the config entry to point to the directory they're automatically mounted to.

*** NOTE
Beware, I tried to do some locking logic, but it seems like it didn't work, so if the cron interval is too short, you might end up with duplicated files.
*** NOTE