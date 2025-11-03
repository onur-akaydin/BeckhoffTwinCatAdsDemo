
# Connecting to Your Beckhoff PLC: A Simple Guide

This guide will walk you through the process of using the Beckhoff TwinCAT ADS Demo application to establish a connection with your Beckhoff PLC and exchange data.

## Introduction

The Beckhoff TwinCAT ADS Demo is a simple console application that allows you to test the communication with your Beckhoff PLC. It provides a user-friendly interface to read and write different data types (Integer, Double, String, and Bool) to your PLC, helping you to verify the connection and data exchange without writing any code.

## Prerequisites

Before you begin, please make sure you have the following:

*   A Beckhoff PLC (e.g., C6930-0070) powered on, running, and connected to your network.
*   The `BeckhoffTwinCatAdsDemo.exe` application file.
*   The **AMS Net ID** of your PLC.
*   The **Port number** of your PLC's runtime (this is typically `851` for the first runtime).

## PLC Project Setup

For the application to communicate with your PLC, you need to have specific variables declared in your PLC project. The application is pre-configured to work with these variables.

1.  Open your TwinCAT XAE environment and your PLC project.
2.  Create a new `PROGRAM` and name it `MAIN`.
3.  In the declaration part of the `MAIN` program, add the following variables:

```st
PROGRAM MAIN
VAR
    nInteger    : INT;
    fReal       : REAL;
    sString     : STRING;
    bBool       : BOOL;
END_VAR
```

4.  Make sure to activate the configuration and start your PLC in **Run** mode.

## Application Configuration

The first time you run the application, it will guide you through a configuration process.

1.  Double-click the `BeckhoffTwinCatAdsDemo.exe` file to run it.
2.  The application will prompt you to enter the following information:
    *   **NetId:** The AMS Net ID of your PLC.
    *   **Port:** The port number of your PLC's runtime.
    *   **Integer Value Address:** The address of the integer variable.
    *   **Double Value Address:** The address of the double variable.
    *   **String Value Address:** The address of the string variable.
    *   **Bool Value Address:** The address of the bool variable.

    For your convenience, the application will suggest default values for all these settings. If you have followed the "PLC Project Setup" section, you can simply press **Enter** to accept the defaults.

3.  Once you have entered the configuration, the application will save it to a file named `settings.json` in the same folder as the application. This file will be used for future runs, so you won't have to enter the configuration every time.

Here is an example of what the `settings.json` file will look like:

```json
{
  "NetId": "192.168.1.1.1.1",
  "Port": 851,
  "IntValueAddress": "MAIN.nInteger",
  "DoubleValueAddress": "MAIN.fReal",
  "StringValueAddress": "MAIN.sString",
  "BoolValueAddress": "MAIN.bBool"
}
```

## Using the Application

Once the application is configured, you will see a menu with the following options:

*   **1. Read Values:** This option reads the current values of the configured variables from your PLC and displays them in a table.
*   **2. Write Values:** This option prompts you to enter new values for each variable and writes them to your PLC.
*   **3. View Configuration:** This option displays the current connection and variable address settings from the `settings.json` file.
*   **4. Configure:** This option allows you to modify the connection and variable address settings. The changes will be saved to the `settings.json` file.
*   **5. Exit:** This option closes the application.

## Troubleshooting

If you encounter any issues, here are some common problems and their solutions:

*   **"Failed to connect to PLC" error:**
    *   Check if the `NetId` and `Port` in your `settings.json` file are correct.
    *   Make sure your PLC is in **Run** mode and connected to the network.
    *   Verify that there are no firewall rules blocking the connection.

*   **"Failed to create variable handles" error:**
    *   Ensure that you have correctly declared the variables in your PLC project as described in the "PLC Project Setup" section.
    *   Make sure the variable addresses in your `settings.json` file match the variable names in your PLC project.
