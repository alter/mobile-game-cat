#!/usr/bin/env python3
"""Собирает из выбранного значка два слоя для Android.

Зачем. Android рисует значок двумя слоями и накладывает на них маску своей
формы — круг, квадрат со скруглением, «капля»: у каждого лаунчера она своя.
Видна только середина холста, примерно 66 %, а при наклоне телефона слои ещё
и расходятся. Значок, положенный в оба слоя целиком, теряет уши: это и было
видно на устройстве 28.08.

Делаем так, как задумано платформой:

  фон  — сплошная заливка цветом, взятым с края самого значка;
  верх — кот, уменьшенный до 62 % холста и положенный в середину.

62 %, а не 66: остаётся запас на смещение при наклоне.

    .venv/bin/python tools/icons/make_adaptive.py game/Assets/Art/icons/icon_3.png

Пишет рядом два файла с окончаниями `_bg` и `_fg`. Их подхватывает
`Assets/Editor/SetAppIcon.cs`. Меняете значок — запускаете это заново, иначе
круглый значок и квадратный покажут разных котов.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

SAFE = 0.62


def build(path: Path) -> tuple[Path, Path]:
    src = Image.open(path).convert("RGB")
    if src.width != src.height:
        raise SystemExit(f"{path.name}: значок должен быть квадратным, а он {src.width}x{src.height}")

    a = np.array(src)
    edge = np.concatenate([
        a[:8].reshape(-1, 3), a[-8:].reshape(-1, 3),
        a[:, :8].reshape(-1, 3), a[:, -8:].reshape(-1, 3),
    ])
    # Медиана, а не среднее: одна светлая деталь, задевшая край, не должна
    # утащить за собой цвет всей заливки.
    bg = tuple(int(x) for x in np.median(edge, axis=0))

    n = src.width
    safe = int(n * SAFE)
    fg = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    fg.paste(src.resize((safe, safe), Image.LANCZOS), ((n - safe) // 2, (n - safe) // 2))

    fg_path = path.with_name(path.stem + "_fg.png")
    bg_path = path.with_name(path.stem + "_bg.png")
    fg.save(fg_path)
    Image.new("RGBA", (n, n), bg + (255,)).save(bg_path)
    return fg_path, bg_path


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    path = Path(sys.argv[1])
    fg, bg = build(path)
    print(f"{fg.name} и {bg.name} готовы, кот занимает {SAFE:.0%} холста")


if __name__ == "__main__":
    main()
