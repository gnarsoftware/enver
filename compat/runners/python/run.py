#!/usr/bin/env python3
"""Compat runner: parse sys.argv[1] via python-dotenv and print parsed JSON."""

import json
import sys

from dotenv import dotenv_values

if len(sys.argv) != 2:
    print("usage: run.py <fixture.env>", file=sys.stderr)
    sys.exit(2)

# dotenv_values() returns an OrderedDict-like mapping. dict() materializes a
# plain dict for json.dumps. python-dotenv resolves interpolation by default.
parsed = dict(dotenv_values(sys.argv[1]))
print(json.dumps(parsed))
