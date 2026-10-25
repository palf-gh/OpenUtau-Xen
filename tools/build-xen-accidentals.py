"""Builds OpenUtau/Assets/Fonts/XenAccidentals.otf from tools/fonts/BravuraText.otf.

Xen Accidentals is a Modified Version of Bravura Text under the SIL Open Font
License 1.1. "Bravura" is a Reserved Font Name, so the modified font has its own
name. Changes: only the SMuFL accidental glyphs (U+E260-U+E4FF) are kept; every
glyph is scaled towards a common height (so short symbols are not cramped,
within limits that keep relative sizes) and centred on the middle of a capital
letter;
U+266D/U+266E/U+266F map to the SMuFL flat, natural and sharp; the line metrics
are those of ordinary text, so the glyphs do not enlarge a text line; small
side bearings keep accidentals clear of neighbouring letters.

Run with Python 3 and fontTools: python tools/build-xen-accidentals.py
"""
import os

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.pens.transformPen import TransformPen
from fontTools.subset import Options, Subsetter
from fontTools.ttLib import TTFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "tools", "fonts", "BravuraText.otf")
TARGET = os.path.join(ROOT, "OpenUtau", "Assets", "Fonts", "XenAccidentals.otf")
FAMILY = "Xen Accidentals"
POSTSCRIPT = "XenAccidentals-Regular"
# Each glyph is scaled towards TARGET_HEIGHT units, between MIN_SCALE and MAX_SCALE of Bravura Text,
# and centred on CAP_MIDDLE: half the cap height of typical interface fonts.
TARGET_HEIGHT = 780
MIN_SCALE, MAX_SCALE = 1.3, 1.8
CAP_MIDDLE = 350
ASCENT, DESCENT = 1000, -250
# Side bearings so an accidental does not touch the letter before or after it.
PAD_LEFT, PAD_RIGHT = 60, 60
UNICODE_ALIASES = {0x266D: 0xE260, 0x266E: 0xE261, 0x266F: 0xE262}


def main():
    font = TTFont(SOURCE)
    options = Options()
    options.desubroutinize = True
    options.layout_features = []
    options.name_IDs = ["*"]
    options.notdef_outline = True
    subsetter = Subsetter(options)
    subsetter.populate(unicodes=range(0xE260, 0xE500))
    subsetter.subset(font)

    glyph_set = font.getGlyphSet()
    cmap = font.getBestCmap()

    cff = font["CFF "].cff
    top = cff.topDictIndex[0]
    charstrings = top.CharStrings
    hmtx = font["hmtx"]
    for name in font.getGlyphOrder():
        width, _ = hmtx[name]
        bounds = BoundsPen(glyph_set)
        glyph_set[name].draw(bounds)
        scale, shift = MIN_SCALE, 0
        if bounds.bounds:
            x_min, y_min, x_max, y_max = bounds.bounds
            height = max(1, y_max - y_min)
            scale = min(MAX_SCALE, max(MIN_SCALE, TARGET_HEIGHT / height))
            shift = CAP_MIDDLE - (y_min + y_max) / 2 * scale
        new_width = round(width * scale) + PAD_LEFT + PAD_RIGHT
        pen = T2CharStringPen(new_width, glyph_set)
        glyph_set[name].draw(TransformPen(pen, (scale, 0, 0, scale, PAD_LEFT, shift)))
        charstring = pen.getCharString(private=charstrings[name].private, globalSubrs=charstrings[name].globalSubrs)
        charstrings[name] = charstring
        box = BoundsPen(None)
        charstring.draw(box)
        hmtx[name] = (new_width, round(box.bounds[0]) if box.bounds else 0)

    for table in font["cmap"].tables:
        if table.isUnicode():
            for alias, target in UNICODE_ALIASES.items():
                table.cmap[alias] = cmap[target]

    hhea = font["hhea"]
    hhea.ascent, hhea.descent, hhea.lineGap = ASCENT, DESCENT, 0
    os2 = font["OS/2"]
    os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap = ASCENT, DESCENT, 0
    os2.usWinAscent, os2.usWinDescent = ASCENT, -DESCENT

    cff.fontNames = [POSTSCRIPT]
    top.FullName = FAMILY
    top.FamilyName = FAMILY
    names = font["name"]
    copyright = names.getDebugName(0) or ""
    for record in list(names.names):
        if record.nameID in (1, 3, 4, 6, 16, 17, 21, 22):
            names.removeNames(nameID=record.nameID)
    names.setName(copyright + " Modified as Xen Accidentals for OpenUtau Xen.", 0, 3, 1, 0x409)
    names.setName(FAMILY, 1, 3, 1, 0x409)
    names.setName("Regular", 2, 3, 1, 0x409)
    names.setName(POSTSCRIPT + ";OpenUtau Xen", 3, 3, 1, 0x409)
    names.setName(FAMILY, 4, 3, 1, 0x409)
    names.setName(POSTSCRIPT, 6, 3, 1, 0x409)
    font.save(TARGET)
    print(f"Wrote {TARGET} ({os.path.getsize(TARGET)} bytes)")


if __name__ == "__main__":
    main()
