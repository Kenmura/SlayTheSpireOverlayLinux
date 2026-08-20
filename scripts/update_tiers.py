#!/usr/bin/env python3
import argparse
import json
import os
import re
import subprocess
import sys
import urllib.request
from datetime import datetime

# Repository & paths
REPO_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WORKSHOP_JSON = os.path.join(REPO_DIR, "workshop", "baalorlord_tiers.json")
EMBEDDED_JSON = os.path.join(REPO_DIR, "src", "SlayTheSpireOverlay.Godot", "baalorlord_tiers.json")
WORKSHOP_META_JSON = os.path.join(REPO_DIR, "workshop", "workshop.json")
WORKSHOP_CONTENT_DIR = os.path.join(REPO_DIR, "workshop", "content")
WORKSHOP_UPLOADER_DIR = os.path.join(REPO_DIR, "workshop", "uploader")

HEADERS = {
    "User-Agent": "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, Gecko) Chrome/120.0.0.0 Safari/537.36"
}

# Map tier names to numeric scores
TIER_SCORES = {
    "S": 95.0,
    "A": 80.0,
    "B": 65.0,
    "C": 50.0,
    "D": 35.0,
    "F": 15.0,
    # Untapped.gg custom text tiers (score=0 means hide number, show label)
    "Always Amazing": 0.0,
    "Always Good": 0.0,
    "Needs Synergy": 0.0,
    "Inconsistent": 0.0,
    "Map Dependent": 0.0,
    "Almost Never": 0.0,
}

def fetch_url(url):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req) as resp:
        return resp.read().decode("utf-8", errors="ignore")

def sanitize_text(text):
    """Strip any external creator or site branding from text."""
    if not text:
        return ""
    cleaned = text.replace("Baalorlord ", "").replace("Baalorlord", "")
    cleaned = cleaned.replace("Untapped.gg ", "").replace("Untapped.gg", "").replace("Untapped", "")
    return cleaned.strip()

def parse_tier_list_page(html, page_title):
    """
    Parse a tier list page using the UUID-based approach:
    1. Build a UUID->tier_name map from tier definitions in the page.
    2. Extract item entries (item_id + tier UUID) and resolve each to its tier name.
    This is accurate because items reference their tier by UUID, not by position in the page.
    """
    # Step 1: Build UUID -> tier name map
    # Pattern: "uuid\":\"<uuid>\"...(within ~100 chars)..."name\":\"<tier_name>\"
    uuid_to_tier = {}
    for uuid, name in re.findall(
        r'uuid\\\":\\\"([a-f0-9-]{36})\\\"[^}]{0,100}?name\\\":\\\"([^\\\\\"]+)\\\"',
        html
    ):
        # Skip the tier list's own UUID (its name is the page title, not a tier label)
        if name != page_title and name not in ("", page_title):
            uuid_to_tier[uuid] = name

    if not uuid_to_tier:
        return {}

    # Step 2: Extract item entries: tier UUID + item_id
    results = {}
    for tier_uuid, item_id in re.findall(
        r'tier\\\":\\\"([a-f0-9-]{36})\\\"[^}]{0,300}?item_id\\\":\\\"([A-Z][A-Z0-9_]{2,39})\\\"',
        html
    ):
        tier_name = uuid_to_tier.get(tier_uuid)
        if not tier_name:
            continue

        score = TIER_SCORES.get(tier_name, 0.0)
        commentary = sanitize_text(f"{item_id.replace('_', ' ').title()} [{sanitize_text(page_title)}] - Rated '{tier_name}'.")

        results[item_id] = {
            "CardId": item_id,
            "Tier": tier_name,
            "Score": score,
            "Commentary": commentary,
        }

    return results

def scrape_untapped_tierlists():
    print("[*] Step 1: Discovering tier lists on Untapped.gg...")
    index_url = "https://sts2.untapped.gg/en/tier-lists?creator=Baalorlord"
    index_html = fetch_url(index_url)

    uuids = sorted(list(set(re.findall(r"/en/tier-list/([a-f0-9-]{36})", index_html))))
    print(f"[+] Found {len(uuids)} tier list pages to scrape.")

    raw_data = {}

    for uid in uuids:
        page_url = f"https://sts2.untapped.gg/en/tier-list/{uid}"
        try:
            html = fetch_url(page_url)
            h1_match = re.search(r"<h1>(.*?)</h1>", html)
            raw_title = h1_match.group(1) if h1_match else "Tier List"
            page_title = sanitize_text(raw_title)

            items = parse_tier_list_page(html, raw_title)
            raw_data.update(items)
            print(f"    - Scraped '{page_title}': {len(items)} items.")
        except Exception as ex:
            print(f"[-] Error scraping {page_url}: {ex}")

    # Return sorted alphabetically for deterministic ordering and clean git diffs
    return {k: raw_data[k] for k in sorted(raw_data.keys())}

def compare_databases(old_db, new_db):
    changes = []
    for key, new_val in new_db.items():
        if key not in old_db:
            changes.append(f"[ADDED] {key}: Tier='{new_val['Tier']}'")
        else:
            old_val = old_db[key]
            if old_val.get("Tier") != new_val.get("Tier") or old_val.get("Score") != new_val.get("Score"):
                changes.append(f"[MODIFIED] {key}: Tier '{old_val.get('Tier')}' -> '{new_val.get('Tier')}'")
    for key in old_db.keys():
        if key not in new_db:
            changes.append(f"[REMOVED] {key}")
    return changes

def run_command(cmd, cwd=REPO_DIR):
    print(f"[*] Executing: {cmd}")
    res = subprocess.run(cmd, shell=True, cwd=cwd)
    if res.returncode != 0:
        print(f"[-] Command failed with exit code {res.returncode}: {cmd}")
        sys.exit(res.returncode)

def main():
    parser = argparse.ArgumentParser(description="Scrape tier lists, detect changes, and auto-publish updates.")
    parser.add_argument("-f", "--force", action="store_true",
                        help="Force update, rebuild, and push even if no tier list diffs are detected.")
    parser.add_argument("--skip-steam", action="store_true",
                        help="Skip the Steam Workshop upload step (e.g. if Steam is not running).")
    args = parser.parse_args()

    print(f"==================================================")
    print(f" Tier List Scraper & Auto-Publisher [{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}]")
    if args.force:
        print(" [!] FORCE FLAG SET: Will rebuild and push regardless of diffs.")
    print(f"==================================================")

    old_db = {}
    if os.path.exists(WORKSHOP_JSON):
        try:
            with open(WORKSHOP_JSON) as f:
                old_db = json.load(f)
        except Exception as ex:
            print(f"[!] Warning: Could not read existing database: {ex}")

    new_db = scrape_untapped_tierlists()
    print(f"[+] Total items scraped: {len(new_db)}")

    if not new_db:
        print("[-] Scrape returned no items. Aborting to protect existing database.")
        sys.exit(1)

    changes = compare_databases(old_db, new_db)

    if not changes and not args.force:
        print("[+] No changes detected in tier lists. Everything is up-to-date!")
        sys.exit(0)

    if changes:
        print(f"\n[!] Detected {len(changes)} changes:")
        for c in changes[:20]:
            print(f"  {c}")
        if len(changes) > 20:
            print(f"  ... and {len(changes) - 20} more changes.")
    else:
        print("\n[!] No tier diffs found, but --force flag is active. Forcing update and push!")

    print("\n[*] Step 2: Updating JSON database files & Steam Workshop changeNote...")
    with open(WORKSHOP_JSON, "w") as f:
        json.dump(new_db, f, indent=2, sort_keys=True)
    with open(EMBEDDED_JSON, "w") as f:
        json.dump(new_db, f, indent=2, sort_keys=True)

    commit_msg = ""
    if os.path.exists(WORKSHOP_META_JSON):
        try:
            with open(WORKSHOP_META_JSON) as f:
                meta = json.load(f)

            if args.force and not changes:
                meta["changeNote"] = "Misc bug fixes and maintenance updates."
                commit_msg = f"Misc bug fixes [{datetime.now().strftime('%Y-%m-%d %H:%M')}]"
            else:
                summary_lines = changes[:10]
                if len(changes) > 10:
                    summary_lines.append(f"... and {len(changes) - 10} more tier list updates.")
                meta["changeNote"] = f"Tier List Update [{datetime.now().strftime('%Y-%m-%d')}]:\n" + "\n".join(summary_lines)
                commit_msg = f"Auto-update tier lists [{datetime.now().strftime('%Y-%m-%d %H:%M')}]"

            with open(WORKSHOP_META_JSON, "w") as f:
                json.dump(meta, f, indent=2)
            print(f"[+] Updated changeNote in workshop.json!")
        except Exception as ex:
            print(f"[!] Warning: Could not update workshop.json changeNote: {ex}")

    if not commit_msg:
        commit_msg = f"Auto-update tier lists [{datetime.now().strftime('%Y-%m-%d %H:%M')}]"

    print("\n[*] Step 3: Running unit test suite...")
    run_command("distrobox enter dotnet-dev -- dotnet test SlayTheSpireOverlay.slnx")

    print("\n[*] Step 4: Publishing mod assembly to workshop/content/...")
    os.makedirs(WORKSHOP_CONTENT_DIR, exist_ok=True)
    run_command(f"distrobox enter dotnet-dev -- env LD_PRELOAD=\"\" dotnet publish src/SlayTheSpireOverlay.Godot/SlayTheSpireOverlay.Godot.csproj -c Release -o '{WORKSHOP_CONTENT_DIR}'")

    print("\n[*] Step 5: Committing and pushing changes to GitHub...")
    run_command("git add workshop/baalorlord_tiers.json src/SlayTheSpireOverlay.Godot/baalorlord_tiers.json workshop/workshop.json")
    # Only commit if there is actually something staged (avoids "nothing to commit" error
    # when --force is used but the tier data was already up to date)
    staged = subprocess.run("git diff --cached --quiet", shell=True, cwd=REPO_DIR)
    if staged.returncode != 0:
        run_command(f"git commit -m '{commit_msg}'")
        run_command("git push")
    else:
        print("[+] No file changes to commit — skipping git commit/push.")

    if args.skip_steam:
        print("\n[!] --skip-steam set: skipping Steam Workshop upload.")
    else:
        print("\n[*] Step 6: Uploading to Steam Workshop...")
        print("[!] NOTE: Steam must be running and you must be logged in.")

        # Clone uploader if not already present
        if not os.path.isdir(WORKSHOP_UPLOADER_DIR):
            run_command("git clone https://github.com/megacrit/sts2-mod-uploader.git workshop/uploader")
        else:
            print("[+] Uploader repo already cloned.")

        # Build the uploader
        run_command(f"distrobox enter dotnet-dev -- env LD_PRELOAD=\"\" dotnet build '{WORKSHOP_UPLOADER_DIR}/ModUploader.sln' -c Release")

        # Locate compiled DLL
        import glob
        dll_matches = glob.glob(os.path.join(WORKSHOP_UPLOADER_DIR, "bin", "**", "ModUploader.dll"), recursive=True)
        if not dll_matches:
            print("[-] Could not find ModUploader.dll — skipping Steam upload.")
        else:
            uploader_dll = dll_matches[0]
            uploader_bin = os.path.dirname(uploader_dll)
            steam_dir = os.path.join(WORKSHOP_UPLOADER_DIR, "steam")
            print(f"[+] Found ModUploader at: {uploader_dll}")

            # Copy Steam API libraries
            import shutil
            shutil.copy(os.path.join(steam_dir, "libsteam_api.so"), uploader_bin)
            runtime_lib = os.path.join(uploader_bin, "runtimes", "linux-x64", "lib", "netstandard2.1")
            os.makedirs(runtime_lib, exist_ok=True)
            shutil.copy(os.path.join(steam_dir, "libsteam_api.so"), runtime_lib)
            shutil.copy(os.path.join(steam_dir, "steam_appid.txt"), REPO_DIR)

            run_command(f"distrobox enter dotnet-dev -- env LD_PRELOAD=\"\" dotnet '{uploader_dll}' upload -w '{REPO_DIR}/workshop'")

    print("\n==================================================")
    if args.skip_steam:
        print(" SUCCESS! Tier lists updated and pushed to GitHub.")
        print(" (Steam Workshop upload skipped — run without --skip-steam to publish)")
    else:
        print(" SUCCESS! Tier lists updated, rebuilt, pushed to GitHub & Steam Workshop!")
    print("==================================================")

if __name__ == "__main__":
    main()
