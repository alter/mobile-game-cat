#!/usr/bin/env python3
"""Print per-method line coverage of CatShelter.Core from a cobertura report."""
import argparse
import glob
import sys
import xml.etree.ElementTree as ET

ap = argparse.ArgumentParser(description=__doc__)
ap.add_argument("--min", type=float, default=None,
                help="fail with exit code 1 below this line rate, for CI")
args = ap.parse_args()

files = sorted(glob.glob("TestResults/*/coverage.cobertura.xml"))
if not files:
    sys.exit("no coverage.cobertura.xml found under TestResults/")
root = ET.parse(files[-1]).getroot()
tot_c = tot = 0
for c in root.iter("class"):
    name = c.get("name") or ""
    if not name.startswith("CatShelter.Core"):
        continue
    # Core and its tests compile into one assembly, so the test classes land in
    # the same report and used to be counted as covered code — which inflated
    # the number the 90% gate is read off.
    if name.startswith("CatShelter.Core.Tests"):
        continue
    for m in c.iter("method"):
        lines = m.find(".//lines")
        if lines is None:
            continue
        cov = sum(1 for l in lines if l.get("hits") != "0")
        n = len(list(lines))
        if cov < n:
            print(f"{name.split('.')[-1]}.{m.get('name')}: {cov}/{n}")
        tot_c += cov
        tot += n
if not tot:
    sys.exit("no CatShelter.Core classes in the report — check the runsettings filter")
rate = 100 * tot_c / tot
print(f"TOTAL Core: {tot_c}/{tot} = {rate:.1f}%  (uncovered methods listed above)")
if args.min is not None and rate < args.min:
    sys.exit(f"FAIL: line rate {rate:.1f}% is below the required {args.min}%")
