import os
import sys
import tempfile
import unittest

# Add scripts directory to path
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "scripts"))
from update_tiers import (
    extract_changelog_section,
    build_workshop_change_note,
    get_manifest_version
)

SAMPLE_CHANGELOG = """# Changelog

## [v1.2.0] - 2026-10-18

### ⚙️ Feature Subheading
* **Setting A:** Some setting description.
* **Setting B:** Another description `with code`.

### 🔧 Fixes
- Bug fix 1 with **bold** text.
- Bug fix 2.

## [v1.1.0] - 2026-10-17

* Older release bullet.
"""

class TestUpdateTiers(unittest.TestCase):
    def setUp(self):
        self.temp_file = tempfile.NamedTemporaryFile(mode="w", delete=False, encoding="utf-8")
        self.temp_file.write(SAMPLE_CHANGELOG)
        self.temp_file.close()

    def tearDown(self):
        if os.path.exists(self.temp_file.name):
            os.remove(self.temp_file.name)

    def test_extract_changelog_section_matching_version(self):
        bullets = extract_changelog_section(self.temp_file.name, "v1.2.0")
        self.assertIsNotNone(bullets)
        self.assertIn("• Setting A: Some setting description.", bullets)
        self.assertIn("• Setting B: Another description with code.", bullets)
        self.assertIn("• Bug fix 1 with bold text.", bullets)
        self.assertIn("• Bug fix 2.", bullets)
        # Verify emoji headings and ### are not in bullets
        self.assertNotIn("⚙️", bullets)
        self.assertNotIn("###", bullets)

    def test_extract_changelog_section_version_without_v_prefix(self):
        bullets = extract_changelog_section(self.temp_file.name, "1.2.0")
        self.assertIsNotNone(bullets)
        self.assertIn("• Setting A: Some setting description.", bullets)

    def test_extract_changelog_section_missing_version(self):
        bullets = extract_changelog_section(self.temp_file.name, "v9.9.9")
        self.assertIsNone(bullets)

    def test_build_workshop_change_note_with_changelog(self):
        note = build_workshop_change_note("v1.2.0", self.temp_file.name, changes=[])
        self.assertTrue(note.startswith("v1.2.0 Release Notes:\n"))
        self.assertIn("• Setting A: Some setting description.", note)

    def test_build_workshop_change_note_fallback_without_matching_changelog(self):
        # Force without changes -> maintenance note
        note_force = build_workshop_change_note("v9.9.9", self.temp_file.name, changes=[], is_force=True)
        self.assertEqual("Misc bug fixes and maintenance updates.", note_force)

        # Changes present -> Tier List Update
        note_diff = build_workshop_change_note("v9.9.9", self.temp_file.name, changes=["ADRENALINE: A -> S"])
        self.assertIn("Tier List Update", note_diff)
        self.assertIn("ADRENALINE: A -> S", note_diff)

    def test_build_workshop_change_note_truncation(self):
        # Extremely small max_len to test truncation
        note = build_workshop_change_note("v1.2.0", self.temp_file.name, changes=[], max_len=40)
        self.assertTrue(len(note) <= 40)
        self.assertTrue(note.endswith("..."))

if __name__ == "__main__":
    unittest.main()
