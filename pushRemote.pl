#!/usr/bin/env perl
use strict;
use warnings;

use JSON::Parse 'json_file_to_perl';
use LockFile::Simple qw(lock trylock unlock);
use Net::OpenSSH;
use Net::Telnet;
use Time::Piece;

# Configuration
my $configFile = 'config.json';
die "Incorrect config file: $configFile\n" unless -e $configFile;
my $appConfig = json_file_to_perl ($configFile);

# Check if we have a connection before continuing

# Utils
my $lockmanager = LockFile::Simple->make(-hold => 3600, -warn => 0);

# Move the files from the spool directory to the various storage directories
$lockmanager->trylock("pushRemote") and do {
    foreach my $remoteConfig (@{$appConfig->{directories}->{remote}}) {
        if ($remoteConfig->{active}) {
            my $telnet = Net::Telnet->new(Timeout => 1, Port => $remoteConfig->{port});
            $telnet->errmode("return");

            my $port = $remoteConfig->{port} || "22";
            my $ssh = undef;
            my $remoteSpoolDirectory = $remoteConfig->{spool};

            next unless -d $remoteSpoolDirectory;

            my @files = readDir($remoteSpoolDirectory);
            @files and do {
                foreach my $dirEntry (@files) {
                    if (-f "$remoteSpoolDirectory/$dirEntry") {
                        # Check connectivity before each file, just in case it's changed
                        $telnet->open($remoteConfig->{host}) or do {
                            logger("Unable to reach $remoteConfig->{host}, no connectivity");
                            $ssh = undef;
                            last;
                        };

                        $ssh or do {
                            $ssh = Net::OpenSSH->new("$remoteConfig->{username}\@$remoteConfig->{host}:$port");
                        };

                        logger("$remoteSpoolDirectory/$dirEntry -> (transfer)");
                        $ssh->scp_put("$remoteSpoolDirectory/$dirEntry", "$remoteConfig->{directory}") or do {
                            $lockmanager->unlock("pushRemote");
                            logger("Problem copying file: " . $ssh->error, 1);
                        };

                        logger("$remoteSpoolDirectory/$dirEntry (remove)");
                        unlink("$remoteSpoolDirectory/$dirEntry");
                    }
                }
                logger("Batch done");
            };
        }
    };
    $lockmanager->unlock("pushRemote");
};

sub logger {
    my ($logstring, $die) = @_;

    my $dateTime = localtime->strftime("%Y-%m-%d %H:%M:%S");

    print "[$dateTime] [Remote] $logstring\n";
    die $logstring if $die;
}

sub readDir {
    my ($directoryToRead) = @_;

    opendir (my $dir, $directoryToRead);
    my @fromDirentries = grep(!/^\.+?/, readdir $dir);
    closedir $dir;

    return @fromDirentries;
}
