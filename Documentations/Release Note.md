## Release Note - EasySave v1.0

**Release Date:** \09/05/2025

**Version:** 1.0

### Description

EasySave is a command-line file backup application for Windows, designed to perform full and differential backups of local, external, and network directories. This initial version provides the basic functionality to create and execute backup jobs, with real-time logging and status tracking

### New Features

* **Backup Job Management:**
    * Creation of up to 5 backup jobs
    * Configuration of backup parameters (name, source directory, target directory, type)
* **Backup Types:**
    * Support for full and differential backups
* **Command-Line Interface:**
    * .NET Core console application
    * Support for french and english languages
* **Backup Execution:**
    * Execution of individual backup jobs
    * Sequential execution of backup jobs via the command line (e.g., 1-3, 1;3)
* **Support for Various Directory Types:**
    * Backup from local, external, and network drives
* **Real-time Logging:**
    * Logging of all backup actions to a daily log file in JSON forma
    * Recorded information: timestamp, backup job name, source and destination paths (UNC), file size, transfer time (negative on error)
* **Real-time Status Tracking:**
    * Recording of backup job progress in a single file in JSON format
    * Recorded information: job name, timestamp, status, progress, etc
* **Compliance with Client Requirements:**
    * Log and status files are saved in appropriate locations for client servers
    * JSON files are formatted for readability in a text editor


### Notes

* This version has been tested on Windows 11.
* For assistance, please contact www.easysave.com/support.
