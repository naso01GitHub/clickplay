# ClickPlay on Mac — Install and Run Guide

As of Oct 22, 2026. Online copy: [ClickPlay on Mac — Install and Run Guide](https://claude.ai/artifact/FVWcKUqqLwSdTpPCEPwJCD).

## What you need

ClickPlay runs on a Mac from its source code: you copy the `ClickPlay` folder, install .NET and PowerShell once, then start it from Terminal. Plan about 30 minutes for the first setup.

| Item | Requirement |
| --- | --- |
| Mac | Apple Silicon (M1/M2/M3/M4) or Intel |
| macOS | 13 Ventura or newer |
| Free disk space | about 2 GB (.NET SDK + Chromium + build) |
| Internet | needed for installing tools and for clicktoplay.io |
| Your account | an admin password (for Homebrew and installers) |

To check which chip you have: Apple menu → About This Mac → **Chip** (Apple M…) or **Processor** (Intel).

The steps, in order:

1. Copy the **ClickPlay** folder to the Mac.
2. Install Homebrew, the .NET 10 SDK and PowerShell.
3. Build, install Chromium, run the tests.
4. Start ClickPlay.

All commands below are typed in **Terminal** (Applications → Utilities → Terminal). Copy each line, paste it, press Return.

## The project folder

In this guide, **“the project” means the folder named `ClickPlay`** — the one that holds `ClickPlay.slnx`. It is the same folder on both computers:

| Computer | Folder |
| --- | --- |
| Windows PC (where it is now) | `H:\Softuni\Projects\ClickPlay` |
| Mac (where this guide puts it) | `~/Projects/ClickPlay` = `/Users/<your Mac user name>/Projects/ClickPlay` |

Inside it you should see:

```text
ClickPlay/
├── ClickPlay.slnx        the solution file — every build and test command uses it
├── INSTALL-MAC.md        this guide
├── AGENTS.md, CLAUDE.md, target_web.md, Request.txt
├── src/
│   ├── ClickPlay.App/    the window (Avalonia)
│   └── ClickPlay.Core/   website, mail and calendar logic
└── tests/
    └── ClickPlay.Tests/
```

Every command in Steps 3 and 4 is run **inside this folder**: that is why they start with `cd ~/Projects/ClickPlay`. If you see `ClickPlay.slnx` when you type `ls`, you are in the right place.

## Step 1 — Copy the ClickPlay folder to the Mac

The easiest way is to clone it from GitHub: your `main` branch is already pushed to `github.com/naso01GitHub/clickplay`. Use option B only if the Mac cannot reach GitHub.

### Option A — Clone from GitHub (recommended)

1. In Terminal, make a folder for projects and clone into it:

```bash
mkdir -p ~/Projects
cd ~/Projects
git clone https://github.com/naso01GitHub/clickplay.git ClickPlay
cd ClickPlay
```

2. If macOS asks to install the **command line developer tools** for `git`, click **Install**, wait, then run the `git clone` line again.
3. If the repository is private, GitHub asks for a login: use your GitHub user name and a **personal access token** as the password (GitHub → Settings → Developer settings → Personal access tokens).

Later, to get new changes made on Windows: `cd ~/Projects/ClickPlay && git pull`.

### Option B — Copy the folder (USB stick, AirDrop, OneDrive)

On the Windows PC, copy `H:\Softuni\Projects\ClickPlay`, but **leave out** the folders that are rebuilt or private:

| Leave out | Why |
| --- | --- |
| every `bin` and `obj` folder (under `src\` and `tests\`) | Windows build output; the Mac rebuilds it |
| `.vs`, `.idea` | Windows editor settings |
| `conversations` | your saved chat logs |

Everything else is needed, including the hidden folders `.claude`, `.cursor` and `.git`. The copy is about 2.5 MB.

On the Mac, put the folder at `~/Projects/ClickPlay` (Finder → Go → Home, create **Projects**, drop **ClickPlay** in it). Then in Terminal:

```bash
cd ~/Projects/ClickPlay
xattr -dr com.apple.quarantine .
```

The second line removes the “downloaded from the internet” flag that AirDrop or a browser adds, so macOS does not block the files.

**Do not copy** `%AppData%\ClickPlay` (Windows settings and the encrypted password): the password file is locked to your Windows login and cannot be read on the Mac. You type the email settings again once on the Mac.

## Step 2 — Install the tools (once per Mac)

ClickPlay needs two tools: the **.NET 10 SDK** to build and run it, and **PowerShell** (`pwsh`) to install Chromium for Playwright. Homebrew installs both with one command each.

1. Install **Homebrew** (skip if `brew --version` already works):

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

2. On an **Apple Silicon** Mac only, add Homebrew to your path (the installer prints these lines too):

```bash
echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.zprofile
eval "$(/opt/homebrew/bin/brew shellenv)"
```

3. Install the .NET SDK and PowerShell:

```bash
brew install --cask dotnet-sdk
brew install --cask powershell
```

4. Close Terminal and open it again, then check both:

```bash
dotnet --version
pwsh --version
```

Expected: `dotnet` prints **10.0.x** or higher, `pwsh` prints **PowerShell 7.x**. If `dotnet` prints 8 or 9, install .NET 10 from [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0) (macOS **Installer**, Arm64 for Apple Silicon or x64 for Intel).

Without Homebrew, both have normal Mac installers: .NET from the link above, PowerShell as a `.pkg` from [github.com/PowerShell/PowerShell/releases](https://github.com/PowerShell/PowerShell/releases) (`osx-arm64.pkg` or `osx-x64.pkg`).

**Optional:** `brew install node` — only if you also use Claude Code on the Mac; its conversation-log hook runs on Node. ClickPlay itself does not need it.

## Step 3 — Build, install Chromium, run the tests

Three commands, run inside the `ClickPlay` folder (`~/Projects/ClickPlay`). The first build downloads the libraries ClickPlay uses (Avalonia, Playwright, MailKit), so it takes a few minutes.

1. Go into the `ClickPlay` folder and build it:

```bash
cd ~/Projects/ClickPlay
dotnet build ClickPlay.slnx
```

Expected at the end: `Build succeeded.` with `0 Error(s)`.

2. Install the Chromium browser that ClickPlay drives (about 150 MB, stored in `~/Library/Caches/ms-playwright`):

```bash
pwsh src/ClickPlay.Core/bin/Debug/net10.0/playwright.ps1 install chromium
```

Run this once per Mac, and again only after updating the Playwright package.

3. Run the tests — a quick health check that everything works on this Mac:

```bash
dotnet test ClickPlay.slnx
```

Expected: `Passed!  - Failed: 0, Passed: 130` (the number grows as features are added). The tests never open clicktoplay.io and never send email.

## Step 4 — Start ClickPlay

Start it from Terminal with one command; the ClickPlay window opens and Terminal stays busy until you close it.

```bash
cd ~/Projects/ClickPlay
dotnet run --project src/ClickPlay.App
```

First use, in the window:

1. Click **Load sports**. Chromium runs hidden; the status banner at the top shows each step.
2. Pick a **Sport** → a **City** → tick one or more **Locations**.
3. Set the calendar day, **Days ahead**, **Repeat (min)** (default 2) and the weekday and weekend/holiday time windows.
4. Optional — open **Email (SMTP)**:
    - pick **Gmail** or **Yahoo** from **Known providers** (fills Host and Port), or type any server;
    - User = your full email address; Password = an **app password**, not your normal one (Gmail: Google Account → Security → App passwords; Yahoo: Account security → Generate app password);
    - From = your address; To = one address per line; Subject = any text;
    - leave **Remember login** ticked to keep User and Password for next time.
5. Click **Search**. Click **Stop** to end the loop.

The first time ClickPlay saves the password, macOS may ask whether **ClickPlay** (shown as `dotnet`) may use the **login keychain**: type your Mac password and click **Always Allow**.

To watch the browser work, set **Debug** to `true` before Load sports or Search.

To quit: close the window (or press Control+C in Terminal).

## Where ClickPlay keeps its files

On the Mac, ClickPlay's files live under the hidden folder `~/.config/ClickPlay` and the password lives in the Keychain; nothing from the Windows PC needs to be moved.

| What | Windows | Mac | Move it? |
| --- | --- | --- | --- |
| Email settings (Host, Port, User, From, To, Subject) | `%AppData%\ClickPlay\smtp.settings.json` | `~/.config/ClickPlay/smtp.settings.json` | No — retype once on the Mac |
| SMTP password | encrypted file, locked to your Windows login | Keychain item **ClickPlay** / smtp-password | Cannot be moved — retype |
| Diagnostic screenshots | `%AppData%\ClickPlay\diagnostics` | `~/.config/ClickPlay/diagnostics` | No |
| Errors log (errors only, restarted weekly) | next to `ClickPlay.App.exe` | `src/ClickPlay.App/bin/Debug/net10.0/errors-log.txt` | No |
| Chromium | `%LocalAppData%\ms-playwright` | `~/Library/Caches/ms-playwright` | No — installed in Step 3 |

To open a hidden folder in Finder: Go → Go to Folder (Shift+Command+G) → type `~/.config/ClickPlay`. To see or delete the saved password: open **Keychain Access** and search for **ClickPlay**.

## Optional — A standalone copy that starts without `dotnet run`

A self-contained build bundles .NET with ClickPlay into one folder, so it starts faster and keeps working even if the `ClickPlay` folder moves. Chromium from Step 3 is still used.

1. Build it for your chip (Apple Silicon: `osx-arm64`, Intel: `osx-x64`):

```bash
cd ~/Projects/ClickPlay
dotnet publish src/ClickPlay.App -c Release -r osx-arm64 --self-contained true -o ~/Applications/ClickPlay
```

2. Start it:

```bash
~/Applications/ClickPlay/ClickPlay.App
```

You can also double-click `ClickPlay.App` in that folder in Finder; it opens with a Terminal window beside it. The errors log is then written to `~/Applications/ClickPlay/errors-log.txt`.

This build can also be made on the Windows PC (same command, any output folder) and copied to the Mac. A copied build needs two extra lines before the first start, or macOS refuses to open it:

```bash
xattr -dr com.apple.quarantine ~/Applications/ClickPlay
chmod +x ~/Applications/ClickPlay/ClickPlay.App
```

It is not a signed `.app` bundle, so it has no Dock icon of its own and is not in Launchpad.

## Troubleshooting

Most problems are a missing tool or a step skipped; find the message you see below.

| You see | Fix |
| --- | --- |
| `zsh: command not found: dotnet` (or `brew`, `pwsh`) | Close and reopen Terminal. Still missing: repeat Step 2 (on Apple Silicon, the `brew shellenv` lines). |
| `The current .NET SDK does not support targeting .NET 10.0` | `dotnet --version` is below 10: install .NET 10 (Step 2 link). |
| `MSB4025` / cannot open `ClickPlay.slnx` | Same cause: the SDK is too old for `.slnx`; install .NET 10. |
| `Executable doesn't exist at …ms-playwright…` | Chromium is missing: run the `playwright.ps1 install chromium` line from Step 3. |
| `playwright.ps1` not found | Build first (`dotnet build ClickPlay.slnx`); the script is created by the build. |
| “ClickPlay.App cannot be opened” / “damaged” | The files are quarantined: run the `xattr -dr com.apple.quarantine` line for that folder (Step 1 or Optional). |
| `Permission denied` when starting `ClickPlay.App` | `chmod +x ~/Applications/ClickPlay/ClickPlay.App` |
| Keychain prompt every time | Choose **Always Allow**, not **Allow**. |
| Banner: “clicktoplay.io is busy (…Too many connections)” | The website's database is full, not a ClickPlay fault: wait a minute and try again. |
| `Mail failed: AuthenticationException` | Use an **app password**; check User is the full address and Port matches the provider (Gmail 587, Yahoo 465). |
| Window opens but stays empty, or a crash right at start | Run `dotnet test ClickPlay.slnx`; send the failing test's output to whoever maintains ClickPlay. |

If something else goes wrong, the text in the status banner at the top can be selected and copied — include it when asking for help, together with the newest files in `~/.config/ClickPlay/diagnostics`.
