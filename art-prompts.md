# Наказы на порождение графики — «Спасённый котёнок»

Дата: 25 августа 2026
Рабочее приложение к `art-brief.md`. Там — что и зачем, здесь — дословные наказы
на каждый актив, с отрицательной частью и оговорками.

---

## 0. Как этим пользоваться

Каждый наказ собирается из трёх частей:

```
[БАЗОВЫЙ СТИЛЬ] + [ПРЕДМЕТ И ЕГО ОСОБЕННОСТИ] + [КАДР]
```

и всегда сопровождается **отрицательной частью**. Отрицательная часть общая для
всего набора и не меняется — она нужна, чтобы стиль не разъезжался между
заходами.

Три правила, без которых набор развалится:

1. **Весь набор порождается за один заход, одним и тем же значением seed-строки
   и одной моделью.** Стиль расходится именно между сессиями, а не внутри одной.
2. **Первый удачный предмет становится референсом** для всех последующих:
   модели, поддерживающие референсное изображение, получают его на вход. Это
   сильнее любых слов в наказе.
3. **Что нейросеть порождать не должна** — маски кота и парные состояния комнат.
   Почему — в разделах 4 и 5. Их делают правкой готового изображения, иначе они
   не совпадут.

### Английский или русский

Наказы даны по-английски: все проверенные модели порождения изображений заметно
устойчивее на английском, и весь стилевой словарь отрасли на нём. Русские
пояснения — для человека, в наказ не идут.

---

## 1. Палитра

Взята из уже написанного прототипа (`build/playtest/index.html`), чтобы графика
совпала с тем, что уже собрано, а не наоборот.

| Роль | Название в наказе | Код | Где |
|---|---|---|---|
| фон, «чистое» | warm cream | `#F4EAD8` | фон экрана, чистые комнаты |
| дерево | light oak | `#C9A97C` | предметы группы «дерево» |
| полка | pale sand | `#E8D9BD` | полка, подложки |
| обводка и текст | dark walnut | `#4A3B28` | обводка всего |
| кремовый | soft cream | `#F0E2C6` | предметы |
| персиковый | muted peach | `#E8B79A` | предметы |
| мятный | dusty mint | `#A8C9B5` | предметы |
| пыльно-голубой | dusty blue | `#9DB3C4` | предметы |
| тёпло-серый | warm grey | `#B0A79B` | предметы |
| грязь, средняя | muddy taupe | `#6B6055` | грязные комнаты |
| грязь, тёмная | dull umber | `#55493D` | грязные комнаты, тени |

Правило по обводке: **одна толщина на весь набор**, примерно 3% от меньшей
стороны кадра, цвет `#4A3B28`, но не чёрный и не резкий — мягкая, чуть размытая
по краю.

Правило по свету: **источник всегда сверху-слева**, тень падает вправо-вниз,
мягкая, без резкой границы, прозрачностью около 25%. Никаких вторых источников,
контрового света и бликов-звёздочек.

---

## 2. Базовые блоки

### Положительный, идёт в начало каждого наказа

```
soft 3D cartoon render, three-quarter top-down view at 30 degrees,
rounded shapes with no sharp corners, thick soft dark-walnut outline,
volume from soft diffused light coming from the upper left,
soft shadow falling to the lower right at 25% opacity,
warm muted palette of cream, peach, mint and light oak,
matte surfaces, gentle ambient occlusion where forms meet,
cozy children-book illustration feel but not childish,
clean flat single-colour background, subject centred in frame
```

### Отрицательный, идёт в каждый наказ без изменений

```
pixel art, voxel, low poly, flat vector, line art, sketch, watercolour,
photorealistic, photograph, 3D studio render with reflections,
neon, glow, bloom, lens flare, rim light, hard specular highlights,
high contrast, dark background, black background, dramatic lighting,
saturated colours, acid colours, pink glitter, sparkles, stars,
kawaii, chibi, anime, big shiny anime eyes, human facial expression,
text, letters, numbers, watermark, signature, logo, UI elements,
frame, border, vignette, drop shadow box, gradient background,
multiple objects, cropped subject, subject touching frame edge,
busy background, clutter behind subject, cast shadow on background wall
```

Часть отрицательного блока повторяет то, что и так сказано в положительном.
Это намеренно: модели порождения слушают отрицательную часть охотнее.

### Кадр, добавляется в конец

Для предметов:

```
single object, centred, occupying 80% of the frame,
10% empty margin on every side, plain #F4EAD8 background
```

Для комнат:

```
interior view, vertical composition, camera at standing height,
bottom third of the frame left empty and uncluttered
```

---

## 3. Тридцать предметов кучи

### Раскладка по семействам и цветам

Цвет и семейство намеренно **не совпадают**: пять круглых предметов — пяти
разных цветов. Тогда любая выборка из десяти видов различима хотя бы по одной
оси. Это не украшательство: в прототипе два вида получили один цвет, и поле
стало нечитаемым.

| № | Файл | Семейство | Цвет | Предмет |
|---|---|---|---|---|
| 1 | `prop_yarn` | круглое | soft cream | клубок пряжи |
| 2 | `prop_ball` | круглое | muted peach | детский мяч |
| 3 | `prop_plate` | круглое | dusty mint | тарелка |
| 4 | `prop_clock` | круглое | light oak | будильник |
| 5 | `prop_spool` | круглое | dusty blue | катушка ниток |
| 6 | `prop_bottle` | высокое | muted peach | бутылка |
| 7 | `prop_vase` | высокое | dusty mint | ваза |
| 8 | `prop_lamp` | высокое | light oak | настольная лампа |
| 9 | `prop_jar` | высокое | dusty blue | банка с крышкой |
| 10 | `prop_candle` | высокое | warm grey | свеча в подсвечнике |
| 11 | `prop_book` | плоское | dusty mint | книга |
| 12 | `prop_box` | плоское | light oak | картонная коробка |
| 13 | `prop_tray` | плоское | dusty blue | поднос |
| 14 | `prop_board` | плоское | warm grey | разделочная доска |
| 15 | `prop_rug` | плоское | soft cream | свёрнутый коврик |
| 16 | `prop_suitcase` | угловатое | light oak | чемодан |
| 17 | `prop_crate` | угловатое | dusty blue | деревянный ящик |
| 18 | `prop_frame` | угловатое | warm grey | рамка для фото |
| 19 | `prop_mirror` | угловатое | soft cream | зеркальце |
| 20 | `prop_casket` | угловатое | muted peach | шкатулка |
| 21 | `prop_keys` | ветвистое | dusty blue | связка ключей |
| 22 | `prop_scissors` | ветвистое | warm grey | ножницы |
| 23 | `prop_hanger` | ветвистое | soft cream | вешалка |
| 24 | `prop_fork` | ветвистое | muted peach | вилка |
| 25 | `prop_comb` | ветвистое | dusty mint | гребень |
| 26 | `prop_pillow` | мягкое | warm grey | подушка |
| 27 | `prop_cloth` | мягкое | soft cream | тряпка |
| 28 | `prop_scarf` | мягкое | muted peach | шарф |
| 29 | `prop_mitten` | мягкое | dusty mint | варежка |
| 30 | `prop_sack` | мягкое | light oak | мешок |

### Шаблон

```
[БАЗОВЫЙ] , <описание предмета>, main colour <цвет> <код>,
<особенность формы>, [КАДР ДЛЯ ПРЕДМЕТОВ]
```

Отрицательная часть — общая, без изменений.

### Тридцать наказов

Даны только средние части; базовый блок, кадр и отрицательную часть подставлять
из раздела 2.

| Файл | Средняя часть наказа |
|---|---|
| `prop_yarn` | `a ball of yarn, soft cream #F0E2C6, loose thread end curling to one side, visible strand grooves` |
| `prop_ball` | `a child's rubber ball, muted peach #E8B79A, one wide painted stripe, slightly scuffed` |
| `prop_plate` | `a ceramic dinner plate seen from above, dusty mint #A8C9B5, plain rim, one small chip` |
| `prop_clock` | `a round alarm clock, light oak #C9A97C body, blank face with no numbers, two small bells on top` |
| `prop_spool` | `a wooden thread spool, dusty blue #9DB3C4 thread, wound unevenly, wide flanges` |
| `prop_bottle` | `a glass bottle, muted peach #E8B79A tint, cork stopper, tall narrow neck` |
| `prop_vase` | `a ceramic vase, dusty mint #A8C9B5, bulbous body narrowing to the top, empty` |
| `prop_lamp` | `a small table lamp, light oak #C9A97C base, fabric shade, switched off` |
| `prop_jar` | `a storage jar with a lid, dusty blue #9DB3C4, straight sides, empty` |
| `prop_candle` | `a candle in a holder, warm grey #B0A79B holder, unlit wick, wax slightly melted` |
| `prop_book` | `a closed hardcover book lying flat, dusty mint #A8C9B5 cover, no title, worn corners` |
| `prop_box` | `a flat cardboard box, light oak #C9A97C, lid slightly ajar, empty` |
| `prop_tray` | `a serving tray, dusty blue #9DB3C4, shallow raised rim, two side handles` |
| `prop_board` | `a wooden cutting board, warm grey #B0A79B, rounded corners, small hanging hole` |
| `prop_rug` | `a rolled-up rug, soft cream #F0E2C6, tied with a band, seen from the side` |
| `prop_suitcase` | `an old suitcase lying flat, light oak #C9A97C, two latches, worn corner caps` |
| `prop_crate` | `a small wooden crate, dusty blue #9DB3C4, visible plank gaps, empty` |
| `prop_frame` | `an empty picture frame, warm grey #B0A79B, plain moulding, no picture inside` |
| `prop_mirror` | `a small hand mirror, soft cream #F0E2C6 handle, oval glass shown as flat pale surface with no reflection` |
| `prop_casket` | `a small trinket box, muted peach #E8B79A, hinged lid closed, tiny clasp` |
| `prop_keys` | `a bunch of three keys on a ring, dusty blue #9DB3C4, keys splayed apart` |
| `prop_scissors` | `a pair of scissors, warm grey #B0A79B handles, blades half open` |
| `prop_hanger` | `a clothes hanger, soft cream #F0E2C6, wide shoulders, hook curving to one side` |
| `prop_fork` | `a table fork, muted peach #E8B79A handle, four tines, lying flat` |
| `prop_comb` | `a hair comb, dusty mint #A8C9B5, wide teeth, one tooth missing` |
| `prop_pillow` | `a small cushion, warm grey #B0A79B, corner tassels, dented in the middle` |
| `prop_cloth` | `a crumpled cleaning cloth, soft cream #F0E2C6, soft folds, no pattern` |
| `prop_scarf` | `a knitted scarf loosely coiled, muted peach #E8B79A, visible knit texture, fringed ends` |
| `prop_mitten` | `a single knitted mitten, dusty mint #A8C9B5, thumb to one side, cuff ribbing` |
| `prop_sack` | `a small cloth sack, light oak #C9A97C, tied at the neck with cord, slumped` |

### Нюансы, которые легко упустить

**Никакой еды и ничего живого.** Дом заброшенный, хлам должен быть неодушевлённым
и несъедобным — иначе игрок начинает искать смысл в наборе.

**Ни одного предмета с текстом.** Книга без названия, часы без цифр, коробка без
надписи. Текст на плитке 52 точки превращается в грязь, и отрицательная часть его
уже запрещает — но в описании предмета его тоже не должно быть.

**Зеркало и стекло — без отражений.** Стекло рисуется как матовая светлая
поверхность. Отражение на плитке нечитаемо и ломает единство.

**Износ, но не разрушение.** Скол, потёртость, недостающий зуб у гребня — да.
Сломанное пополам, обугленное, покрытое плесенью — нет: аудитория пришла наводить
порядок, а не разгребать помойку.

### Приёмка

Уменьшить любые десять до 52 точек, положить рядом, показать постороннему:
уверенно ли он говорит, что это десять разных вещей. Не «красиво ли», а
«различимо ли».

---

## 4. Кот

### Общая мысль, которую надо держать в голове

Игрок сравнивает этого кота **со своим котом, который лежит рядом на диване.**
Отсюда всё остальное: пропорции настоящие, глаза чуть крупнее живых, но не
блюдца; никакой человеческой мимики, никаких бровей; шерсть читается фактурой, а
не отдельными волосками.

Кот проходит три состояния, и переход между ними — это **половина смысла игры**.
Разница должна читаться мгновенно, но это один и тот же зверь: не худой чёрный
котёнок в начале и пушистый рыжий в конце, а один кот, которому стало лучше.

| Состояние | Комнаты | Поза | Шерсть | Уши, хвост | Взгляд |
|---|---|---|---|---|---|
| 1 | 1–4 | сидит, сжавшись, в углу | свалявшаяся, торчит клоками, тусклая | уши прижаты, хвост обёрнут вокруг лап | смотрит вбок, не на зрителя |
| 2 | 5–8 | стоит, идёт | опрятная, ещё не блестит | уши подняты, хвост опущен но не поджат | смотрит на зрителя |
| 3 | 9–12 | лежит на подоконнике, лапы подобраны | гладкая, блестящая | уши расслаблены, хвост свободно лежит | глаза прикрыты, доволен |

### Поправка к прежней редакции

В `cat-shelter-mvp.md`, раздел 10, записано, что кот собирается из частей —
тело, голова, хвост, лапы. **Это неверно для нашего случая.** Части окупаются
только при оживлении, а в MVP анимации нет, и поза меняется от состояния к
состоянию целиком. Разбиение на части дало бы швы и втрое больше работы.

Правильно: **целый силуэт на каждое состояние**, а окрас накладывается слоями
поверх.

### Что порождается нейросетью, а что нет

Порождается: **шесть силуэтов** — три состояния × две длины шерсти.

**Маски не порождаются.** Маска узора и маска белых пятен обязаны совпадать с
основой точка в точку. Нейросеть этого не даст никогда: она перерисует кота
заново. Маски делаются **правкой готового силуэта** в редакторе — обвести
области, залить белым по чёрному. Это ручная работа, и её надо заложить в срок.

То же про длину шерсти: если породить «того же кота, но пушистого» отдельным
наказом, получатся два разных кота. Правильный путь — породить короткошёрстного,
а длинношёрстного получить правкой того же изображения либо порождением с
референсом.

### Наказы на шесть силуэтов

Общая часть для всех: базовый блок, кадр для предметов, отрицательная часть —
плюс дополнительно в отрицательную:

```
collar, clothing, accessories, human hands, background objects,
multiple cats, kitten and adult cat together, cat food, bowl
```

**Важно:** силуэты порождаются **обесцвеченными** — окрас накладывается кодом.

| Файл | Средняя часть наказа |
|---|---|
| `cat_1_short_base` | `a thin young short-haired cat sitting hunched in a corner, desaturated greyscale fur with no colour, matted uneven coat sticking out in tufts, ears flattened back, tail wrapped tightly around the front paws, looking to the side away from the viewer, realistic cat proportions, eyes only slightly larger than life, dull lifeless coat` |
| `cat_2_short_base` | `the same short-haired cat now standing and mid-step, desaturated greyscale fur with no colour, coat tidy but not yet glossy, ears upright, tail lowered but not tucked, looking directly at the viewer, calm and curious, realistic cat proportions` |
| `cat_3_short_base` | `the same short-haired cat lying on a windowsill with paws tucked under, desaturated greyscale fur with no colour, sleek glossy coat, ears relaxed, tail resting loosely alongside the body, eyes half closed and content, realistic cat proportions` |
| `cat_1_long_base` | `identical pose and framing to cat_1_short_base but long-haired, desaturated greyscale fur, matted clumped long coat, visible tangles, ruff around the neck` |
| `cat_2_long_base` | `identical pose and framing to cat_2_short_base but long-haired, desaturated greyscale fur, coat combed out, full tail plume` |
| `cat_3_long_base` | `identical pose and framing to cat_3_short_base but long-haired, desaturated greyscale fur, soft flowing coat, thick tail curled alongside` |

Слово «identical pose and framing» само по себе не сработает — на длинную шерсть
обязательно подавать короткошёрстный вариант как референсное изображение.

### Слои, которые делаются вручную

На каждый из шести силуэтов:

| Файл | Что нарисовать | Нюанс |
|---|---|---|
| `..._pattern_tabby` | полосы поперёк спины и боков, кольца на хвосте, «M» на лбу | полосы сужаются к животу, не доходят до груди |
| `..._pattern_bicolor` | нижняя половина тела и лапы | граница неровная, идёт по плечу и бедру |
| `..._pattern_calico` | три-четыре крупных неровных пятна | пятна несимметричны, одно обязательно захватывает ухо |
| `..._pattern_tuxedo` | манишка на груди, лапы, кончик хвоста | манишка каплей, сужается книзу |
| `..._pattern_pointed` | морда, уши, лапы, хвост | границы размытые, а не резкие |
| `..._mark_chest` | пятно на груди | овальное, смещено от середины |
| `..._mark_paws` | «носочки» на всех четырёх лапах | разной высоты, задние выше |
| `..._mark_face` | пятно на морде | асимметричное, захватывает один глаз или нос |
| `..._eyes` | только радужки, без век и белка | миндалевидные, зрачок вертикальный |

Маски строго чёрно-белые, сглаживание края не больше двух точек, за силуэт
основы не выходят.

**Отдельный нюанс про белого кота.** Белые пятна на белом коте не видны. Значит
маски пятен рисуются так, чтобы читаться обводкой и мягкой тенью, а не только
заливкой: край пятна получает чуть более тёмный контур того же оттенка.

### Приёмка

Показать шесть силуэтов постороннему человеку: **это один кот или разные?**
Ответ «разные» — не принято, сколько бы усилий ни вложено.

---

## 5. Двенадцать комнат

### Главный нюанс: пару нельзя породить двумя наказами

Если породить «грязную гостиную», а потом «чистую гостиную» — получатся две
разные гостиные. Окно переедет, диван сменит форму, и вся сила пары «было —
стало» пропадёт.

Правильный порядок:

1. Породить **чистую** комнату — она сложнее и задаёт устройство.
2. Грязную получить **правкой того же изображения**: приглушить в серо-бурое,
   добавить пыль, отставшие обои, разбросанный хлам. Либо порождением с чистой
   комнатой как референсом и наказом «the same room, neglected».

Чистая порождается первой намеренно: испортить проще, чем прибрать.

### Общая часть наказов

Базовый блок, кадр для комнат, отрицательная часть — плюс дополнительно
в отрицательную:

```
people, cat, animals, modern electronics, television, computer,
open flame, mould, insects, rubbish bags, broken glass, decay, rot,
text on walls, posters with writing
```

Кот и хлам накладываются кодом поверх, в самой комнате их быть не должно.

### Чистые комнаты

Средние части наказа; каждая начинается с базового блока.

| Файл | Средняя часть |
|---|---|
| `room_01_clean` | `a small tidy entrance hall, coat hooks on the wall, a bench with a cushion, a round mirror, morning light through the door glass, warm and welcoming` |
| `room_02_clean` | `a tidy cottage kitchen, open shelves with plain crockery, a kettle on the stove, checked curtain, sunlight on the counter` |
| `room_03_clean` | `a tidy living room, a soft sofa with cushions, a low table, a rug, tall window with light curtains, warm afternoon light` |
| `room_04_clean` | `a tidy bedroom, a made bed with a folded quilt, a bedside table with a lamp, a small window, soft calm light` |
| `room_05_clean` | `a tidy child's room, a low bed, a shelf of toys neatly arranged, a small rug, bright cheerful light` |
| `room_06_clean` | `a tidy study, a wooden desk, a chair, a bookshelf with upright books, a desk lamp, quiet focused light` |
| `room_07_clean` | `a tidy bathroom, a claw-foot tub, folded towels on a rack, a small window with frosted glass, clean bright light` |
| `room_08_clean` | `a tidy pantry, wooden shelves with jars and baskets in rows, a step stool, cool even light` |
| `room_09_clean` | `a tidy attic, sloped ceiling, a round window, a few neatly stacked boxes, a rocking chair, dusty golden light` |
| `room_10_clean` | `a tidy veranda, wicker chair, potted plants, wooden railing, view of green outside, bright open light` |
| `room_11_clean` | `a tidy corridor, a runner rug, framed empty pictures on the wall, doors along one side, soft even light` |
| `room_12_clean` | `a tidy loft room under the roof, a window seat with cushions, low bookshelf, warm evening light through a skylight` |

### Грязные варианты

Правка чистой комнаты. Наказ для порождения с референсом:

```
[БАЗОВЫЙ] , the same room, neglected and long abandoned,
overall colour shifted to muddy taupe #6B6055 and dull umber #55493D,
dim grey light instead of warm light, dust in the air,
wallpaper peeling at the seams, cobwebs in the upper corners,
furniture out of place and covered with dust sheets,
scattered clutter on the floor, curtains sagging,
identical camera angle, identical furniture layout, identical window position,
[КАДР ДЛЯ КОМНАТ]
```

Ключевые слова здесь — три последних: **тот же ракурс, та же мебель, то же
окно.** Без них пара разъедется.

### Нюансы

**Заброшено, но не разрушено.** Пыль, паутина, отставшие обои — да. Плесень,
дыры в полу, битое стекло, мусорные мешки — нет: аудитория пришла наводить
уют, а не работать на расчистке аварийного жилья. Это же в отрицательной части.

**Нижняя треть кадра пустая.** Там будут полка и куча. В чистой комнате туда
попадёт пол и, может, край ковра; ничего важного — окон, мебели, кота — там
быть не должно.

**Свет — единственная переменная, которую нельзя экономить.** Разница «было —
стало» на две трети делается светом, а не хламом. Грязная комната серо-бурая и
тусклая, чистая — тёплая и светлая. Если это не так, приёмка на 200×400 не
пройдёт.

**Порядок комнат.** Чердак, веранда и мансарда — в конец, они самые уютные, и
на них приходятся третье состояние кота и последние награды.

---

## 6. Остальные активы

### Болванка — предмет под завалом

```
[БАЗОВЫЙ] , an unidentifiable object hidden under a dust sheet,
warm grey #B0A79B cloth draped over an unknown shape,
soft folds, a little dust, the shape underneath unreadable,
calm and quiet, not spooky, [КАДР ДЛЯ ПРЕДМЕТОВ]
```

В отрицательную дополнительно: `ghost, skull, face, eyes, anything recognisable
under the cloth`.

Нюанс: этих плиток на экране бывает по тридцать штук. Она должна быть **тихой** —
минимум деталей, ровный тон, иначе поле зарябит.

### Запертый предмет

```
[БАЗОВЫЙ] , a length of rough twine wound crosswise several times,
warm grey #B0A79B cord, tied in a simple knot at the centre,
rendered as a standalone overlay on transparent background,
nothing underneath, [КАДР ДЛЯ ПРЕДМЕТОВ]
```

Это **накладка поверх обычного предмета**, поэтому в середине должно оставаться
свободное место — под верёвкой предмет должен угадываться. В отрицательную:
`chain, padlock, metal, rust, prison bars`. Замок и цепь читаются как наказание,
а у нас игра про заботу.

### Награды

```
reward_bowl:
[БАЗОВЫЙ] , a ceramic cat bowl, dusty mint #A8C9B5,
low and wide, empty, a small paw print painted on the side,
slightly nicer and cleaner than ordinary household objects,
[КАДР ДЛЯ ПРЕДМЕТОВ]

reward_blanket:
[БАЗОВЫЙ] , a small folded blanket for a cat, muted peach #E8B79A,
soft knitted texture, neatly folded in three, one corner turned over,
slightly nicer and cleaner than ordinary household objects,
[КАДР ДЛЯ ПРЕДМЕТОВ]
```

В отрицательную обязательно: `glow, sparkle, magic aura, rarity border,
star, badge, plus sign, number`. Награды **не должны выглядеть усилителем** —
как только миска начнёт светиться, забота превратится в расчёт снаряжения.

### Значок приложения, пять вариантов

Один наказ, пять заходов с разной средней частью:

| Вариант | Средняя часть |
|---|---|
| 1 | `close-up of a content cat face, warm cream background, no objects` |
| 2 | `a cat sitting inside a cosy cleaned room seen through a doorway` |
| 3 | `a cat peeking out from behind a cardboard box` |
| 4 | `split composition, dull grey clutter on the left, warm tidy room with a cat on the right` |
| 5 | `a cat curled asleep on a folded blanket, seen from above` |

Кадр другой: `square composition, fills the entire frame, no margins,
no rounded corners, no transparency`. Скругление накладывает система.

Приёмка — опрос десяти человек: **на какой из пяти вы бы нажали.** Не «какой
красивее».

### Карта дома

```
map_background:
[БАЗОВЫЙ] , a cutaway view of a small two-storey house,
twelve empty rooms arranged in a grid inside the walls,
plain interior, roof and outer walls in light oak #C9A97C,
rooms empty and unfurnished, seen straight on, [КАДР ДЛЯ КОМНАТ]
```

Клетки комнат — три состояния, различимые издали: `dirty` тёмная серо-бурая,
`partial` наполовину светлая с ясной границей, `clean` тёплая светлая.

Нюанс: различие делается **светлотой, а не оттенком.** Карту смотрят мельком и
целиком; разница по цвету при таком размере не читается, разница по светлоте
читается всегда.

### Рамка карточки «было — стало»

```
[БАЗОВЫЙ] , a simple square photo frame divided into two equal halves
by a thin vertical line, light oak #C9A97C moulding, both halves empty,
rendered as an overlay on transparent background, [КАДР ДЛЯ ПРЕДМЕТОВ]
```

Место под имя кота **не предусматривать** — имя на публичной карточке решено не
выносить.

---

## 7. Порядок и приёмка

Порождать в этом порядке, и каждый шаг принимать до следующего:

1. **Пилот:** три предмета из разных семейств, один кот в состояниях 1 и 3, одна
   комната парой. Это ответ на вопрос «получится ли вообще», и он нужен раньше
   рекламных роликов.
2. Тридцать предметов одним заходом.
3. Болванка и накладка.
4. Шесть силуэтов кота, потом маски вручную.
5. Пять значков.
6. Карта дома.
7. Двенадцать комнат: сначала все чистые, потом все грязные.
8. Награды и рамка.

Отбор после каждого захода **ручной**. Нейросеть выдаёт годное примерно в
половине случаев, и рассчитывать надо на два-три захода на предмет.

Три проверки, которые нельзя поручить машине и нельзя поручить себе:

- десять предметов при 52 точках — различимы ли (посторонний);
- шесть силуэтов кота — один кот или разные (посторонний);
- пара комнат на 200×400 за полсекунды — какая чистая (посторонний).

Автор всегда видит замысел, а игрок видит картинку.
</content>
