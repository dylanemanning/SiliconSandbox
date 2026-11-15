#!/usr/bin/env python3

"""Merge Unity package and Syft SPDX 2.3 inventories (package-level only)."""

import argparse
import copy
import json
import re
from pathlib import Path


def key(p):
    refs = [
        r.get("referenceLocator", "")
        for r in p.get("externalRefs", [])
        if r.get("referenceType") == "purl"
    ]

    if refs:
        return ("purl", sorted(refs)[0])

    return (
        "name-version",
        p["name"].casefold(),
        str(p.get("versionInfo", "NOASSERTION")),
    )


def main():
    parser = argparse.ArgumentParser()

    parser.add_argument(
        "--unity",
        required=True,
        type=Path,
    )

    parser.add_argument(
        "--scans",
        nargs="+",
        required=True,
        type=Path,
    )

    parser.add_argument(
        "--output",
        required=True,
        type=Path,
    )

    args = parser.parse_args()

    out = json.loads(
        args.unity.read_text(encoding="utf-8")
    )

    if out.get("spdxVersion") != "SPDX-2.3":
        raise ValueError("Unity SBOM must be SPDX 2.3")

    existing = {
        key(p): p["SPDXID"]
        for p in out["packages"]
    }

    ids = {
        p["SPDXID"]
        for p in out["packages"]
    }

    rels = {
        (
            r["spdxElementId"],
            r["relationshipType"],
            r["relatedSpdxElement"],
        )
        for r in out.get("relationships", [])
    }

    licenses = {
        x["licenseId"]: x
        for x in out.get("hasExtractedLicensingInfos", [])
    }

    count = 0

    for index, path in enumerate(args.scans, 1):

        if not path.is_file():
            continue

        scan = json.loads(
            path.read_text(encoding="utf-8")
        )

        if scan.get("spdxVersion") != "SPDX-2.3":
            raise ValueError(
                f"{path}: expected SPDX 2.3"
            )

        # Preserve extracted license information.
        for lic in scan.get(
            "hasExtractedLicensingInfos", []
        ):
            old = licenses.get(lic["licenseId"])

            if (
                old
                and old.get("extractedText")
                != lic.get("extractedText")
            ):
                raise ValueError(
                    f'Conflicting license ID: {lic["licenseId"]}'
                )

            licenses[lic["licenseId"]] = lic

        mapping = {}

        # Add packages not already present.
        for pkg in scan.get("packages", []):

            identity = key(pkg)

            if identity in existing:
                mapping[pkg["SPDXID"]] = existing[identity]
                continue

            p = copy.deepcopy(pkg)

            base = re.sub(
                r"[^A-Za-z0-9.-]",
                "-",
                p["SPDXID"],
            )

            new_id = (
                f"SPDXRef-Syft{index}-"
                f'{base.removeprefix("SPDXRef-")}'
            )

            suffix = 1

            while new_id in ids:
                suffix += 1
                new_id = (
                    f"SPDXRef-Syft{index}-"
                    f'{base.removeprefix("SPDXRef-")}'
                    f"-{suffix}"
                )

            p["SPDXID"] = new_id

            # This is a package-level merge.
            # File inventories are not imported.
            p.pop("hasFiles", None)
            p.pop("packageVerificationCode", None)

            out["packages"].append(p)

            ids.add(new_id)
            existing[identity] = new_id
            mapping[pkg["SPDXID"]] = new_id

            count += 1

            rels.add(
                (
                    "SPDXRef-DOCUMENT",
                    "DESCRIBES",
                    new_id,
                )
            )

        # Preserve dependencies between recognized packages.
        for r in scan.get("relationships", []):

            if r["relationshipType"] != "DEPENDS_ON":
                continue

            left = mapping.get(r["spdxElementId"])
            right = mapping.get(r["relatedSpdxElement"])

            if left and right and left != right:
                rels.add(
                    (left, "DEPENDS_ON", right)
                )

    out["relationships"] = [
        {
            "spdxElementId": a,
            "relationshipType": b,
            "relatedSpdxElement": c,
        }
        for a, b, c in sorted(rels)
    ]

    if licenses:
        out["hasExtractedLicensingInfos"] = list(
            licenses.values()
        )

    out["name"] = (
        "Silicon Sandbox - Unity and Windows Build SBOM"
    )

    out["creationInfo"]["creators"].append(
        "Tool: merge_sboms.py"
    )

    out["packages"][0]["comment"] = (
        "Combined Unity package graph and "
        "Syft-discovered software packages; "
        "detection is not exhaustive."
    )

    args.output.parent.mkdir(
        parents=True,
        exist_ok=True,
    )

    args.output.write_text(
        json.dumps(out, indent=2) + "\n",
        encoding="utf-8",
    )

    print(
        f"Added {count} distinct Syft packages; "
        f'total {len(out["packages"])} packages'
    )


if __name__ == "__main__":
    main()
