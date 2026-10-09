# Privacy policy

NEXUS Optimizer 0.6.0 is a local Windows application. It has no own analytics,
advertising, cloud sync or automatic update client. No hardware inventory,
process list, file paths or diagnostic logs are uploaded.

Optional DNS measurement sends three example.com A queries to each of Cloudflare,
Google, Quad9 and AdGuard over IPv4/UDP port 53 after the user presses Measure.
These providers see the queried domain and public source IP. Optional ICMP testing
sends five small echo requests to the IP explicitly entered; the destination sees
the requests. Changing the system DNS provider affects subsequent Windows/app DNS
traffic, not just the NEXUS benchmark. The chosen provider may receive those queries.
DNS changes, DHCP renewal and cache flushing require explicit confirmation and
administrator rights. NEXUS does not configure encrypted DNS or send browsing history.

Local inspection reads CPU/GPU names, memory and CPU usage, disk capacity/free space,
network adapters, IPs, gateways, DNS, DHCP, accessible process names/PIDs/start times,
working sets/private memory, optional package identities and selected Windows values.
The app library stores executable paths selected by the user; detection compares
these with accessible running processes. It does not launch registered executables. Optional automatic gaming mode uses a
frozen, explicitly reviewed library/profile for the current opening only, changes
only the authorised session settings and attempts to restore them when the game
ends or the application closes normally. It does not stop/freeze background apps.

SQLite under %LOCALAPPDATA%/NexusOptimizer stores profiles, app-library paths, settings
before/after, sessions, benchmark environment/timings and action events, including
latency destination IPs and app names. Graph samples remain in memory. Local logs may
contain file paths, hardware names and Windows errors; seven daily files are retained.
History is retained until the data is removed. NEXUS_DATA_DIR isolates testing data.
Backup export copies all SQLite tables, not logs; keep exported backups private.

Optional PresentMon CSV import reads only the file chosen by the user. It stores
the chosen process/stream's aggregate presentation timings, process identity,
filename without its full path, SHA-256 and user-declared test conditions locally.
It does not upload the CSV or launch/install PresentMon. The hash identifies bytes,
not authenticity or the capture PC. Optional per-app CPU measurement reads two
local cumulative processor-time samples about two seconds apart, for accessible
processes in the current session. These samples are shown, without changing apps
or sending them anywhere. Opening official documentation uses the default browser
and that website's own privacy policy.

Manual cleanup reads file names/sizes/timestamps in the reviewed category. Selected
old regular files are revalidated and sent to the Windows Recycle Bin. Optional
automatic cleanup is disabled on each launch and requires confirmation of the local
Temp folder, interval and bounds: at least 30 days since access/write, at most 100
files, 10 MiB per file and 256 MiB per run. It pauses for detected library apps/game
sessions. It never empties the bin, permanently deletes files or follows reparse points.
Shader/web cache removal can trigger regeneration or another sign-in.

Optional working-set reduction affects only a reviewed eligible background app;
Windows reloads needed pages, without freeing its private allocations. Optional app
removal is limited to a known optional package for the current user and may remove
local app data. It has no NEXUS undo. Reinstall opens a Microsoft Store search, which
uses the Store's own account/privacy policy and availability. No mass removal occurs.

User privacy policies preserve the old value/absence for undo. Writing a policy does
not prove effectiveness for every Windows edition/account. Windows security, update,
restore, driver and essential-service settings are not disabled. The app does not
install a service or elevate itself. Closing stops monitoring/automatic cleanup;
restore active adjustments and sessions before deleting the database or uninstalling.

GitHub/SignPath/Store websites have their own policies. NEXUS does not contact GitHub
or SignPath at runtime. Review logs, backups and executable paths before sharing them.
