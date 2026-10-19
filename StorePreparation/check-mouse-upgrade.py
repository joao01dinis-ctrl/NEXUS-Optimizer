"""Real SQLite 0.10 -> Mouse Lab migration assertions, only in disposable hosted CI."""
import os
import sqlite3
import sys
from pathlib import Path

if os.getenv('GITHUB_ACTIONS') != 'true' or os.getenv('RUNNER_ENVIRONMENT') != 'github-hosted':
    raise SystemExit('Disposable hosted runner required')
mode, filename = sys.argv[1:]
path = Path(filename).resolve()
if 'NexusOptimizer.StoreValidation.07' not in path.parts or not path.is_file():
    raise SystemExit('Unexpected test database')
with sqlite3.connect(path) as connection:
    if mode == 'seed':
        if connection.execute("SELECT count(*) FROM sqlite_master WHERE name='mouse_lab_profiles'").fetchone()[0]:
            raise SystemExit('Initial payload already contains Mouse Lab; not a 0.10 migration')
        connection.execute("UPDATE settings SET profile='Trabalho' WHERE id=1")
        connection.execute("INSERT INTO profiles VALUES('Migration fixture',8,'CI only — preserve existing profiles')")
        connection.execute("INSERT INTO history(at,before_profile,after_profile,undone) VALUES('2026-10-19T00:00:00Z','Normal','Trabalho',0)")
    elif mode == 'verify':
        assert connection.execute('SELECT profile FROM settings WHERE id=1').fetchone()[0] == 'Trabalho'
        assert connection.execute("SELECT seconds FROM profiles WHERE name='Migration fixture'").fetchone()[0] == 8
        assert connection.execute("SELECT count(*) FROM history WHERE at='2026-10-19T00:00:00Z' AND before_profile='Normal' AND after_profile='Trabalho' AND undone=0").fetchone()[0] == 1
        assert connection.execute('SELECT count(*) FROM mouse_lab_profiles').fetchone()[0] >= 5
        assert connection.execute("SELECT value FROM mouse_lab_metadata WHERE key='schema'").fetchone()[0] == '1'
        assert connection.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
    else:
        raise SystemExit('Unknown mode')
print('PASS:', mode, 'real 0.10 settings, history, custom profile and additive Mouse Lab schema')
