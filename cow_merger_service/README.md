# cow_merger_service

### Configuration

The configuration is done via the appsettings.json file. Make sure to configure all the settings below.

| Name | Description |
| ---- | ---------- |
|WorkingDirectory| 	It is the directory where the server stores the data from a session and does the merge with the original image.|
|OriginalImageDirectory| Location of the original images, once a session is started, the server will copy the corresponding file from this folder in the WorkingDirectory.|
|DestinationDirectory|After a successful merge, the image will be copied from the WorkingDirectory in this folder with an increased file extension.|
|Urls| The Urls the server should bind to. Semicolon separated list.|
