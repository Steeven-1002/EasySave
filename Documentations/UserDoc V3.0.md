# User Documentation - EasySave 3.0

## 1. Getting Started

### 1.1. Language Selection
You can choose between English and French for the application interface. This setting is available in the "Settings" menu.

## 2. Managing Backup Jobs

### 2.1. Creating a Backup Job
EasySave allows you to create and configure multiple backup jobs to suit your needs.

* **Unlimited Jobs:** You can create an unlimited number of backup jobs.
* **Job Name:** Assign a unique and descriptive name to your backup job.
* **Source Path:** Specify the folder (or folders) you want to back up. You can browse to select the source directory.
* **Target Path:** Define the destination folder where the backup will be stored. You can browse to select the target directory.
* **Backup Type:** Select the desired backup type:
    * **Full:** Copies all selected files and folders every time the job runs.
    * **Differential:** Copies only the files that have changed or are new since the last full backup of that specific job.

### 2.2. Executing Backup Jobs
* **Selection:** Select one or more backup jobs from the list that you wish to execute. You can use the checkboxes next to each job.
* **Execution:**
    * **Parallel Execution:** EasySave 3.0 executes multiple selected backup jobs simultaneously for improved efficiency.
    * Click the "Start Selected Backups" button to begin the process.

### 2.3. Real-time Interaction with Backup Jobs
You have real-time control over running backup jobs, individually or collectively:
* **Pause:** Temporarily suspend one or more active backup jobs. The pause will take effect after the currently transferring file is completed for each job.
* **Play (Resume):** Resume a paused backup job or start an initialized one.
* **Stop:** Immediately halt one or more backup jobs, including any file currently being transferred.

## 3. Monitoring Backups

### 3.1. Real-time Progress Tracking
* The main interface displays the status and progress percentage for each backup job in real-time.

### 3.2. Log and State Files
* **Daily Log File:** Every action related to a backup job (creation, execution, file transfer, errors) is automatically logged. A new log file is created daily.
    * **Format:** You can choose the log file format in the settings: JSON or XML. XML is the default selection.
    * **Information Recorded:** Timestamp, backup job name, source and destination paths (UNC if applicable), file size, transfer time, and encryption time.
* **Real-time Status File:** A single JSON file is maintained to track the current state of all backup jobs.
    * **Information Recorded:** Job name, timestamp, current state (e.g., Running, Paused, Completed, Error), number of files to copy, total size, files remaining, and progress percentage.

### 3.3. Remote Monitoring Console (New in 3.0)
* A separate graphical user interface (GUI) application is available to monitor the progress of backup jobs in real-time from a remote computer.
* This console also allows for remote interaction with the backup jobs (Pause, Play, Stop).
* Communication between the main EasySave application and the remote console is handled via Sockets.

## 4. Advanced Settings & Features

### 4.1. Encryption
* In the "Settings" menu, you can specify a list of file extensions (e.g., .txt, .docx, .jpg) that will be encrypted during the backup process.
* Each extension must be separated by a comma.
* A strong encryption key can also be set in the parameters. CryptoSoft, the encryption tool, will be used for this process.
* **CryptoSoft Single-Instance:** CryptoSoft has been updated to ensure only one instance runs at a time on the same computer.

### 4.2. Business Software Detection
* You can specify a "business software" (e.g., calc.exe, outlook.exe) in the settings.
* If EasySave detects that this specified software is running, all active backup jobs will automatically pause.
* Backups will resume automatically once the business software is closed.

### 4.3. Priority Files (New in 3.0)
* You can define a list of "priority file extensions" in the general settings.
* No non-priority file will be backed up by any job as long as there are files with priority extensions pending in at least one active backup job. This ensures critical files are saved first.

### 4.4. Large File Transfer Management (New in 3.0)
* To prevent network or disk saturation, EasySave 3.0 limits the simultaneous transfer of large files.
* You can define a threshold (in Kilobytes) in the settings. Only one file exceeding this size will be transferred at any given time across all active jobs.
* While a large file is being transferred, other backup jobs can continue to transfer smaller files (respecting the priority file rule).