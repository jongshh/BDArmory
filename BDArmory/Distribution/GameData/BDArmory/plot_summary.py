#!/usr/bin/env python3

# Standard library imports
import argparse
import csv
import sys
from pathlib import Path

# Third party imports
import matplotlib.pyplot as plt
import numpy

VERSION = "1.2"

parser = argparse.ArgumentParser(description="Plot the scores of a tournament as they accumulated per round", formatter_class=argparse.ArgumentDefaultsHelpFormatter)
parser.add_argument("tournament", nargs="?", type=str, help="The tournament to plot (optional).")
parser.add_argument('-t', '--title', type=str, help="A title.")
parser.add_argument("--version", action='store_true', help="Show the script version, then exit.")
args = parser.parse_args()

if args.version:
    print(f"Version: {VERSION}")
    sys.exit()

if args.tournament is None:
    tournamentFolders = sorted(list(dir for dir in ((Path(__file__).parent / "Logs").resolve().glob("Tournament*")) if dir.is_dir()))
    tournamentDir = tournamentFolders[-1] if len(tournamentFolders) > 0 else Path('.')
else:
    tournamentDir = Path(args.tournament)

with open(tournamentDir / "summary.csv", 'r') as f:
    data = list(csv.reader(f))
vessel_count = data.index([]) - 1
names = [data[row][0] for row in range(len(data) - vessel_count, len(data))]
scores = numpy.array([[float(v) for v in data[row][1:]] for row in range(len(data) - vessel_count, len(data))])
plt.figure(figsize=(8, 5), dpi=200)
plt.plot(scores.transpose(), linewidth=5)
plt.legend(names)
if args.title is not None:
    plt.title(args.title)
plt.show(block=True)