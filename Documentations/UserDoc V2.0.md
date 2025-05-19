# User Documentation

## Language Selection

At the start of the application, you can choose between French or English.

## Create a backup

* To create a backup, be sure to have less than 5 existing backups.
* Enter the name of your backup.
* Enter the source path of the folder(s) you want to save.
* Enter the destination path where the backup will be stored.
* Select the backup type: Full (copies all files/folders) or Differential (copies only changes since the last full backup).
* If you select a differential backup, you also will need to give the path of the folder containing a previous full backup.

## Load a backup

* To execute a backup job, enter the number of the backup you wish to execute.
* Once the number is entered, the backup process will begin.

## Log and state file

Every action regarding a backup job (creation or load) will automatically generate or update the log and state files.
It's possible to choose the extension of this file : Json or XML. By default the extension selected is XML.

## Encryption

In the settings it's possible to list the extensions files that will be encrypt.
Each extension has to be seperate by a coma (Ex : .txt, .jpg).

## Business Software

A business software can be choosen. No jobs can be performed while this software is running.