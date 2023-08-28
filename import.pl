#!/usr/bin/env perl
use strict;
use warnings;

use File::Basename;
use File::Compare;
use File::Copy;
use Image::ExifTool qw(:Public);
use JSON::Parse 'json_file_to_perl';
use LockFile::Simple qw(lock trylock unlock);
use Time::Piece;

# Configuration
my $configFile = 'config.json';
die "Incorrect config file: $configFile\n" unless -e $configFile;
my $appConfig = json_file_to_perl ($configFile);

# Utils
my $lockmanager = LockFile::Simple->make(-hold => 600, -warn => 0);
my $exifTool = Image::ExifTool->new;

# Directories
my $importDirectory   = $appConfig->{directories}->{import};
my $incomingDirectory = $appConfig->{directories}->{incoming};
my $localDirectories  = $appConfig->{directories}->{local};
my $remoteDirectories = $appConfig->{directories}->{remote};

# Move the files from the cards to the spool directory
$lockmanager->trylock("import") && do {
    importFiles($importDirectory);
    routeFiles();
    $lockmanager->unlock("import");
};

sub importFiles {
    my ($directoryFrom) = @_;

    my $filesImported = 0;

    # Iterate through every directory entry
    my @files = readDir($directoryFrom);
    @files and do {
        foreach my $dirEntry (@files) {
            # do recursion if it's a directory
            $filesImported = $filesImported + importFiles("$directoryFrom/$dirEntry") if (-d "$directoryFrom/$dirEntry");

            # Move files from cards
            if (-f "$directoryFrom/$dirEntry") {
                foreach my $fileType (@{$appConfig->{fileTypes}}) {
                    if ($dirEntry =~ /\.$fileType->{extension}/i) {
                        my $filenameTo = $dirEntry;

                        # Change spool filename to date if there's a chance the files might be duplicates
                        $filenameTo = dateRename($directoryFrom, $dirEntry) if ($fileType->{dateRename});

                        if (copyFile($directoryFrom, $incomingDirectory, $dirEntry, $filenameTo)) {
                            logger("$directoryFrom/$dirEntry (remove)");
                            unlink("$directoryFrom/$dirEntry") or die "problem removing file from import location: $!";
                        }
                        $filesImported++;
                    }
                }
            }
        }
        logger("Import done") if ($directoryFrom eq $appConfig->{directories}->{import} && $filesImported);
    };

    return $filesImported;
}

sub routeFiles {
    my @files = readDir($incomingDirectory);
    @files and do {
        foreach my $dirEntry (@files) {
            -f "$incomingDirectory/$dirEntry" and do {
                my $canRemoveFromIncoming = 0;
                foreach my $localDirectory (@$localDirectories) {
                    if ($localDirectory->{active}) {
                        -d $localDirectory->{spool} || mkdir $localDirectory->{spool};
                        copyFile($incomingDirectory, $localDirectory->{spool}, $dirEntry, $dirEntry) or do {
                            logger("$incomingDirectory -> $localDirectory->{spool} (failed)");
                            last;
                        };
                        $canRemoveFromIncoming = 1;
                    }
                }
                foreach my $remoteDirectory (@$remoteDirectories) {
                    if ($remoteDirectory->{active}) {
                        -d $remoteDirectory->{spool} || mkdir $remoteDirectory->{spool};
                        copyFile($incomingDirectory, $remoteDirectory->{spool}, $dirEntry, $dirEntry) or do {
                            logger("$incomingDirectory -> $remoteDirectory->{spool} (failed)");
                            last;
                        };
                        $canRemoveFromIncoming = 1;
                    }
                }
                if ($canRemoveFromIncoming) {
                    logger("$incomingDirectory/$dirEntry (remove)");
                    unlink("$incomingDirectory/$dirEntry") or die "problem removing file from routing location: $!";
                } else {
                    logger("$incomingDirectory/$dirEntry (not removed)");
                }
            }
        }
        logger("Internal routing done");
    };
}

sub copyFile {
    my ($directoryFrom, $directoryTo, $filenameFrom, $filenameTo) = @_;

    if (-f "$directoryTo/$filenameTo") {
        if (compare("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo")) {
            $filenameTo = resolveFilenameCollision($directoryTo, $filenameFrom);
            logger("$directoryFrom/$filenameFrom -> $directoryTo/$filenameTo (copy with collision resolve)");
            copy("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo") || die "problem copying file $directoryFrom/$filenameFrom -> $directoryTo/$filenameTo";
            fileDateTransfer("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo");
        } else {
            logger("$directoryFrom/$filenameFrom -> $directoryTo/$filenameTo (no operation)");
        }
    } else {
        logger("$directoryFrom/$filenameFrom -> $directoryTo/$filenameTo (copy)");
        copy("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo") || die "problem copying file $directoryFrom/$filenameFrom -> $directoryTo/$filenameTo";
        fileDateTransfer("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo");
    }

    return !compare("$directoryFrom/$filenameFrom", "$directoryTo/$filenameTo");
}

sub logger {
    my ($logstring) = @_;

    my $dateTime = localtime->strftime("%Y-%m-%d %H:%M:%S");

    print "[$dateTime] [Import] $logstring\n";
}

sub readDir {
    my ($directoryToRead) = @_;
    -d $directoryToRead or die "directory $directoryToRead not found";

    opendir (my $dir, $directoryToRead);
    my @fromDirentries = grep(!/^\.+?/, readdir $dir);
    closedir $dir;

    return @fromDirentries;
}

sub dateRename {
    my ($directoryFrom, $filename) = @_;

    my $dateTime = $exifTool->ImageInfo("$directoryFrom/$filename")->{'DateTimeOriginal'};
    defined $dateTime or do {
        $dateTime = localtime((stat("$directoryFrom/$filename"))[9])->strftime("%Y:%m:%d %H:%M:%S");
    };
    $dateTime =~ s/:/-/g;
    return "$dateTime+$filename";
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