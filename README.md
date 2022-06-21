# cow_merger_service
### Table of Contents
1. [Build](#build)
2. [Configuration](#configuration)
3. [Running the server](#running-the-server)



### Build

#### Windows
  Install the dotnet sdk from: https://dotnet.microsoft.com/en-us/download/visual-studio-sdks.  
  This server hast been developed with net 5.0 but should also work with 6.0.
  
  If you have Visual Stuido installed, just open the `cow_merger_service.sln`, the rest should be self explanatory.
  
  Otherwise  you can build it the same way as on linux:
  ```
  dotnet build cow_merger_service.csproj --runtime win-x64
  ```

#### Ubuntu

1. If not already happened to have the dotnet sdk installed:
    First add the Microsoft package signing key to your list of trusted keys and add the package repository.

    For ubuntu 22.04:
    ```
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    ```
    For another version, replace the 22.04 in the link with the corresponding version.

    Then install the net sdk:
    ```
    sudo apt-get update; \
    sudo apt-get install -y apt-transport-https && \
    sudo apt-get update && \
    sudo apt-get install -y dotnet-sdk-5.0
    ```

    This server hast been developed with net 5.0 but should also work with 6.0.

    For other distributions or more information, see https://docs.microsoft.com/en-us/dotnet/core/install/linux.

2. To build the cow server, run the following command:

    ```
    dotnet build cow_merger_service.csproj --runtime linux-x64
    ```
    This will build the server ind `/bin/Debug` as debug build.
    For further information see https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build.

### Configuration

The configuration is done via the `appsettings.json` file. Make sure to configure all the settings below.

| Name | Description |
| ---- | ---------- |
|WorkingDirectory| 	It is the directory where the server stores the data from a session and does the merge with the original image.|
|OriginalImageDirectory| Location of the original images, once a session is started, the server will copy the corresponding file from this folder in the WorkingDirectory.|
|DestinationDirectory|After a successful merge, the image will be copied from the WorkingDirectory in this folder with an increased file extension.|
|Urls| The Urls the server should bind to. Semicolon separated list.|


### Running the server
If the server is run on a different machine than the one that has compiled it and has build without self-contained, make sure that the machine has either the corresponding dotnet sdk or runtime. 

Just run the `cow_merger_service` or `cow_merger_service.exe` depending on your operating system.

If you instead run it with:
```
ASPNETCORE_ENVIRONMENT=Development ./cow_merger_service
```
you will get a swagger page under the link `/swagger/index.html` which will show you all endpoints.

