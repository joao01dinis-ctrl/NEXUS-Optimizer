# Privacy policy

NEXUS Optimizer 0.1.0 is a local Windows application. It does not include
analytics, advertising, telemetry, automatic updates or cloud synchronisation.
It does not transfer information to other networked systems.

The application reads CPU and GPU names, physical memory statistics, CPU usage
and fixed-volume capacity/free space. It displays these locally. It stores
application profiles and their change history in SQLite under
`%LOCALAPPDATA%\NexusOptimizer`. Local logs can contain hardware names,
profile changes, file paths and Windows error messages. Seven daily log files
are retained. Profile history is retained until the user removes the data.

`NEXUS_DATA_DIR` can override the data folder for isolated testing.
Closing the application stops monitoring. To remove the portable application,
close it and delete its extracted folder. To remove preferences and logs too,
delete the NEXUS Optimizer data folder. No Windows service is installed.

GitHub and SignPath are separate services used for development/distribution;
their websites have their own privacy policies. NEXUS does not contact them
when it runs. Before sharing diagnostic logs in public issues, remove personal
paths and any other information you do not want to publish.
