# EasySave

# Introduction

EasySave is an application aiming to create and execute one or several folders.

## Context 

Our team has just integrated the software publisher ProSoft.   Under the responsibility of the CIO, we are in charge of managing the "EasySave" project which consists in developing a backup software.  As any software of the ProSoft Suite, the software will be integrated into the pricing policy.

* Unit price : 200 €HT
* Annual maintenance contract 5/7 8-17h (updates included): 12% purchase price (Annual contract tacitly renewed with revaluation based on the SYNTEC index) During this project, we will have to ensure the development, the management of major and minor versions, but also the documentation (user and customer support).  To ensure that our work can be taken over by other teams, we must work within certain constraints such as the tools used.

## Tools

* Visual Studio 2022
* Argo UML
* GitHub
* C#
*Framework .NET 8.0

# Version 3.0

Here is a list given by the client that is needed to develop the software : 

The software is a console application using .Net Core.

The software must allow you to create up to 5 backup jobs.

The software must be usable by at least English-speaking and French-speaking users.

The user can request the execution of one of the backup jobs or the sequential execution of all the jobs.

The program can be launched from a command line.

The directories (source and target) can be located on:

* Local drives

* External drives

* Network drives

  All elements of a source directory (files and subdirectories) must be backed up.

  Daily Log File :

The software must write in real time in a daily log file all actions performed during backups (transfer of a file, creation of a directory,...). The minimum information expected is:

* Timestamp
* Backup name
* Full source file address (UNC format)
* Full destination file address (UNC format)
* File size
* File transfer time in ms (negative if error)
* File Real-time status:

The software must record in real time, in a single file, the progress of the backup work and the action in progress. The information to be recorded for each backup job is at least:

* Name of the backup work
*Timestamp of last action
* Status of the Backup job (ex: Active, Not Active...). If the job is active:
* The total number of eligible files
* The size of the files to transfer
* The progression
* Number of files remaining
* Size of remaining files
* Full address of the Source file being saved
* Full destination file address

 The files (daily log and status) and any configuration files will be in JSON format

 ---

## Version 1.1

Following client feedback, **EasySave 1.1** introduces a new feature:

- **Log file format selection**: The user can now choose between **JSON** and **XML** formats for both the log file and the status file.

This version keeps the same features and limitations as version 1.0, including the console interface and a maximum of 5 backup jobs.

---

## Version 2.0

Following a customer satisfaction survey, the management decided to launch version 2.0 with major improvements:

### New Features

- **Graphical User Interface (GUI)**: The application is no longer console-based and now uses WPF.  
- **Unlimited backup jobs**: The user is no longer limited to 5 backup jobs.  
- **Integration with CryptoSoft**:  
  The application can encrypt files using the external **CryptoSoft** tool.  
  Only files with extensions defined by the user in the general settings will be encrypted.  
- **Enhanced log file**:  
  A new field is added to the daily log file:  
  - Encryption time in milliseconds  
    - `0` → no encryption  
    - `>0` → encryption duration  
    - `<0` → error code  
- **Business software detection**:  
  If a business application (defined by the user) is running, no backup job may start.  
  If detected during sequential backup, the current file is completed before stopping.  
  The interruption must be logged.

  ---

## Version 3.0

The new version **EasySave 3.0** brings the following enhancements:

### Parallel Backup Execution

- All backup jobs now run in **parallel**, replacing the previous sequential execution model.

### Priority File Management

- Files with **priority extensions** (defined by the user in the general settings) must be transferred first.
- No non-priority file can be transferred if at least one priority file remains to be backed up on any job.

### Bandwidth Control

- It is **forbidden** to transfer more than one file larger than `n` KB at the same time (where `n` is a user-defined parameter).
- During the transfer of a large file (> n KB), other smaller files may still be transferred as long as they comply with priority rules.

### Real-Time Interaction

- For **each individual job** or **all jobs together**, the user can:
  - **Pause** (effective after the current file finishes)
  - **Play** (start or resume after pause)
  - **Stop** (immediate stop of the job and current task)
- The application provides real-time progress for each job (e.g., with a progress bar or percentage).

### Business Software Detection & Pause

- If a business application is detected (e.g., calculator), **all jobs are paused automatically**.
- Backup jobs **resume automatically** once the application is closed.

### Remote Monitoring Console

- A **graphical remote console** is provided, allowing users to:
  - **Monitor** backup progress in real-time from another machine
  - **Control** the jobs remotely (Pause, Play, Stop)
- Communication is done via **Sockets**.

### CryptoSoft Mono-Instance

- The **CryptoSoft** encryption tool is now **mono-instance**, meaning:
  - Only **one instance** can run on a machine at a time
  - The system detects and **prevents** concurrent executions
  - All concurrency conflicts must be managed gracefully
