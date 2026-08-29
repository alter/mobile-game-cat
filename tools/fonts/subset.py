#!/usr/bin/env python3
"""Обрезает шрифты Noto до тех знаков, которые игра действительно показывает.

Зачем это понадобилось. Игра не везла своего шрифта вовсе: в
`PanelSettings.asset` стояло `textSettings: {fileID: 0}`. На Android это сходило
с рук — Unity 6 молча берёт недостающие знаки из шрифтов самой системы, и 29.08
смотровой экран (`glyphs.txt`) показал, что рисуются все семнадцать языков.

На симуляторе iOS в тот же день:

  тайский                     ▢▢▢▢▢▢▢▢▢▢▢▢▢  — ни одного знака
  китайский упрощённый        房▢干▢了          — выпали 间 и 净

Причём тайский шрифт в системе **есть** (`Thonburi.ttc` лежит в образе
симулятора) — Unity просто до него не дошла. А китайские знаки берутся из
японского начертания, в котором упрощённых форм нет; оттого традиционный
китайский цел, а упрощённый дырявый.

Опираться на такую подстановку нельзя: она не описана, ведёт себя по-разному на
двух платформах и может измениться в любом обновлении. Свои знаки надо везти с
собой.

Везти целиком нельзя — четыре начертания CJK весят 22 МБ при пакете в 50. Но
игре нужно не всё: во всех таблицах вместе набирается несколько сотен знаков.
Обрезанные начертания весят вместе меньше сотни килобайт.

    .venv/bin/python tools/fonts/subset.py

Читает знаки прямо из `Copy*.cs` — не из заранее составленного списка, который
разойдётся с таблицами при первой же правке. Кладёт готовое в
`game/Assets/Resources/Fonts/`.

Лицензия. Noto распространяется по SIL Open Font License 1.1, которая прямо
разрешает и обрезку, и включение в приложение. Файл лицензии кладётся рядом с
шрифтами: OFL требует, чтобы он ехал вместе с ними.
"""
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
COPY_DIR = ROOT / "game" / "Assets" / "Shell"
OUT_DIR = ROOT / "game" / "Assets" / "Resources" / "Fonts"

# Откуда берём исходные начертания. Скачиваются отдельно — см. README рядом.
SOURCES = {
    "NotoSansThai-Regular.ttf": "тайский",
    "NotoSansArabic-Regular.ttf": "арабский",
    "NotoSansDevanagari-Regular.ttf": "деванагари (хинди)",
    "NotoSansSC-Regular.otf": "китайский упрощённый",
    "NotoSansTC-Regular.otf": "китайский традиционный",
    "NotoSansJP-Regular.otf": "японский",
    "NotoSansKR-Regular.otf": "корейский",
}

# Значение в таблице: ["ключ"] = "строка" либо продолжение через +.
VALUE = re.compile(r'"((?:[^"\\]|\\.)*)"')


def characters() -> set[str]:
    """Каждый знак из каждой таблицы Copy.

    Берём из всех файлов, включая латинские: вьетнамские и турецкие надстрочные
    знаки на глаз выглядят обычной латиницей, но это отдельные кодовые точки, и
    ошибиться тут дороже, чем положить лишнюю сотню байт.
    """
    chars: set[str] = set()
    files = sorted(COPY_DIR.glob("Copy*.cs"))
    if not files:
        raise SystemExit(f"не нашёл ни одного Copy*.cs в {COPY_DIR}")

    # И смотровой экран: его образцы и подписи в таблицах не встречаются, а
    # квадрат на экране, который для того и сделан, чтобы показывать квадраты,
    # прочтут как настоящую поломку. Так и вышло 29.08: подпись «简体中文»
    # вышла как «□体中文», потому что знака 简 нет ни в одной строке игры.
    glyph_check = ROOT / "game" / "Assets" / "View" / "GlyphCheckView.cs"
    if glyph_check.exists():
        files.append(glyph_check)

    for path in files:
        for raw in VALUE.findall(path.read_text(encoding="utf-8")):
            # \" и \\ внутри строки C# — на набор знаков не влияют, но пусть
            # текст будет тем же, что увидит игрок.
            chars.update(raw.replace('\\"', '"').replace("\\\\", "\\"))

    # То, что легко забыть и что видно на экране: пробел, цифры и скобки из
    # {0}, многоточие, средняя точка из map.legend, тире.
    chars.update(" 0123456789{}…·—–,.:;!?()'\"")
    return chars


def subset(source: Path, target: Path, text: str) -> tuple[int, int]:
    before = source.stat().st_size
    # --layout-features='*' обязателен. Без него из шрифта вылетают таблицы
    # GSUB/GPOS, а вместе с ними связность арабских букв и перестановка знаков
    # деванагари: файл станет меньше, а текст — неправильным.
    subprocess.run(
        [
            sys.executable, "-m", "fontTools.subset", str(source),
            f"--text={text}",
            "--layout-features=*",
            "--glyph-names",
            "--notdef-outline",
            "--recommended-glyphs",
            f"--output-file={target}",
        ],
        check=True, capture_output=True,
    )
    return before, target.stat().st_size


def main() -> None:
    src_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else Path.cwd()
    chars = characters()
    text = "".join(sorted(chars))
    print(f"знаков во всех таблицах: {len(chars)}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    total_before = total_after = 0
    for name, what in SOURCES.items():
        source = src_dir / name
        if not source.exists():
            print(f"  {name}: нет исходника, пропускаю ({what})")
            continue
        before, after = subset(source, OUT_DIR / name, text)
        total_before += before
        total_after += after
        print(f"  {name:32} {before//1024:6d} КБ → {after//1024:4d} КБ   {what}")

    if total_before:
        print(f"\nвсего {total_before//1024} КБ → {total_after//1024} КБ "
              f"({100 - total_after * 100 // total_before} % долой)")


if __name__ == "__main__":
    main()
