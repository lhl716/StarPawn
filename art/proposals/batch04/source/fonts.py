"""Resolve system fonts for review sheets; runtime artwork embeds no fonts."""
import os
from pathlib import Path
from PIL import ImageFont


def review_font(size, zh=False):
    override = os.environ.get('STARPAWN_ZH_FONT' if zh else 'STARPAWN_EN_FONT')
    candidates = ([override] if override else []) + (
        ['/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc',
         'C:/Windows/Fonts/msyh.ttc', 'C:/Windows/Fonts/simhei.ttf'] if zh else
        ['/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 'C:/Windows/Fonts/arial.ttf'])
    for path in candidates:
        if Path(path).is_file():
            return ImageFont.truetype(path, size)
    raise RuntimeError('No review font found. Set STARPAWN_ZH_FONT or STARPAWN_EN_FONT.')
