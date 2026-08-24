#!/usr/bin/env python3
"""Print per-method line coverage of CatShelter.Core from a cobertura report."""
import glob
import sys
import xml.etree.ElementTree as ET

files = sorted(glob.glob("TestResults/*/coverage.cobertura.xml"))
if not files:
    sys.exit("no coverage.cobertura.xml found under TestResults/")
root = ET.parse(files[-1]).getroot()
tot_c = tot = 0
for c in root.iter("class"):
    if not (c.get("name") or "").startswith("CatShelter.Core"):
        continue
    for m in c.iter("method"):
        lines = m.find(".//lines")
        if lines is None:
            continue
        cov = sum(1 for l in lines if l.get("hits") != "0")
        n = len(list(lines))
        print(f"{m.get('name')}: {cov}/{n}")
        tot_c += cov
        tot += n
print(f"TOTAL Core: {tot_c}/{tot} = {100 * tot_c / tot:.0f}%")
