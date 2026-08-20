import urllib.request
import re
import json
import os

lists = {
    "Colorless": "0a626c22-dc49-433e-ac6e-76cc9abf5684",
    "Defect": "5a512e04-4583-4a16-9271-d46864c6cb4c",
    "Silent": "6d61ea21-0552-4c49-8bb5-a5c15530fc00",
    "Necrobinder": "43d0b41f-7d6d-4ce9-928e-c1310a413983",
    "Regent": "0e6c1e23-bec6-4887-a9e0-dbf49ede974d",
    "Ironclad": "004de170-026a-4dd4-a280-3b904be0b5d6"
}

# Mapping tier names to numeric scores
tier_scores = {
    "S": 95.0,
    "A": 80.0,
    "B": 65.0,
    "C": 50.0,
    "D": 35.0,
    "F": 15.0
}

combined_data = {}

# Set User-Agent to emulate browser and prevent potential bot blocks
headers = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
}

for name, uuid in lists.items():
    url = f"https://sts2.untapped.gg/en/tier-list/{uuid}"
    print(f"Fetching {name} Tier List ({uuid})...")
    try:
        req = urllib.request.Request(url, headers=headers)
        with urllib.request.urlopen(req) as response:
            html = response.read().decode('utf-8')
        
        # 1. Parse all Next.js pushed string components to assemble full payload
        pushes = re.findall(r'self\.__next_f\.push\(\[1,"(.*?)"\]\)', html, re.DOTALL)
        assembled = ""
        for p in pushes:
            assembled += p.replace('\\"', '"').replace('\\n', '\n').replace('\\/', '/')
            
        # 2. Extract tier definitions: {"uuid":"...", "name":"...", "color":"..."}
        tier_defs = {}
        for m in re.finditer(r'\{"uuid":"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})","name":"([^"]+)"', assembled):
            t_uuid, t_name = m.group(1), m.group(2)
            tier_defs[t_uuid] = t_name
            
        # Fallback tier search in case of different formatting
        for m in re.finditer(r'"uuid":"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})".*?"name":"([^"]+)"', html):
            t_uuid, t_name = m.group(1), m.group(2)
            if len(t_name) < 30 and t_uuid not in tier_defs:
                tier_defs[t_uuid] = t_name

        # 3. Extract card mappings
        # Format: {"uuid":"...","tier":"<tier_uuid>",...,"commentary":"...","item_id":"...","card_id":"..."}
        items = []
        card_pattern = r'\{"uuid":"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}","tier":"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})",.*?"commentary":"(.*?)",.*?"item_id":"(.*?)","card_id":"(.*?)"'
        for m in re.finditer(card_pattern, assembled):
            items.append((m.group(4), m.group(1), m.group(2)))
            
        # Fallback card search
        if not items:
            card_pattern = r'"tier":"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})".*?"commentary":"(.*?)".*?"card_id":"([^"]+)"'
            for m in re.finditer(card_pattern, html):
                items.append((m.group(3), m.group(1), m.group(2)))

        print(f"-> Found {len(items)} cards.")
        
        # 4. Map and store in combined structure
        for card_id, t_uuid, commentary in items:
            tier_letter = tier_defs.get(t_uuid, "C")
            # In case the tier name is long (e.g. custom description instead of letter), grab first char
            tier_letter = tier_letter[0].upper() if tier_letter else "C"
            if tier_letter not in tier_scores:
                tier_letter = "C" # Default fallback
            score = tier_scores[tier_letter]
            
            # Clean up escape slashes in commentary
            clean_commentary = commentary.encode().decode('unicode-escape', errors='ignore') if '\\u' in commentary else commentary
            clean_commentary = clean_commentary.replace('\\"', '"').replace('\\n', '\n').strip()

            combined_data[card_id] = {
                "CardId": card_id,
                "Tier": tier_letter,
                "Score": score,
                "Commentary": ""
            }
    except Exception as e:
        print(f"Error scraping {name}: {e}")

# Save the final results to the workshop folder
output_path = "/var/home/nickmarc/repos/SlayTheSpireOverlayLinux/workshop/baalorlord_tiers.json"
with open(output_path, "w", encoding="utf-8") as f:
    json.dump(combined_data, f, indent=2, ensure_ascii=False)

print(f"\n[+] Successfully saved {len(combined_data)} cards to {output_path}")
