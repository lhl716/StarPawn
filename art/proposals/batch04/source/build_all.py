#!/usr/bin/env python3
"""Regenerate the whole batch04 proposal: items -> handbook pages -> UI -> validation."""
import os, shutil, subprocess, sys
here = os.path.dirname(os.path.abspath(__file__))
for d in ("items", "ui", "review"):
    shutil.rmtree(os.path.join(here, "..", d), ignore_errors=True)
for script in ("build_items.py", "build_handbook.py", "build_ui.py", "build_review.py", "validate.py"):
    rc = subprocess.call([sys.executable, os.path.join(here, script)], stdout=subprocess.DEVNULL if script != "validate.py" else None)
    if rc and script != "validate.py":
        sys.exit("%s failed" % script)
