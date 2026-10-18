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
WORKSHOP_META_JSON = os.path.join(REPO_DIR, "workshop", "workshop.json")
WORKSHOP_CONTENT_DIR = os.path.join(REPO_DIR, "workshop", "content")
WORKSHOP_UPLOADER_DIR = os.path.join(REPO_DIR, "workshop", "uploader")
MANIFEST_PATH = os.path.join(REPO_DIR, "src", "SlayTheSpireOverlay.Godot", "SlayTheSpireOverlay.Godot.json")
CHANGELOG_PATH = os.path.join(REPO_DIR, "CHANGELOG.md")

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

# Official Untapped.gg Companion STS2 Tier List UUIDs (reverse-engineered from Core-Ba2NpoJ9.js)
DEFAULT_TIER_LIST_UUIDS = {
    "Baalorlord": [
        "004de170-026a-4dd4-a280-3b904be0b5d6",  # ironclad
        "6d61ea21-0552-4c49-8bb5-a5c15530fc00",  # silent
        "0e6c1e23-bec6-4887-a9e0-dbf49ede974d",  # regent
        "43d0b41f-7d6d-4ce9-928e-c1310a413983",  # necrobinder
        "5a512e04-4583-4a16-9271-d46864c6cb4c",  # defect
        "0a626c22-dc49-433e-ac6e-76cc9abf5684",  # colorless (cards, relics, potions)
    ],
    "Jorbs": [
        "6b3390b7-bcbc-4ebc-ae78-ae9a400c66dc",  # ironclad
        "2cd72d4b-eeb4-42ab-89ca-4ac682137672",  # silent
        "c47dbfda-4983-4107-8c9c-c33d5ae32b23",  # regent
        "3ae661e6-6190-4a45-b00e-63e8400fff94",  # necrobinder
        "5f8d3e04-1f50-4b56-b51a-d8650b000ce3",  # defect
        "81684acf-26a7-4e44-a649-84f4c2824ec0",  # colorless
    ],
}

def fetch_tier_list_api(uid, game_version="public"):
    url = f"https://api.sts2.untapped.gg/api/v1/tier-lists/{uid}/resolve/?game_version={game_version}"
    raw_json = fetch_url(url)
    payload = json.loads(raw_json)
    tier_list = payload.get("tier_list", {})
    list_name = sanitize_text(tier_list.get("name", "Tier List"))
    results = {}

    for tier in tier_list.get("tiers", []):
        tier_name = tier.get("name", "N/A")
        score = TIER_SCORES.get(tier_name, 0.0)

        for card in tier.get("cards", []):
            item_id = card.get("card_id")
            if not item_id:
                continue
            commentary = sanitize_text(card.get("commentary") or "")
            if not commentary:
                commentary = sanitize_text(f"{item_id.replace('_', ' ').title()} [{list_name}] - Rated '{tier_name}'.")
            results[item_id] = {
                "CardId": item_id,
                "Tier": tier_name,
                "Score": score,
                "Commentary": commentary,
            }

        for group_key in ("relics", "potions"):
            for item in tier.get(group_key) or []:
                item_id = item.get("item_id")
                if not item_id:
                    continue
                commentary = sanitize_text(item.get("commentary") or "")
                if not commentary:
                    commentary = sanitize_text(f"{item_id.replace('_', ' ').title()} [{list_name}] - Rated '{tier_name}'.")
                results[item_id] = {
                    "CardId": item_id,
                    "Tier": tier_name,
                    "Score": score,
                    "Commentary": commentary,
                }

    return list_name, results

def scrape_untapped_tierlists(author="Baalorlord"):
    raw_data = {}

    print(f"[*] Step 1a: Resolving official Companion API tier lists for {author}...")
    for uid in DEFAULT_TIER_LIST_UUIDS.get(author, []):
        try:
            list_name, items = fetch_tier_list_api(uid)
            raw_data.update(items)
            print(f"    - Resolved API '{list_name}' ({uid}): {len(items)} items.")
        except Exception as ex:
            print(f"    [!] API resolve fallback for {uid}: {ex}")

    print("[*] Step 1b: Discovering additional web tier lists on Untapped.gg...")
    index_url = f"https://sts2.untapped.gg/en/tier-lists?creator={author}"
    try:
        index_html = fetch_url(index_url)
        uuids = sorted(list(set(re.findall(r"/en/tier-list/([a-f0-9-]{36})", index_html))))
        print(f"[+] Found {len(uuids)} tier list pages to scrape.")

        for uid in uuids:
            if uid in DEFAULT_TIER_LIST_UUIDS.get(author, []) and raw_data:
                continue
            page_url = f"https://sts2.untapped.gg/en/tier-list/{uid}"
            try:
                html = fetch_url(page_url)
                h1_match = re.search(r"<h1>(.*?)</h1>", html)
                raw_title = h1_match.group(1) if h1_match else "Tier List"
                page_title = sanitize_text(raw_title)

                items = parse_tier_list_page(html, raw_title)
                for k, v in items.items():
                    if k not in raw_data:
                        raw_data[k] = v
                print(f"    - Scraped '{page_title}': {len(items)} items.")
            except Exception as ex:
                print(f"[-] Error scraping {page_url}: {ex}")
    except Exception as ex:
        print(f"[!] Web index discovery skipped/failed: {ex}")

    print("[*] Step 1c: Fetching Event Commentary from Untapped.gg...")
    try:
        events_json = json.loads(fetch_url("https://sts2.untapped.gg/api/v1/commentary/events"))
        author_events = events_json.get(author) or events_json.get("Baalorlord") or {}
        if isinstance(author_events, dict):
            ev_count = 0
            for ev_id, comm in author_events.items():
                if comm and isinstance(comm, str):
                    norm_key = f"EVENT_{ev_id.strip().upper()}"
                    raw_data[norm_key] = {
                        "CardId": ev_id.strip().upper(),
                        "Tier": "Guide",
                        "Score": 0.0,
                        "Commentary": sanitize_text(comm),
                    }
                    ev_count += 1
            print(f"    - Fetched {ev_count} event commentary entries.")
    except Exception as ex:
        print(f"[!] Event commentary fetch skipped/failed: {ex}")

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

def get_manifest_version(manifest_path=MANIFEST_PATH):
    if os.path.exists(manifest_path):
        try:
            with open(manifest_path, "r", encoding="utf-8") as f:
                data = json.load(f)
                return data.get("version", "").strip()
        except Exception:
            pass
    return ""

def extract_changelog_section(changelog_path, version):
    if not os.path.exists(changelog_path) or not version:
        return None
    try:
        with open(changelog_path, "r", encoding="utf-8") as f:
            content = f.read()

        v_clean = version.lstrip("v").strip()
        if not v_clean:
            return None
        pattern = rf'##\s*\[v?{re.escape(v_clean)}\][^\n]*\n(.*?)(?=\n##\s*\[|\Z)'
        match = re.search(pattern, content, re.DOTALL)
        if not match:
            return None

        section_text = match.group(1).strip()
        bullets = []
        for line in section_text.splitlines():
            line = line.strip()
            if line.startswith(("* ", "- ")):
                bullet = re.sub(r'\*\*(.*?)\*\*', r'\1', line[2:]).strip()
                bullet = re.sub(r'`(.*?)`', r'\1', bullet).strip()
                if bullet:
                    bullets.append(f"• {bullet}")

        return "\n".join(bullets) if bullets else None
    except Exception:
        return None

def build_workshop_change_note(manifest_version, changelog_path, changes, is_force=False, max_len=950):
    changelog_bullets = extract_changelog_section(changelog_path, manifest_version) if manifest_version else None
    if changelog_bullets:
        prefix = f"{manifest_version} Release Notes:\n"
        note = prefix + changelog_bullets
        if changes:
            tier_summary = "\n\nTier List Updates:\n" + "\n".join(changes[:5])
            if len(note) + len(tier_summary) < max_len:
                note += tier_summary
        if len(note) > max_len:
            note = note[:max_len - 3] + "..."
        return note

    if is_force and not changes:
        return "Misc bug fixes and maintenance updates."

    summary_lines = changes[:10]
    if len(changes) > 10:
        summary_lines.append(f"... and {len(changes) - 10} more tier list updates.")
    note = f"Tier List Update [{datetime.now().strftime('%Y-%m-%d')}]:\n" + "\n".join(summary_lines)
    if len(note) > max_len:
        note = note[:max_len - 3] + "..."
    return note

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

    commit_msg = ""
    if os.path.exists(WORKSHOP_META_JSON):
        try:
            with open(WORKSHOP_META_JSON) as f:
                meta = json.load(f)

            meta["visibility"] = "private"

            manifest_v = get_manifest_version(MANIFEST_PATH)
            meta["changeNote"] = build_workshop_change_note(manifest_v, CHANGELOG_PATH, changes, is_force=args.force)

            if extract_changelog_section(CHANGELOG_PATH, manifest_v):
                commit_msg = f"Release {manifest_v} [{datetime.now().strftime('%Y-%m-%d %H:%M')}]"
            elif args.force and not changes:
                commit_msg = f"Misc bug fixes [{datetime.now().strftime('%Y-%m-%d %H:%M')}]"
            else:
                commit_msg = f"Auto-update tier lists [{datetime.now().strftime('%Y-%m-%d %H:%M')}]"

            with open(WORKSHOP_META_JSON, "w") as f:
                json.dump(meta, f, indent=2)
            print(f"[+] Updated changeNote in workshop.json (visibility={meta['visibility']})!")
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
    run_command("git add workshop/baalorlord_tiers.json workshop/workshop.json")
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
