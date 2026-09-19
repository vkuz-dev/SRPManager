# SRPManager - AppLocker Management Utility

## Overview
<img width="2400" height="1667" alt="srpmanager-overview" src="https://github.com/user-attachments/assets/49f9a21f-443e-4fe8-91f2-e0b375df347e" />

---
SRPManager is a system-tray utility for monitoring and managing Windows [AppLocker](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/applocker/applocker-overview) policy.
It lets administrators enable or disable AppLocker enforcement, view active rules, run `GPUpdate /force`, temporarily freeze the policy state, and inspect both standard and packaged-app (AppX) AppLocker events - all from the notification area or the command line. With **remote options** enabled, a **Host** selector at the top of the tray menu retargets every action at another machine over WinRM.

---

## Features

| Feature                        | Description                                                                                                     |
| ------------------------------ | --------------------------------------------------------------------------------------------------------------- |
| **System tray icon**           | Shows the current AppLocker state (On / Off / AuditOnly / Custom / Unknown) with a distinct icon for each state |
| **Enable / Disable AppLocker** | Toggle AppLocker enforcement with a single click                                                                |
| **Policy freeze**              | Temporarily lock the policy to prevent AppLocker from being re-enabled - useful during software installations   |
| **Event log viewer**           | Tab-based window showing AppLocker events for files (EXE, DLL, MSI, Script) and packaged apps (AppX)            |
| **Event summaries**            | Summarized view grouping events by file path or package name with a count of occurrences                        |
| **Filtering**                  | Quick search bar (space-separated terms, prefix `!` to exclude) plus a detailed filter dialog                   |
| **CSV export**                 | Export events or rules to a CSV file                                                                            |
| **Remote management** _(opt-in)_ | Pick a target host from the tray menu and run every action - enable/disable, logs, rules, GPUpdate - against it over WinRM |
| **Rules viewer**               | Shows all active AppLocker rules - EXE, MSI, Script, and AppX                                                   |
| **Edit AppLocker rules**       | Opens the AppLocker rule editor directly (local machines only, requires admin rights)                           |
| **GPUpdate**                   | Run `gpupdate /force` from the tray menu                                                                        |
| **Audit logging**              | Records an entry in the Windows Application event log whenever AppLocker is enabled or disabled                 |
| **Multi-language**             | Tray menu and settings in English, Latvian |
| **Command-line interface**     | Full CLI support for scripting and automation                                                                   |

---

## Command-Line Usage

```
SRPManager.exe [command] [options]
```

| Command | Aliases | Description |
|---|---|---|
| _(no arguments)_ | | Launch the system-tray GUI |
| `-help` | `-h`, `-?`, `/?` | Show help text |
| `-status` | | Show the current AppLocker state, service status, and whether the policy is frozen |
| `-enable` | `-e` | Enable AppLocker |
| `-enable -force` | `-ef` | Enable AppLocker even if the policy is currently frozen |
| `-disable` | `-d` | Disable AppLocker |
| `-logs <days>` | | Print AppLocker events from the last N days to the console |
| `-logs <days> -gui` | | Open the event viewer pre-loaded with the last N days of events |
| `-rules` | | Print the effective policy's rules to the console |
| `-rules -gui` | | Open the rules viewer |

### Examples

```
SRPManager.exe -enable
SRPManager.exe -status
SRPManager.exe -logs 1
SRPManager.exe -rules
SRPManager.exe -rules > rules.txt
```

`-logs` and `-rules` both print a plain text table to the console, so they can be redirected to a
file or piped into `findstr`. Add `-gui` to either one to open the corresponding window instead.

---

## System Tray

### Icon States

The tray icon always reflects the current AppLocker status:

- **On** - AppLocker is actively enforcing rules
- **Off** - AppLocker is not blocking anything
- **AuditOnly** - Rules are in logging mode only; nothing is blocked
- **Custom** - Mixed enforcement (some rule types enforced, others not)
- **Unknown** - Status could not be determined

### Tray Menu

| Menu Item | Description |
|---|---|
| **Host** _(if remote options are enabled in Settings)_ | Choose the machine every other menu item acts on - Localhost, a recently used host, or a new one |
| **Status indicators** | Read-only info: whether you are running as Administrator, whether the AppLocker service is running, and whether DLL filtering is active |
| **Enable / Disable** | Toggle AppLocker enforcement. A checkmark shows the current state. |
| **Freeze** | Lock the policy to prevent AppLocker from being re-enabled until you unfreeze it |
| **Logs** | View events from the last 1, 7, or 30 days |
| **Rules** | Open the active rules viewer |
| **GPUpdate** | Run Group Policy update |
| **Settings** | Open the settings window |
| **About** | Version and license information |
| **Exit** | Close SRPManager |

---

## Event Log Viewer

The event viewer opens as a tabbed window with four tabs:

- **File Events** - Individual AppLocker events for EXE, DLL, MSI, and Script files
- **File Event Summary** - Events grouped by file path, showing how many times each was allowed or blocked
- **AppX Events** - Events for packaged (Store) applications
- **AppX Event Summary** - Events grouped by package name

### Filtering

- **Quick filter bar** - Type one or more words to show only matching rows. Prefix a word with `!` to exclude it (e.g., `explorer !system`).
- **Filter dialog** - Build structured filters using Contains, Equals, StartsWith, EndsWith, or Regex.
- **Checkboxes** - Quickly hide DLL events, hide Windows system events, or show only your own user's events.

### Right-click Options

Right-click any event row to open the containing folder, copy the file path or publisher, filter by that user or file, or exclude a file from the current view.

---

## Rules Viewer

Shows all active AppLocker rules parsed from the current Effective policy. Columns include rule type, enforcement mode, action (Allow/Deny), the rule name, who it applies to, and the path, publisher, or hash condition.

Use the search bar to filter rules. You can export the full list to CSV.

The same data is available without the GUI via `SRPManager.exe -rules`, which prints one row per
rule. Hash rules print their source file names rather than the raw digests, and any exceptions are
summarised as a count - use the CSV export from the GUI when you need the full detail.

**Edit AppLocker Rules** (admin only, local machines only) - Opens the built-in AppLocker rule editor so you can make changes directly. The button is hidden when `DomainMode=1` (see [Deploy in domain](#deploy-in-domain)), since local rules are overridden by domain Group Policy there.

---

## Remote Management

SRPManager can manage AppLocker on another machine, not just the one it's running on.

First, **enable** Remote Management in the Settings.

Open the **Host** menu at the top of the tray menu, enter a hostname, and the rest of the tray menu switches to operate against that machine - enable/disable, view logs, view rules, and GPUpdate all work the same way remotely as they do locally. Recently used hosts are remembered for quick access, and you can switch back to your local machine at any time.

This requires the target machine to have PowerShell Remoting (WinRM) enabled and reachable on the network. Also, the user that currenly runs SRPManager should have Administrator privileges on the target machine, as it will be used to execute remote commands.

---

## AppLocker States Explained

| State | What it means |
|---|---|
| **On** | AppLocker is enforcing rules - unauthorized programs will be blocked |
| **Off** | AppLocker is not blocking anything (service stopped, no rules configured, or rules allow everything) |
| **AuditOnly** | Rules exist but only generate log entries; nothing is actually blocked |
| **Custom** | Unusual configuration - some rule types are enforced while others are not |
| **Unknown** | SRPManager could not determine the state (check if PowerShell is available) |

---

## Settings

Open settings from the tray menu. User settings take priority over machine-wide defaults set by an administrator.

| Setting                       | Description                                                                                                                                                   |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Run at startup**            | Start SRPManager automatically when you log in                                                                                                                |
| **Always show tray icon**     | Keep the icon visible in the taskbar notification area                                                                                                        |
| **Enable remote options**     | Shows the **Host** selector in the tray menu, so every action can be pointed at another machine over WinRM. Shows a WinRM reminder when turned on. |
| **Language**                  | Choose between English, Latvian, or Russian. Latvian and Russian cover the tray menu and settings; the log and rules viewers are English-only, and any untranslated text falls back to English. |
| **Copy policy files**         | Copies the built-in policy files to `C:\Windows\AppLocker\Policies\` (requires admin rights, not needed on domain-joined machines)                            |

**Domain mode** is registry-only and has no entry in the Settings dialog - see [Deploy in domain](#deploy-in-domain). When enabled, the "Enable AppLocker" action uses a transitional policy that avoids conflicting with domain Group Policy.

### Deploy in domain

When deploying at scale, it is recommended to use registry keys to configure app default look and behavior.
Settings are stored in the Windows registry under
- `HKCU\SOFTWARE\SRPManager` (user)
- `HKLM\SOFTWARE\SRPManager` (machine defaults).
- The Settings dialog is accessible from the tray icon's context menu.
- Values set by User are saved in the `HKCU` registry hive.
- `HKCU` values have a priority over `HKLM`.
- `HKLM` keys can be used to set the default values for the tool in the domain environment, when deploying.

| Setting                   | Key                 | Type   | Description                                                                                                                                                       |
| ------------------------- | ------------------- | ------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Always show tray icon** | `PinToTray`         | DWORD  | Pins app icon to the taskbar corner for easier access                                                                                                             |
| **Enable remote options** | `EnableRemoteManagement` | DWORD  | When set to `1`, shows the **Host** selector in the tray menu. Can be set at `HKLM` to enable it by default across a fleet.                                  |
| **Language**              | `Language`          | String | Default UI language, supported values are: `en\|lv\|ru`                                                                                                           |
| **Domain mode**           | `DomainMode` (HKLM) | DWORD  | When set to `1`, the enable flow uses `AppLocker-EnableNotConfigured.xml` (NotConfigured) instead of the full policy, to avoid overlapping with domain GPO policy |

---

## Policy Files

SRPManager includes three ready-to-use AppLocker policy files:

| File                                | Purpose                                                                                                                                                             |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AppLocker-Enable.xml`              | Full enforcement policy. Allows programs from `Windows\`, `Program Files\`, and Microsoft-signed binaries. User-writable paths and known LOLbin tools are excluded. |
| `AppLocker-EnableNotConfigured.xml` | A transitional policy used on domain-joined machines - clears local settings so that the domain Group Policy can take over after a GPUpdate.                        |
| `AppLocker-Disable.xml`             | Disables AppLocker enforcement entirely.                                                                                                                            |

---

## Audit Log

Every time AppLocker is enabled or disabled through SRPManager, an entry is written to the **Windows Application** event log:

| Event ID | Meaning |
|---|---|
| **1000** | AppLocker was **enabled** (includes user, machine, and timestamp) |
| **1001** | AppLocker was **disabled** (includes user, machine, and timestamp) |

---

## Requirements
- .NET Framework 4.6.2 or later
- Administrator rights required for enabling/disabling AppLocker
- For **Remote Actions**: target machines need WinRM (PowerShell Remoting) enabled and reachable

## Known issues
- **Domain-joined machines without domain connectivity**: On domain-joined machines, the effective AppLocker policy is created by merging local and domain policies. If the domain is unreachable, the domain policy cannot be fetched, causing the merge to fail. As a result, any changes to local policy are ignored until the domain becomes reachable again. Since SRPManager manipulates local policies, you won't be able to change the AppLocker state when the domain is unavailable. **Workaround**: Configure AppLocker rules that allow the local Administrator account to run all applications (`*`), or configure specific approved rules that give Administrator some leeway for emergency situations. The built-in Administrator SID ends in `-500` and is machine-specific (`S-1-5-21-<machine-id>-500`) - run `wmic useraccount where name='Administrator' get sid` on the target to get the exact value. The built-in **Administrators group** (`S-1-5-32-544`) is the same on every machine and is usually the more practical choice.
