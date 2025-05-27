## Release Note - EasySave v1.0

**Release Date:** 28/05/2025

**Version:** 3.0

### Description

EasySave is a command-line file backup application for Windows, designed to perform full and differential backups of local, external, and network directories. This initial version provides the basic functionality to create and execute backup jobs, with real-time logging and status tracking

# EasySave 3.0 - Release Notes

We are excited to introduce EasySave 3.0, a major update to our backup solution, focused on enhancing performance, flexibility, and real-time control. This version introduces key features requested by our users for an even more robust and efficient backup experience.

## Key New Features (EasySave 3.0)

* **Parallel Backups:**
    * Simultaneous execution of multiple backup jobs, significantly optimizing overall backup time.
* **Advanced Priority File Management:**
    * Introduction of a file prioritization system based on extensions.
    * Files with priority extensions (configurable by the user in the settings) are processed before any non-priority file, regardless of the backup job.
* **Bandwidth Control for Large Files:**
    * Limitation of simultaneous transfer of files exceeding a user-configurable threshold (in KB) to avoid bandwidth saturation.
    * During the transfer of a large file, other jobs can continue transferring smaller files, while respecting the priority file rule.
* **Improved Real-Time User Interactions:**
    * Granular control over backup jobs:
        * **Pause:** Pause one or more jobs (the pause takes effect after the current file transfer within the respective job is completed).
        * **Play (Resume):** Start a job or resume it after a pause.
        * **Stop:** Immediately stop one or more jobs, including the ongoing file transfer task.
    * Real-time progress tracking for each job, including at least a progress percentage visible in the main interface.
* **Automatic Pause and Detection for Business Software:**
    * Automatic pausing of all backup jobs if specific business software (configurable, e.g., calculator) is detected running.
    * Automatic resumption of backups as soon as the business software is closed.
* **Remote Monitoring Console (New Graphical Interface):**
    * Introduction of a separate GUI application allowing remote, real-time monitoring of backup job progress.
    * This remote console also allows interaction with the jobs (pause, resume, stop).
    * Communication between the main application and the remote console established via Sockets.
* **CryptoSoft as Single-Instance:**
    * The external CryptoSoft tool has been modified to ensure it can only run as a single instance simultaneously on the same computer.
    * Management of issues related to this restriction to ensure smooth integration.
* **(Optional) Dynamic Reduction of Parallel Jobs:**
    * Ability for the application to reduce the number of backup tasks running in parallel if network load exceeds a defined threshold, to prevent network saturation.

## Inherited and Maintained Features

* **Backup Job Management:**
    * Creation and configuration of backup jobs (name, source directory, target directory, type).
* **Backup Types:**
    * Full support for complete and differential backups.
* **Main User Interface:**
    * User-friendly WPF application for backup management.
    * Language support for French and English.
* **Backup Execution:**
    * Individual or grouped execution of backup jobs from the graphical interface.
* **Support for Various Locations:**
    * Backup from local drives, external drives, and network shares (UNC paths).
* **Real-time Logging:**
    * Recording of all backup actions to a daily log file.
    * User-configurable log format (JSON or XML).
    * Recorded information: timestamp, backup job name, source and destination paths, file size, transfer time.
* **Real-time Status Tracking:**
    * Recording of backup job progress in a single status file (JSON format).
    * Recorded information: job name, timestamp, status, progress, etc.
* **Compliance and Formatting:**
    * Log and status files saved in appropriate locations.
    * JSON files formatted for easy readability in a text editor.

### Notes

* This version has been tested on Windows 11.
* For assistance, please contact www.easysave.com/support.
