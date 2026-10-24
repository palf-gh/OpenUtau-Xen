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

`AppTest.StringsTest` loads every compiled overlay, checks exact key coverage,
non-empty values and identical formatting placeholders, then switches through
every registered language and verifies actual resource resolution. It also opens
the tuning editor headlessly and checks its numerical control geometry.

Translations were authored during implementation, with checks for terminology
and placeholder consistency. They are not a claim of independent native-speaker
certification; specialist MOS/temperament terminology and long-label typography
remain suitable subjects for community review.
