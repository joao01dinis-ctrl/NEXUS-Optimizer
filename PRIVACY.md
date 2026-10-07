# Privacy policy

NEXUS Optimizer 0.2.0 is a local Windows application. It does not include
analytics, advertising, telemetry, automatic updates or cloud synchronisation.
The optional DNS measurement contacts Cloudflare, Google, Quad9 and AdGuard over
IPv4/UDP port 53, sending three example.com A queries per provider. Providers
can see the public source IP and queried domain. This runs only after the user
presses Measure DNS; no hardware, process list, file paths or logs are uploaded.

The application reads CPU and GPU names, physical memory statistics, CPU usage
and fixed-volume capacity/free space. It displays these locally. It stores
application profiles, settings before/after values and gaming-session history in SQLite under
`%LOCALAPPDATA%\NexusOptimizer`. Local logs can contain hardware names,
profile changes, file paths and Windows error messages. Seven daily log files
are retained. Profile history is retained until the user removes the data.

`NEXUS_DATA_DIR` can override the data folder for isolated testing.
Memory inspection reads accessible process names, PIDs, working sets and private
memory in the user's session. Temporary cleanup reviews names, sizes and timestamps
under the user's temporary folder; selected old files are sent to the Windows
Recycle Bin after confirmation, where the user can restore them. Recycling does
not empty the bin or permanently delete files.

Closing the application stops monitoring and game-session tracking. Restore active
system adjustments and unfinished game sessions before removing the database;
closing does not automatically restore all settings. To remove the portable application,
close it and delete its extracted folder. To remove preferences and logs too,
delete the NEXUS Optimizer data folder. No Windows service is installed.

GitHub and SignPath are separate services used for development/distribution;
their websites have their own privacy policies. NEXUS does not contact them
when it runs. Before sharing diagnostic logs in public issues, remove personal
paths and any other information you do not want to publish.
