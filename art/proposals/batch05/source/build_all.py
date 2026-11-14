#!/usr/bin/env python3
"""Regenerate the whole batch05 proposal: items -> handbook pages -> UI -> validation."""
import os, subprocess, sys
import argparse
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--integration", action="store_true", help="Allow runtime changes when validating an integration branch.")
args = parser.parse_args()
here = os.path.dirname(os.path.abspath(__file__))
# Write generated paths in place. Never erase the previous delivery before a
# successful run, and propagate validation failures to the calling process.
for script in ("build_items.py", "build_handbook.py", "build_ui.py", "build_review.py", "validate.py"):
    command = [sys.executable, os.path.join(here, script)]
    if script == "validate.py" and args.integration:
        command.append("--integration")
    rc = subprocess.call(command)
    if rc:
        sys.exit("%s failed" % script)
