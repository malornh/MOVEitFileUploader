READ TO THE END!

# MoveitFileUploaderApp

MoveitFileUploaderApp is a C# console application designed to sync files between a local folder and a cloud folder in MOVEit Transfer.
The application monitors changes in the local folder, such as file creations and deletions, and updates the cloud folder accordingly.
It also polls the cloud folder for changes and updates the local folder to reflect those changes.

## Features

- **Automatic file upload:** Monitors the local folder for new files and uploads them to the cloud folder.
- **Automatic file deletion:** Monitors the local folder for deleted files and deletes the corresponding files from the cloud folder.
- **Cloud to local sync:** Polls the cloud folder for changes and updates the local folder with new or deleted files.
- **Initial sync:** On startup, syncs all files from the cloud folder to the local folder.
- **Secure login:** Prompts the user for MOVEit Transfer credentials and retrieves an access token for authentication.

## Prerequisites

- .NET Core SDK 3.1 or later
- MOVEit Transfer API credentials (username and password)

## Installation

1. **Clone the repository:**
   ```sh
   git clone https://github.com/malornh/MOVEitFileUploader.git
   cd MoveitFileUploaderApp

dotnet restore
dotnet build
dotnet run --project MoveitFileUploader

Enter username: your_username
Enter password: ********

!!! USE AN EMPTY LOCAL FOLDER, THE CONTENT MAY BE LOST !!!

Enter local folder path: C:\path\to\your\folder 

(Copy the folder's path directly from the folder's URL)





