# User Documentation

## Language Selection

In the parameters you can choose between English and French

## Create a backup

* You can choose to create an unlimited amount of backup jobs
* Enter the name of your backup.
* Enter the source path of the folder(s) you want to save.
* Enter the destination path where the backup will be stored.
* Select the backup type: Full (copies all files/folders) or Differential (copies only changes since the last full backup).


## Load a backup

* To execute a backup job, select the backup you wish to execute.
* Once you selected the backup job you can execute it

## Log and state file

Every action regarding a backup job (creation or load) will automatically generate or update the log and state files.
It's possible to choose the extension of this file : Json or XML. By default the extension selected is XML.

## Encryption

In the settings it is possible to enter a list of extensions that will be encrypt during the backup.
Each extension has to be seperate by a coma (Ex : .txt, .jpg).

## Business Software

A business software can be choosen. No jobs can be performed while this software is running.
