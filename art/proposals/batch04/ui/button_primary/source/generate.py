#!/usr/bin/env python3
import os, subprocess, sys
here = os.path.dirname(os.path.abspath(__file__))
sys.exit(subprocess.call([sys.executable, os.path.normpath(os.path.join(here, "..", "..", "..", "source", "build_ui.py")), "button_primary"]))
