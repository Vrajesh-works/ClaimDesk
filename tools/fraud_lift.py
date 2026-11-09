#!/usr/bin/env python3
"""Recalibrate ClaimDesk risk rule weights against the Kaggle vehicle claim fraud dataset.

Download fraud_oracle.csv from
https://www.kaggle.com/datasets/shivamb/vehicle-claim-fraud-detection
(a free Kaggle login is required), then run:

    python3 tools/fraud_lift.py path/to/fraud_oracle.csv

For every category used by a rule in RiskScorer.cs it prints the fraud rate
(FraudFound_P = 1) and the lift over the base rate. A lift well above 1.0 supports a
positive weight; a lift near or below 1.0 means the rule should be weakened or dropped.
Standard library only, so it runs anywhere Python 3 does.
"""
import csv
import sys
from collections import defaultdict

RULE_COLUMNS = {
    "Days_Policy_Accident": ["none", "1 to 7"],
    "AddressChange_Claim": ["under 6 months"],
    "PoliceReportFiled": ["No"],
    "PastNumberOfClaims": ["2 to 4", "more than 4"],
    "WitnessPresent": ["No"],
    "Fault": ["Policy Holder"],
    "VehicleCategory": ["Sport"],
    "AgentType": ["External"],
}


def main(path: str) -> None:
    with open(path, newline="", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    if not rows:
        sys.exit("CSV has no rows")

    base = sum(int(r["FraudFound_P"]) for r in rows) / len(rows)
    print(f"rows={len(rows)} base_fraud_rate={base:.4f}\n")
    print(f"{'column':<22} {'value':<16} {'rows':>6} {'fraud_rate':>10} {'lift':>6}  rule")

    for column, flagged in RULE_COLUMNS.items():
        counts = defaultdict(lambda: [0, 0])
        for r in rows:
            bucket = counts[r[column].strip()]
            bucket[0] += 1
            bucket[1] += int(r["FraudFound_P"])
        for value, (total, fraud) in sorted(counts.items(), key=lambda kv: -kv[1][0]):
            rate = fraud / total if total else 0.0
            lift = rate / base if base else 0.0
            marker = "<- scored" if value in flagged else ""
            print(f"{column:<22} {value:<16} {total:>6} {rate:>10.4f} {lift:>6.2f}  {marker}")
        print()


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
