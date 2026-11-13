#!/usr/bin/env python3
"""Regenerate this folder from the shared deterministic batch04 generator."""
import os, subprocess, sys
here = os.path.dirname(os.path.abspath(__file__))
gen = os.path.normpath(os.path.join(here, "..", "..", "..", "source", "build_items.py"))
sys.exit(subprocess.call([sys.executable, gen, "refined_silver_bar"]))
