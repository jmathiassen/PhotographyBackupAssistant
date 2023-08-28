#!/usr/bin/env perl
use strict;
use warnings;

use File::Basename;
use File::Compare;
use File::Copy;
use JSON::Parse 'json_file_to_perl';
use LockFile::Simple qw(lock trylock unlock);
use Time::Piece;

# Configuration
my $configFile = 'config.json';
die "Incorrect config file: $configFile\n" unless -e $configFile;
my $appConfig = json_file_to_perl ($configFile);

# Utils
my $lockmanager = LockFile::Simple->make(-hold => 600, -warn => 0);

# Directories
my $localDirectories  = $appConfig->{directories}->{local};

# Move the files from the spool directory to the various storage directories
$lockmanager->trylock("pushLocal") and do {
    foreach my $localDirectory (@$localDirectories) {
        if ($localDirectory->{active}) {
            my @files = readDir($localDirectory->{spool});
            @files and do {
                if (!-d $localDirectory->{spool}) {
                    logger("$localDirectory->{spool} is missing");
                    next;
                }
                if (!-d $localDirectory->{storage}) {
                    logger("$localDirectory->{storage} is missing");
                    next;
                }
                foreach my $dirEntry (@files) {
                    if (copyFile($localDirectory->{spool}, $localDirectory->{storage}, $dirEntry, $dirEntry)) {
                        logger("$localDirectory->{spool}/$dirEntry (remove)");
                        unlink("$localDirectory->{spool}/$dirEntry");
                    } else {
                        logger("$localDirectory->{spool}/$dirEntry -> $localDirectory->{storage}/$dirEntry (failed)");
                    }
                }
                logger("Batch done");
            };
        }
    }
    $lockmanager->unlock("pushLocal");
};

sub logger {
    my ($logstring, $die) = @_;

    my $dateTime = localtime->strftime("%Y-%m-%d %H:%M:%S");

    print "[$dateTime] [Local] $logstring\n";
    die $logstring if $die;
}

sub copyFile {
    my ($directoryFrom, $directoryTo, $filenameFrom, $filenameTo) = @_;

    if (-f "$directoryTo/$filenameTo") {
        if (compare("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo")) {
            $filenameTo = resolveFilenameCollision($directoryTo, $filenameFrom);
            logger("$directoryFrom/$filenameFrom -> $directoryTo/$filenameTo (copy with collision resolve)");
            copy("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo") or do {
                $lockmanager->unlock("pushLocal");
                logger "problem copying file $directoryFrom/$filenameFrom -> $directoryTo/$filenameTo", 1;
            };
            fileDateTransfer("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo");
        } else {
            logger("$directoryFrom/$filenameFrom -> $directoryTo/$filenameTo (no operation)");
        }
    } else {
        logger("$directoryFrom/$filenameFrom -> $directoryTo/$filenameTo (copy)");
        copy("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo") or do {
            $lockmanager->unlock("pushLocal");
            logger "problem copying file $directoryFrom/$filenameFrom -> $directoryTo/$filenameTo", 1;
        };
        fileDateTransfer("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo");
    }

    return !compare("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo");
}


sub readDir {
    my ($directoryToRead, $recursive) = @_;

    opendir (my $dir, $directoryToRead);
    my @fromDirentries = grep(!/^\.+?/, readdir $dir);
    closedir $dir;

    return @fromDirentries;
}

sub resolveFilenameCollision {
    my ($directoryTo, $filenameTo) = @_;

    my $i = 0;
    my $filenameToCollision = $filenameTo;
    
    while (-f "$directoryTo/$filenameTo")
    {
        $i++;
        my ($name, $dir, $ext) = fileparse("$directoryTo/$filenameToCollision", '\..*');
        $filenameTo = "$name ($i)$ext";
    }
    
    return $filenameTo;
}

sub fileDateTransfer {
    my ($fileFrom, $fileTo) = @_;

    my ($atime,$mtime) = (stat($fileFrom))[8,9];
    utime($atime, $mtime, $fileTo)
}
