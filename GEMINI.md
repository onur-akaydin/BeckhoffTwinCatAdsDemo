# Project Overview

This project is a .NET Framework console application designed to facilitate communication with a Beckhoff TwinCAT PLC. It provides a simple, interactive command-line interface for reading and writing various data types (Integer, Double, String, and Bool) to a PLC. The application uses the `TwinCAT.Ads` library for ADS communication, `Spectre.Console` for the user interface, and `Newtonsoft.Json` for managing a persistent JSON configuration file.

The architecture is straightforward, with a clear separation of concerns:

*   **`Program.cs`**: Contains the main entry point and handles the loading and saving of application settings.
*   **`Settings.cs`**: Defines the data structure for all configurable settings, including PLC connection parameters and variable addresses.
*   **`PlcService.cs`**: Encapsulates all the logic for interacting with the Beckhoff PLC, such as connecting, disconnecting, creating variable handles, and reading/writing data.
*   **`UserInterface.cs`**: Manages all user-facing interactions, including the main menu, configuration prompts, and the display of data and error messages.

# Building and Running

## Building

This project can be built using either `msbuild.exe` or Visual Studio.

**Using `msbuild.exe`:**

```powershell
# To restore NuGet packages (run from the solution directory)
nuget restore BeckhoffTwinCatAdsDemo.sln

# To build the project (run from the solution directory)
msbuild BeckhoffTwinCatAdsDemo.sln /p:Configuration=Debug
```
*You may need to have msbuild and nuget in your system's PATH.*

**Using Visual Studio:**

1.  Open the `BeckhoffTwinCatAdsDemo.sln` file in Visual Studio.
2.  Select the desired build configuration (e.g., "Debug").
3.  From the "Build" menu, select "Build Solution".

## Running

Once the project is built, you can run the application from the command line:

```powershell
# Navigate to the output directory
cd BeckhoffTwinCatAdsDemo\bin\Debug

# Run the application
.\BeckhoffTwinCatAdsDemo.exe
```

## Testing

This project does not currently have an automated test suite.

# Development Conventions

*   **Coding Style:** The codebase follows standard C# and .NET Framework conventions.
*   **Dependency Management:** NuGet packages are managed using `packages.config`.
*   **Configuration:** All application settings are stored in a `settings.json` file, which is created and managed by the application.
*   **Error Handling:** Exceptions are handled gracefully, with detailed (but sanitized) error messages printed to the console to aid in troubleshooting. Operations are designed to be resilient, attempting to process all requested PLC variables even if one or more fail.
