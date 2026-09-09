> ## mRemoteNF
>
> **mRemoteNF** is a private, security-focused fork of [mRemoteNG](https://github.com/mRemoteNG/mRemoteNG).
> The focus of this fork is to close security issues — for example just-in-time (lazy) credential
> decryption to mitigate CVE-2023-30367 and hardening of the legacy cryptography provider.
> It tracks upstream mRemoteNG and applies security fixes on top; changes flow one way (upstream → fork).

---



  
<p align="center">
  mRemoteNF - An open source, multi-protocol, tabbed remote connections manager allowing you to view all of your connections in a simple yet powerful interface
</p>

## Features

The following protocols are supported:

* RDP (Remote Desktop Protocol)
* VNC (Virtual Network Computing)
* SSH (Secure Shell)
* Telnet (TELecommunication NETwork)
* HTTP/HTTPS (Hypertext Transfer Protocol)
* rlogin (Remote Login)
* Raw Socket Connections
* Powershell remoting
* AnyDesk

For a detailed feature list and general usage support, refer to the [Documentation](https://mremoteng.readthedocs.io/en/latest/).

## Installation

### Supported Operating Systems

- [Windows 11](https://en.wikipedia.org/wiki/Windows_11)
- [Windows 10](https://en.wikipedia.org/wiki/Windows_10)
- [Windows 8.1](https://en.wikipedia.org/wiki/Windows_8.1)
- [Windows Server 2022](https://en.wikipedia.org/wiki/Windows_Server_2022)
- [Windows Server 2019](https://en.wikipedia.org/wiki/Windows_Server_2019)
- [Windows Server 2016](https://en.wikipedia.org/wiki/Windows_Server_2016)
- [Windows Server 2012 R2](https://en.wikipedia.org/wiki/Windows_Server_2012_R2)

#### Source package

This contains the source code from which mRemoteNF is built.
You will need to compile it yourself using Visual Studio.

### Minimum Requirements

Make sure you have the latest version installed:

* [Microsoft .NET Desktop Runtime 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
* Microsoft Visual C++ Redistributable 2015–2026 is needed:
 - [x64](https://aka.ms/vs/18/release/vc_redist.x64.exe)
 - [ARM64](https://aka.ms/vs/18/release/vc_redist.arm64.exe)
 - [x86](https://aka.ms/vs/18/release/vc_redist.x86.exe)
* Microsoft Terminal Service Client 6.0 or later (needed if you use RDP with mstscax.dll and/or msrdp.ocx to be registered)

### Download

> :star: Starting Windows 11 you can use winget to install mRemoteNG. Just run `winget install -e --id mRemoteNG.mRemoteNG`

mRemoteNF is available as a redistributable MSI package or as a portable ZIP package and can be downloaded from the following locations:
* [GitHub](https://github.com/mRemoteNG/mRemoteNG/releases)
* [Project Website](https://mremoteng.org/download)

### Command line install

The MSI package of mRemoteNF can be installed using the command line:

`msiexec /i [/qn] C:\Path\To\mRemoteNG-Installer.exe [INSTALLDIR=value] [IGNOREPREREQUISITES=value] [/lv* <log path>]`

| Argument/Property | Value | Description |
|-|-|-|
| /qn | `Silent Installation` | Will run the installer silently in the background. |
| /lv* | `Silent Installation` | Will write a logfile to the specified location. (For paths that contain spaces, enclose the path in double quotes) |
| INSTALLDIR | `folder path` | Allows you to set the installation directory from the command line. (For paths that contain spaces, enclose the path in double quotes) |
| IGNOREPREREQUISITES | `0` or `1` | When set to `1`, the installer will not be halted if any prerequisite check is not met. You must still run the installer as administrator. |

## Manual Uninstall

_If you are using the Portable version, simply deleting the folder that contains mRemoteNG should be sufficient. These uninstall instructions are only necessary for the normal binary .MSI installed version of mRemoteNG_

* Delete the folder where mRemoteNG was installed. By default, this is:
	`%PROGRAMFILES%\mRemoteNG` (for versions before 1.77 on a x64 Windows its `%programfiles(x86)%\mRemoteNG`)

* Delete the mRemoteNG install entry from the following location. You may search for "mRemoteNG" in the DisplayName field:
  * x86 Windows or mRemoteNG starting with v1.77: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\`
  * x64 Windows and mRemoteNG before 1.77: `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\`
* Remove the following registry key: `HKLM\SOFTWARE\mRemoteNG` (on x64 Windows with mRemoteNG before 1.77 it's `HKLM\SOFTWARE\WOW6432Node\mRemoteNG`)

* (Optional) If you would also like to delete user data remove `%LOCALAPPDATA%\mRemoteNG`
* (Optional) If you would also like to remove the connection configuration, delete `%APPDATA%\mRemoteNG`

* (Optional) If no other software uses it, the "Microsoft Windows Desktop Runtime" may be uninstalled too.

## Featured Projects

* [PSmRemoteNG](https://github.com/realslacker/PSmRemoteNG) A module to create mRemoteNG connection files from PowerShell.
* [mRemoteNGOpenVPN](https://github.com/T3los/mRemoteNGOpenVPN) A script that can be embedded as an external tool to control OpenVPN.
* [mRemoteNG-Icons](https://github.com/bearlikelion/mRemoteNG-Icons) A collection of fancy icons to customize the connections

## Contribute

If you find mRemoteNF useful and would like to contribute, it would be greatly appreciated. Because this is a security-focused fork, contributions that improve security, fix vulnerabilities, or keep the fork in sync with upstream mRemoteNG are especially welcome.



</br>

