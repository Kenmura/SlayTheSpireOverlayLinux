#!/usr/bin/env python3
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
PROTON_USER_DIR = "/var/home/nickmarc/.local/share/Steam/steamapps/compatdata/2868840/pfx/drive_c/users/steamuser/AppData/Roaming/SlayTheSpire2"
PROTON_CACHE_JSON = os.path.join(PROTON_USER_DIR, "tier_list_cache.json")
LOCAL_MOD_DIR = "/var/home/nickmarc/.local/share/Steam/steamapps/common/Slay the Spire 2/mods/"

HEADERS = {
    "User-Agent": "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, Gecko) Chrome/120.0.0.0 Safari/537.36"
}

def fetch_url(url):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req) as resp:
        return resp.read().decode("utf-8", errors="ignore")

def sanitize_text(text):
    if not text:
        return ""
    cleaned = text.replace("Baalorlord ", "").replace("Baalorlord", "")
    cleaned = cleaned.replace("Untapped.gg ", "").replace("Untapped.gg", "").replace("Untapped", "")
    return cleaned.strip()

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
            raw_h1 = h1_match.group(1) if h1_match else "Tier List"
            h1 = sanitize_text(raw_h1)
            
            unescaped = html.replace("\\\"", "\"").replace("\\\\", "\\")
            
            pattern = r"\"name\":\"([^\"]{1,40})\"[^}]*?\"color\":\"(#[0-9a-fA-F]{6})\""
            parts = re.split(pattern, unescaped)
            
            items_found = 0
            if len(parts) >= 4:
                for i in range(1, len(parts), 3):
                    tier_name = parts[i].strip()
                    block = parts[i+2] if i+2 < len(parts) else ""
                    
                    found_items = re.finditer(r"\"id\":\"([A-Z0-9_]{3,40})\".*?\"name\":\"([^\"]+)\"", block[:8000])
                    for m in found_items:
                        item_id = m.group(1)
                        item_name = m.group(2)
                        
                        if item_id in ["CARD", "RELIC", "POTION", "MAIN", "ANY"]:
                            continue
                        
                        score = 0.0
                        if tier_name in ["S Tier", "S"]:
                            score = 95.0
                        elif tier_name in ["A Tier", "A"]:
                            score = 80.0
                        elif tier_name in ["B Tier", "B"]:
                            score = 65.0
                        elif tier_name in ["C Tier", "C"]:
                            score = 50.0
                        elif tier_name in ["D Tier", "D"]:
                            score = 35.0
                        elif tier_name in ["F Tier", "F"]:
                            score = 15.0

                        commentary = sanitize_text(f"{item_name} [{h1}] - Rated '{tier_name}'.")

                        raw_data[item_id] = {
                            "CardId": item_id,
                            "Tier": tier_name,
                            "Score": score,
                            "Commentary": commentary
                        }
                        items_found += 1
            print(f"    - Scraped '{h1}': {items_found} items.")
        except Exception as ex:
            print(f"[-] Error scraping {page_url}: {ex}")

    # Return dictionary sorted alphabetically by key for deterministic ordering
    sorted_data = {k: raw_data[k] for k in sorted(raw_data.keys())}
    return sorted_data

def compare_databases(old_db, new_db):
    changes = []
    
    for key, new_val in new_db.items():
        if key not in old_db:
            changes.append(f"[ADDED] {key}: Tier='{new_val['Tier']}'")
        else:
            old_val = old_db[key]
            if old_val.get("Tier") != new_val.get("Tier") or old_val.get("Score") != new_val.get("Score") or old_val.get("Commentary") != new_val.get("Commentary"):
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
    print(f"==================================================")
    print(f" Tier List Scraper & Auto-Publisher [{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}]")
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

    if not changes:
        print("[+] No changes detected in tier lists. Everything is up-to-date!")
        sys.exit(0)

    print(f"\n[!] Detected {len(changes)} changes:")
    for c in changes[:20]:
        print(f"  {c}")
    if len(changes) > 20:
        print(f"  ... and {len(changes) - 20} more changes.")

    print("\n[*] Step 2: Updating JSON database files (Sorted Alphabetically)...")
    with open(WORKSHOP_JSON, "w") as f:
        json.dump(new_db, f, indent=2, sort_keys=True)
    with open(EMBEDDED_JSON, "w") as f:
        json.dump(new_db, f, indent=2, sort_keys=True)

    if os.path.exists(PROTON_USER_DIR):
        with open(PROTON_CACHE_JSON, "w") as f:
            json.dump(new_db, f, indent=2, sort_keys=True)
        print(f"[+] Updated proton disk cache: {PROTON_CACHE_JSON}")

    print("\n[*] Step 3: Running unit test suite...")
    run_command("distrobox enter dotnet-dev -- dotnet test SlayTheSpireOverlay.slnx")

    print("\n[*] Step 4: Compiling & Publishing mod assembly...")
    run_command(f"distrobox enter dotnet-dev -- dotnet publish src/SlayTheSpireOverlay.Godot/SlayTheSpireOverlay.Godot.csproj -c Release -o '{LOCAL_MOD_DIR}'")

    print("\n[*] Step 5: Committing and pushing changes to GitHub...")
    commit_msg = f"Auto-update tier lists [{datetime.now().strftime('%Y-%m-%d %H:%M')}]"
    run_command("git add workshop/baalorlord_tiers.json src/SlayTheSpireOverlay.Godot/baalorlord_tiers.json")
    run_command(f"git commit -m '{commit_msg}'")
    run_command("git push")

    print("\n==================================================")
    print(" SUCCESS! Tier lists updated, rebuilt, and pushed!")
    print("==================================================")

if __name__ == "__main__":
    main()
