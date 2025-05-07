# EasySave

# Introduction

EazySave is an application aiming to create and execute one or several folders.

## Context 

Our team has just integrated the software publisher ProSoft.   Under the responsibility of the CIO, we are in charge of managing the "EasySave" project which consists in developing a backup software.  As any software of the ProSoft Suite, the software will be integrated into the pricing policy.

* Unit price : 200 €HT
* Annual maintenance contract 5/7 8-17h (updates included): 12% purchase price (Annual contract tacitly renewed with revaluation based on the SYNTEC index) During this project, we will have to ensure the development, the management of major and minor versions, but also the documentation (user and customer support).  To ensure that our work can be taken over by other teams, we must work within certain constraints such as the tools used.

## Tools

Visual Studio 2022
Argot UML
GitHub
C#
Framework .NET 8.0

# Version 1.0

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
