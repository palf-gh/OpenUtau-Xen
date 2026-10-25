# Xen localisation

The application registers every language represented in the upstream Strings
directory: en-US, de-DE, es-ES, es-MX, fi-FI, fr-FR, id-ID, it-IT, ja-JP, ko-KR,
nl-NL, pl-PL, pt-BR, ro-RO, ru-RU, th-TH, tr-TR, vi-VN, zh-CN and zh-TW.
Romanian's existing dictionary was present but unregistered; Xen registers it.

Each `Strings/Xen.<culture>.axaml` contains the same 100 Xen-owned keys. The
British-English master retains the upstream en-US resource identifier. Existing
Crowdin-managed dictionaries are not edited. `XenStrings.SetLanguage` removes
previous overlays and merges the English fallback followed by the selected
language. Existing menus use dynamic resources; modal controls use the active
language at construction, consistent with existing application dialogs.

Covered copy includes tuning/reference fields, scale and MOS operations, notation
and layout selectors, file dialogs, MIDI lifecycle, export warnings and preset
names. Serialised tuning/notation identifiers, degree lists, Scala syntax and
user-authored names are never translated. Proper names such as Scala, KBM,
Stein–Zimmermann–Gould and Ups and Downs remain recognisable. Device names and
technical diagnostic details originate from their respective backends.

## Untranslated upstream strings

Many upstream dictionaries lack keys or keep the English text, notably for
features ported from OpenUtau Lunai (packages, transcription, themes, mix
finishing) and newer upstream menus. Xen covers these without editing the
Crowdin files: `Strings/XenUpstream.<culture>.axaml` holds translations for the
upstream keys a language still leaves untranslated, and `SetLanguage` merges it
last, after the upstream dictionary and the Xen overlays. There is no file for
en-US.

`tools/xen-upstream-strings.py` maintains them. `missing` lists, per language,
the keys that are absent or identical to English (only text containing a
lower-case word counts; `ui.fontfamily` is a setting and is left alone) and not
yet in `tools/xen-upstream-strings/<culture>.json`. `build` writes each overlay
from that JSON, keeping only keys upstream still leaves untranslated, so a
later Crowdin translation takes over automatically once it arrives. After an
upstream merge, run `missing`, translate the listed keys into the JSON, then run
`build`. Product and format names (DiffSinger, GAME, RMVPE, wavtool, oto
parameter names) stay as they are.

## Tests

`AppTest.StringsTest` loads every compiled overlay, checks exact key coverage,
non-empty values and identical formatting placeholders, then switches through
every registered language and verifies actual resource resolution. Upstream
overlays must use existing upstream keys with the same placeholders and line
count as en-US and keep any leading or trailing space the English has. Japanese
and Korean are checked to resolve overlay strings at run time. The test also
opens the tuning editor headlessly and checks its numerical control geometry.

Translations were authored during implementation, with checks for terminology
and placeholder consistency. They are not a claim of independent native-speaker
certification; specialist MOS/temperament terminology and long-label typography
remain suitable subjects for community review.
