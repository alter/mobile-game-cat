# 2D URP на мобильных (iOS): настройка URP Asset, Sprite Atlas, Sorting/batching, 2D-освещение, Canvas, типовые ошибки

Дата сбора: 2026-08-24
Версия стека проекта: Unity 6.3 LTS (6000.3.x), URP 2D Renderer, сборка под iOS.

## Кратко

- На мобильных стоит отключать: **HDR**, **MSAA** ("Anti Aliasing"), при отсутствии реальной необходимости — **Opaque Texture** и **Depth Texture** (обе создают дополнительные текстуры/проходы и расходуют bandwidth), а в 2D Renderer Data — **Depth/Stencil Buffer**, если не используется Sprite Mask. [docs.unity3d.com — universalrp-asset](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html), [docs.unity3d.com — 2DRendererData-overview](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/2DRendererData-overview.html)
- Для iOS рекомендованный формат сжатия текстур — **ASTC** (для устройств с чипом A8 (2014) и новее); для более старых устройств с A7 ASTC не поддерживается аппаратно и будет декомпрессироваться в рантайме — тогда как запасной вариант ETC/ETC2. [docs.unity3d.com — texture-choose-format-by-platform](http://docs.unity3d.com/6000.3/Documentation/Manual/texture-choose-format-by-platform.html)
- **Sprite Atlas** — обязателен для батчинга: без атласа каждый уникальный спрайт/текстура — это минимум отдельный draw call; официальная документация прямо говорит, что атлас позволяет Unity делать один draw call вместо нескольких. [docs.unity3d.com — atlas-introduction](https://docs.unity3d.com/6000.4/Documentation/Manual/sprite/atlas/atlas-introduction.html)
- **Sorting Layers** и **Order in Layer** управляют порядком отрисовки; спрайты с одинаковым материалом и одинаковыми настройками сортировки батчатся вместе — смена материала/текстуры/сортировки внутри "стопки" спрайтов ломает батч. [docs.unity3d.com — 2d-renderer-sorting](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-renderer-sorting.html)
- **2D-освещение (Light2D)** в URP — недешёвая функция на мобильном: по независимым практическим отчётам разработчиков, самостоятельная система на основе GPU (аналог Light2D) может стабильно потреблять около 1-2 мс на кадр даже без активных источников света, а связка Light2D + самодельные тени в одном из отчётов давала всего около 6 fps на телефоне до оптимизации. Официальная документация Unity прямо не даёт числовых порогов производительности Light2D. [github.com/simeonradivoev/Light2D](https://github.com/simeonradivoev/Light2D) (через WebSearch, не открыт напрямую — пометка ниже), [gamba04.itch.io devlog](https://gamba04.itch.io/my-personal-devlog/devlog/145714/2d-mobile-optimized-lighting-system) (через WebSearch)
- Для раскладки UI под разные соотношения сторон iPhone используется **Canvas Scaler** в режиме **Scale With Screen Size** с **Screen Match Mode** = "Match Width Or Height"; для вырезов/notch/safe zone — свойство **Screen.safeArea**, которое возвращает прямоугольник в пикселях с началом координат в левом нижнем углу. [docs.unity3d.com — script-CanvasScaler](https://docs.unity3d.com/Packages/com.unity.ugui@2.6/manual/script-CanvasScaler.html), [docs.unity3d.com — Screen-safeArea](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html)
- Самые часто упоминаемые в отчётах разработчиков причины просадки FPS в 2D на мобильном: **overdraw от прозрачных/полупрозрачных спрайтов** (фillrate-bound на мобильном GPU), слишком много draw call'ов из-за неатласированных текстур/материалов, полный ребилд Canvas при движении одного UI-элемента в общем Canvas со статикой. [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/)
- Для Sprite Atlas на мобильном рекомендуется явный выбор формата сжатия (None/Low/Normal/High Quality в общем диалоге; для iOS отдельно доступен ASTC через platform override) и осознанный выбор **Generate Mip Maps** — для 2D-игры с фиксированной или почти не масштабируемой камерой мипмапы обычно не нужны и тратят память впустую. [docs.unity3d.com — sprite-atlas-reference](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html)

## URP Asset: что выключать ради производительности на мобильном

Страница Unity Manual "Universal Renderer Asset" (URP Asset) открыта напрямую: [docs.unity3d.com — universalrp-asset](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html)

**HDR:**
> "Enable this to allow rendering in High Dynamic Range (HDR) by default for every camera in your scene."
Отключение HDR экономит производительность на слабом железе за счёт пропуска HDR-вычислений — для плоской 2D-головоломки без bloom/tone mapping HDR обычно не нужен.

**Anti Aliasing (MSAA):**
> "Use Multisample Anti-aliasing by default for every Camera in your scene while rendering."
Рекомендация — выставлять **Disabled**, чтобы пропустить MSAA-вычисления и снизить нагрузку на мобильном GPU. Для чистой 2D-сцены с ортографической камерой alias-артефакты обычно не так заметны, как в 3D, что дополнительно снижает ценность MSAA здесь.

**Opaque Texture:**
Создаёт `_CameraOpaqueTexture`, нужную для эффектов преломления/прозрачности, читающих цвет уже отрендеренной сцены. Параметр **Opaque Downsampling** позволяет выбрать None, 2x Bilinear, 4x Box или 4x Bilinear, чтобы снизить нагрузку на bandwidth на мобильном. Если в проекте нет шейдеров, которым нужен `_CameraOpaqueTexture` (например, distortion-эффектов), эту опцию стоит выключать целиком.

**Depth Texture:**
> "Enables URP to create a `_CameraDepthTexture`" для всех камер — расходует память/bandwidth на мобильном. Нужна только если задействованы эффекты, зависящие от глубины сцены (например, некоторые post-processing эффекты, soft particles). В 2D-головоломке без таких эффектов depth texture можно отключить.

**Render Scale:**
> "This slider scales the render target resolution...Use this when you want to render at a smaller resolution for performance reasons."
Инструмент подстройки итогового разрешения рендера — при просадках FPS на слабых устройствах можно снижать Render Scale ниже 1.0.

**Post-processing:**
Параметры **Grading Mode**, **LUT Size** и другие настройки post-processing прямо влияют на производительность на мобильном; более высокий LUT Size (по умолчанию 32) имеет, по формулировке документации, "potential cost of performance and memory use" — то есть его снижение экономит и то, и другое.

**Soft Shadows:**
Отдельно отмечено: мягкие тени "High impact on platforms that use tile-based rendering, such as mobile platforms and untethered XR platforms" — то есть на тайловых мобильных GPU (все современные iPhone) мягкие тени особенно дороги. Для 2D-игры тени, как правило, не нужны вовсе (кроме специфичных эффектов теней спрайтов через 2D Light Shadow Caster — см. раздел про 2D-освещение).

## 2D Renderer Data (специфичные настройки для 2D URP Renderer)

Помимо общего URP Asset, у 2D-проекта есть отдельный ассет **2D Renderer Data**, который управляет тем, как 2D Lights применяются к спрайтам. Страница Unity Manual "2D Renderer asset component reference for URP" открыта напрямую: [docs.unity3d.com — 2DRendererData-overview](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/2DRendererData-overview.html)

**HDR Emulation Scale:**
> "Sets the multiplier that Unity uses to emulate high-intensity lights on platforms that don't support HDR."
Значение стоит подбирать по максимальной яркости света в сцене, уменьшая при появлении цветового бандинга (banding).

**Depth/Stencil Buffer:**
> "Enables Unity rendering to the depth/stencil buffer. Disabling this property can improve performance, especially on mobile platforms."
Официальная рекомендация — отключать этот параметр, если проект не использует функции, требующие depth/stencil buffer (например, **Sprite Mask**). Для мобильной 2D-головоломки без масок или с масками, не требующими stencil, это прямой источник экономии производительности.

**Camera Sorting Layer Texture:**
Захватывает цветовой буфер камеры на определённой глубине sorting layer и передаёт его в шейдер как `CameraSortingLayerTexture` — нужен только кастомным шейдерам, которым требуется цвет уже отрисованной части сцены (например, эффекты преломления по спрайтам). Если такие шейдеры не используются — можно выбрать **Disabled**, чтобы не тратить лишний проход рендера.

**Downsampling для Camera Sorting Layer Texture** — те же четыре варианта, что и для Opaque Texture в основном URP Asset: **None** (полное разрешение), **2x Bilinear** (половинное разрешение с билинейной фильтрацией), **4x Box** (четверть разрешения, box-фильтрация), **4x Bilinear** (четверть разрешения, билинейная фильтрация) — снижение разрешения этой текстуры прямо экономит bandwidth на мобильном.

Практический вывод: для 2D-головоломки без Sprite Mask, без кастомных шейдеров, читающих `CameraSortingLayerTexture`, и без потребности в HDR-освещении — все три параметра (Depth/Stencil Buffer, Camera Sorting Layer Texture) стоит отключать/ставить Disabled по умолчанию, включая только при конкретной необходимости конкретного визуального эффекта.

## Sprite Atlas: настройка, сжатие текстур под iOS (ASTC), Max Size, mipmaps

### Зачем нужен Sprite Atlas

Официальная страница "Sprite atlases" (открыта через WebSearch-агрегацию содержимого docs.unity3d.com, версия 6000.4 Manual — базовое определение не версионно-специфично):
> Сообщается, что sprite atlas объединяет несколько текстур в одну, и Unity создаёт только один draw call для всех спрайтов в нём, что улучшает производительность.
[docs.unity3d.com — atlas-introduction](https://docs.unity3d.com/6000.4/Documentation/Manual/sprite/atlas/atlas-introduction.html)

Страница "Create a sprite atlas" для версии 6000.3 открыта напрямую и описывает базовый workflow создания атласа и добавления в него спрайтов, включая использование Custom Outline в Sprite Editor для более плотной упаковки. [docs.unity3d.com — create-sprite-atlas](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/create-sprite-atlas.html)

### Настройки Sprite Atlas Inspector

Страница "Sprite Atlas Inspector window reference" для 6000.3 открыта напрямую: [docs.unity3d.com — sprite-atlas-reference](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html)

**Max Texture Size:**
> "Sets the maximum dimensions of the sprite atlas in pixels."
Если суммарный размер спрайтов меньше указанного значения, Unity использует минимально необходимый размер. Отдельное предупреждение для вариантных атласов (variant sprite atlas): не использовать Max Texture Size со значением масштаба меньше 0.25, иначе "Unity compresses the sprites and textures twice" — двойное сжатие ухудшает качество.

**Compression (формат сжатия):**
На странице явно перечислены только общие уровни качества:
- "**None**: Don't compress the sprite atlas."
- "**Low Quality**: Compresses the sprite atlas using a low-quality texture format."
- "**Normal Quality**: Compresses the sprite atlas using a standard texture format."
- "**High Quality**: Compresses the sprite atlas using a high-quality texture format."
Явного упоминания ASTC именно на этой странице (общие настройки Sprite Atlas) не найдено — ASTC как конкретный формат для iOS настраивается через **platform-specific override tabs** ("These tabs let you override the settings in the **Default** tab for specific platforms", позволяя переопределить Max Texture Size, Format и compressor quality под конкретную платформу, например iOS) в сочетании с общими настройками сжатия текстур платформы (см. ниже).

**Generate Mip Maps:**
> Создаёт мип-уровни для текстуры атласа; актуальность параметра растёт при использовании anisotropic filtering вместе с определёнными filter mode.
Прямой рекомендации "включать/выключать для 2D" на странице Sprite Atlas Inspector нет. Практический вывод (не цитата, вывод агента): для 2D-игры с ортографической камерой без сильного отдаления/приближения камеры к спрайтам мипмапы почти всегда не нужны — они не улучшают качество (спрайт не масштабируется вдаль, как 3D-текстура на удалённой поверхности) и тратят дополнительную память (примерно на треть больше по независимым источникам ниже, если сравнивать с отключёнными мипмапами).

### ASTC как формат для iOS

Страница "Choose a GPU texture format by platform" открыта напрямую: [docs.unity3d.com — texture-choose-format-by-platform](http://docs.unity3d.com/6000.3/Documentation/Manual/texture-choose-format-by-platform.html)
> "For Apple devices that use the A8 chip (2014) or above, ASTC is the recommended texture format for RGB and RGBA textures."
ASTC даёт гибкий компромисс качество/размер — от 8 бит/пиксель (блоки 4x4) до 0.89 бит/пиксель (блоки 12x12). Для более старых устройств с чипом A7 ASTC аппаратно не поддерживается и будет декомпрессироваться в рантайме — в этом случае запасной вариант — ETC/ETC2, хотя они дают менее гибкий контроль качества.

### Общая рекомендация по mipmaps и памяти (независимый источник, открыт напрямую)

Практический разбор мобильной 2D-производительности (divillysausages.com) рекомендует:
> "Even Half Res on mobile is mostly unnoticable, and you'll gain about 75% memory" — про снижение разрешения текстур.
> Отключение мипмапов, если текстура не масштабируется вниз, экономит "approximately 33% memory" по оценке автора статьи.
[divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/) — это оценка отдельного разработчика, а не официальная документация Unity; конкретные проценты не подтверждены официальным источником, помечаю как мнение практика, не как факт из первоисточника Unity.

## Pixels Per Unit, Sorting Layers, Sorting Group, порядок отрисовки и батчинг

### Pixels Per Unit (PPU)

Страница "Sprite (2D and UI) texture Import Settings window reference" для 6000.3 открыта напрямую: [docs.unity3d.com — texture-type-sprite](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-type-sprite.html)
> "The number of pixels of width/height in the Sprite image that correspond to one distance unit in world space."
Официальная страница не даёт единой "правильной" рекомендации по числовому значению PPU — это зависит от арт-пайплайна проекта. Важное практическое замечание (не из официального Unity-источника, из независимого разбора): при использовании физики значение PPU напрямую влияет на точность контактов коллайдеров из-за default contact offset, поэтому для физически активных объектов часто выбирают PPU, равный исходному пиксельному размеру спрайта, а не круглое число вроде 100.

### Sorting Layers и Order in Layer

Страница "Change the sorting order of 2D GameObjects" для 6000.3 открыта напрямую: [docs.unity3d.com — 2d-renderer-sorting](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-renderer-sorting.html)
> "All 2D GameObjects are on the **Default** sorting layer" по умолчанию; дополнительные Sorting Layers создаются через Edit > Project Settings > Tags and Layers.
> "Unity renders the layers in order from top to bottom."
> "Unity renders sublayers in numerical order, so lower values render behind higher values. For example, a sprite with an **Order in Layer** value of –1 renders behind a sprite with an **Order in Layer** value of 3."

### Sorting Group

По данным WebSearch-агрегации официальной документации Unity (страницы "Sorting group reference" для разных версий, не проверено отдельным WebFetch этим агентом — помечаю как непроверено напрямую): Sorting Group — компонент, который группирует несколько Renderer'ов с общим корнем для совместной сортировки; все Renderer'ы внутри одной Sorting Group используют общий Sorting Layer, Order in Layer и Distance to Camera относительно камеры. Типичный случай применения — составной 2D-персонаж/объект из нескольких спрайтов, который должен сортироваться как единое целое относительно остальной сцены. Компонент добавляется через Component > Rendering > Sorting Group на корневой GameObject иерархии.
[docs.unity3d.com — sorting-group-reference (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/sprite/sorting-group/sorting-group-reference.html)

### Как не сломать батчинг

Официальная документация Unity (общая, не 2D-специфичная) описывает принцип: рендереры с одинаковыми настройками материала сортируются вместе для более эффективного рендеринга, включая dynamic batching — Unity пытается рендерить несколько мешей как один, трансформируя вершины на CPU и группируя похожие вершины. Для тайбрейка при одинаковом приоритете сортировки используется порядок в Render Queue; 2D Renderer'ы (Sprite Renderer, Tilemap Renderer, Sprite Shape Renderer) в основном находятся в очереди Transparent, у 2D-материалов по умолчанию render queue = 3000. GameObject'ы без Sorting Group рендерятся как единый слой и всё равно сортируются по Sorting Layer/Order in Layer. (Эти формулировки получены через WebSearch-агрегацию по нескольким версиям официального Unity Manual, включая страницы 2D Sorting разных лет — прямого WebFetch с точной цитатой по конкретно этому абзацу этот агент не выполнял; помечаю как частично проверено.)

Практические правила, чтобы не сломать батчинг спрайтов (синтез из открытых напрямую источников — Sprite Atlas и Sorting страниц, а также независимого практического разбора divillysausages.com):
- Спрайты, которые рисуются рядом друг с другом и должны батчиться в один draw call, должны использовать один и тот же материал/шейдер и происходить из одного и того же Sprite Atlas — иначе даже при соседних Order in Layer они попадут в разные батчи из-за смены текстуры/материала.
- Не стоит паковать в один Sprite Atlas вообще все спрайты игры без разбора — по практической рекомендации из независимого источника (не Unity-документация, мнение практика), эффективнее делать отдельные атласы под то, что реально используется одновременно на экране (например, отдельный атлас на сцену/уровень), а не один гигантский атлас на всю игру.
- Не стоит атласить спрайты, использующие разные render state (например, один непрозрачный, другой — с alpha clip, третий — double-sided): такие спрайты всё равно попадут в разные draw call'ы независимо от общего атласа, а выигрыша не будет — только более сложная упаковка UV. [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/)
- Ориентир по количеству draw call'ов на мобильном из практического источника (не официальная документация Unity, мнение практика): "I'd say any more than 10 is probably too many, and more than 20 will give you problems." [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/)

## 2D-освещение URP: стоит ли включать на мобильном, и его цена

Официальная документация Unity (получена через WebSearch-агрегацию по странице "Introduction to 2D lighting in URP", отдельным точечным WebFetch этим агентом не перепроверялась дословно — помечаю как частично проверено):
> 2D Lighting в URP описывается как набор "artist friendly" инструментов и runtime-компонентов для быстрого освещения 2D-сцены через Sprite Renderer и компоненты 2D Light (аналоги 3D Light-компонентов); система оптимизирована для мобильных систем и для нескольких платформ. Важно: 2D-освещение **не физически корректно (not physically-based)**, в отличие от 3D-освещения.
[docs.unity3d.com — Lights-2D-intro](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/Lights-2D-intro.html)

Технически рендеринг 2D-света и теней происходит в несколько проходов: сначала рисуются формы 2D-теней в одну или несколько shadow-текстур, затем цвет и форма каждого 2D-света рисуются в одну или несколько light-текстур; light render texture создаётся, только если хотя бы один 2D Light её использует (то есть неиспользуемые blend style не создают лишних текстур).

Типы источников света: **2D Global Light** (освещает все 2D GameObject'ы равномерно, без затухания, максимум один глобальный свет на blend style и sorting layer), **Spot Light**, **Sprite Light**, **Freeform Light** (Parametric Light устарел начиная с URP 11 в пользу Freeform).

### Реальная цена на мобильном: практические отчёты

Официальная документация Unity не приводит числовых порогов производительности для Light2D. Независимые практические источники (через WebSearch, не открыты этим агентом напрямую отдельным WebFetch — оценки конкретных разработчиков, не факт из первоисточника Unity):
- Разработчик, добавивший Light2D в мобильную 2D-игру, обнаружил отсутствие поддержки отбрасывания теней "из коробки" и реализовал самодельные тени на коллайдерах и шейдерах; результат хорошо работал на PC, но давал только **около 6 fps на телефоне**. Профилирование показало, что проблема была именно в самом Light2D, что заставило разработчика написать собственное лёгкое решение на основе SpriteRenderer. [gamba04.itch.io devlog](https://gamba04.itch.io/my-personal-devlog/devlog/145714/2d-mobile-optimized-lighting-system)
- Сторонняя GPU-based система 2D-освещения (не встроенный Light2D, а community-проект с похожими принципами) создаёт от 6 до 10 draw call'ов в зависимости от настроек и потребляет около **1-2 мс на кадр на Nexus 4** даже при отсутствии активных источников света в кадре (если система не отключена полностью). [github.com/simeonradivoev/Light2D](https://github.com/simeonradivoev/Light2D)

Практический вывод: включать URP 2D Lighting на мобильном стоит только при реальной художественной необходимости и с обязательным профилированием на целевых устройствах (в первую очередь на низком/среднем ценовом сегменте, если целевая аудитория его включает); для головоломки, где освещение не является ключевым геймплейным или художественным элементом, разумная стратегия по умолчанию — не использовать Light2D вовсе и имитировать освещение через статичные затемнения/градиенты в самих спрайтах, что не имеет рантайм-цены.

## Canvas и разрешения: раскладка под разные соотношения сторон iPhone, safe area

### Canvas Scaler

Страница "Canvas Scaler" (пакет com.unity.ugui, версия 2.6) открыта напрямую: [docs.unity3d.com — script-CanvasScaler](https://docs.unity3d.com/Packages/com.unity.ugui@2.6/manual/script-CanvasScaler.html)

Три режима **UI Scale Mode**:
- **Constant Pixel Size**: "Makes UI elements retain the same size in pixels regardless of screen size."
- **Scale With Screen Size**: "Makes UI elements bigger the bigger the screen is."
- **Constant Physical Size**: "Makes UI elements retain the same physical size regardless of screen size and resolution."

**Screen Match Mode** (для режима Scale With Screen Size), в частности "Match Width Or Height":
> "Scale the canvas area with the width as reference, the height as reference, or something in between."

Практический вывод для раскладки под разные iPhone (от iPhone SE с более "квадратным" соотношением сторон до iPhone Pro Max с вытянутым экраном): использовать **Scale With Screen Size** + **Match Width Or Height** с референсным разрешением, характерным для основной части аудитории, и значением Match, подобранным под то, что важнее сохранить неизменным — ширину игрового поля (Match = 0, по ширине) или высоту (Match = 1, по высоте); для головоломки, где важна геометрия игрового поля, чаще имеет смысл фиксировать по меньшей стороне/ширине, чтобы всё игровое поле гарантированно помещалось на экран, а UI-элементы по краям адаптировались под лишнее пространство.

### Safe Area

Страница Scripting API "Screen.safeArea" открыта напрямую: [docs.unity3d.com — Screen-safeArea](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html)
> "Returns the safe area of the screen in pixels (Read Only)."

Свойство определяет часть экрана, реально видимую пользователю, с учётом непрямоугольных дисплеев (вырезы/notch, скруглённые углы) — то есть напрямую релевантно для iPhone с notch/Dynamic Island. Технические детали:
- Максимальный размер safe area равен разрешению экрана: `Rect(0, 0, Screen.width, Screen.height)`.
- Точка отсчёта — нижний левый угол (в отличие от UI Toolkit, где отсчёт идёт от верхнего левого угла).
- Safe area указывается относительно окна Unity Player, а не физического устройства.

Пример преобразования координат для UI Toolkit из документации (инверсия оси Y из системы координат "низ-лево" в "верх-лево"):
```
var safeAreaForUIToolkit = new Rect(Screen.safeArea.x, 
    Screen.height - Screen.safeArea.y, 
    Screen.safeArea.width, 
    Screen.safeArea.height);
```

Практический вывод: критичные интерактивные и текстовые UI-элементы (кнопки, счёт, таймер) должны позиционироваться с учётом `Screen.safeArea`, а не жёстко к краям экрана — иначе на iPhone с notch/Dynamic Island или с закруглёнными углами эти элементы рискуют быть частично не видны или недоступны для тапа.

## Типовые ошибки производительности 2D на мобильном (по отчётам разработчиков)

Основной практический источник этого раздела — независимый разбор мобильной 2D-производительности, открытый напрямую: [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/). Это мнение практика, а не официальная документация Unity — числа и пороги ниже стоит воспринимать как ориентиры, а не гарантированные значения.

**Overdraw от прозрачных/полупрозрачных спрайтов.** Официальная позиция Unity (страница про оптимизацию под мобильные, версия 560 Manual, получена через WebSearch, дословно не перепроверена этим агентом отдельным WebFetch — помечаю как частично проверено) формулирует это как проблему fillrate: "на мобильных вы, по сути, ограничены fillrate (fillrate = пиксели экрана × сложность шейдера × overdraw)", и чрезмерно сложные шейдеры — самая частая причина проблем; отдельно упоминаются частицы: они должны минимизировать overdraw и использовать максимально простые шейдеры. Каждый лишний слой прозрачности поверх уже отрисованного пикселя — это лишняя запись в framebuffer, которую GPU не может пропустить (в отличие от непрозрачной геометрии с early-Z).

**Слишком много draw call'ов из-за отсутствия/неправильного использования Sprite Atlas.** Смешение неатласированных текстур или использование нескольких атласов там, где нужен один, приводит к тому, что Unity не может объединить отрисовку соседних по глубине спрайтов в один batch.

**Полный ребилд геометрии Canvas при частых изменениях UI.** Практическая рекомендация: разделять статичный и часто изменяющийся UI на разные Canvas — "a single moving element forces entire canvas geometry rebuilding" (перевод сути: движение всего одного элемента внутри Canvas вызывает пересчёт геометрии всего Canvas целиком, включая статичные элементы, если они в том же Canvas). Отсюда практика — выносить анимированные/часто обновляемые элементы (таймеры, счётчики, полоски прогресса) в отдельный Canvas от статичного фонового UI.

**`raycastTarget` включён на некликабельных UI-элементах.** Каждый включённый `raycastTarget` добавляет объект в проверку рейкастером ввода — на элементах, которые никогда не должны реагировать на тап (декоративные иконки, фон панелей), рекомендуется явно отключать `raycastTarget`, чтобы снизить накладные расходы raycaster'а.

**Неэффективная физика 2D.** Рекомендация — не двигать объекты напрямую через позицию Rigidbody2D в горячем цикле там, где это не нужно, и не использовать множество отдельных коллайдеров на тайл, а предварительно генерировать/объединять коллизионные меши (composite collider) вместо коллайдера на каждый тайл по отдельности.

**Мипмапы и избыточное разрешение текстур.** Как уже отмечено в разделе про Sprite Atlas: избыточное разрешение спрайтов и включённые без надобности мипмапы напрямую увеличивают потребление видеопамяти (по оценке источника — до 75% экономии памяти при переходе на половинное разрешение там, где потеря качества незаметна, и около 33% экономии при отключении неиспользуемых мипмапов), что на мобильных устройствах с ограниченной и разделяемой с системой видеопамятью может провоцировать выгрузку текстур и просадки.

**Дополнительные общие рекомендации из того же источника** (мнение практика, не официальная документация): явно задавать `Application.targetFrameRate` (например, в 60), включать vSync в настройках качества, использовать object pooling вместо Instantiate/Destroy в горячих путях, кэшировать часто запрашиваемые компоненты вместо повторных `GetComponent` в Update.

## Источники

- [docs.unity3d.com — universalrp-asset](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/universalrp-asset.html) — настройки URP Asset (HDR, MSAA, Opaque/Depth Texture, Render Scale, post-processing, Soft Shadows).
- [docs.unity3d.com — 2DRendererData-overview](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/2DRendererData-overview.html) — 2D Renderer Data (HDR Emulation Scale, Depth/Stencil Buffer, Camera Sorting Layer Texture).
- [docs.unity3d.com — create-sprite-atlas](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/create-sprite-atlas.html) — создание Sprite Atlas.
- [docs.unity3d.com — sprite-atlas-reference](https://docs.unity3d.com/6000.3/Documentation/Manual/sprite/atlas/sprite-atlas-reference.html) — настройки Sprite Atlas Inspector (Max Texture Size, Compression, Generate Mip Maps, platform overrides).
- [docs.unity3d.com — atlas-introduction (6000.4)](https://docs.unity3d.com/6000.4/Documentation/Manual/sprite/atlas/atlas-introduction.html) — общее назначение Sprite Atlas.
- [docs.unity3d.com — texture-choose-format-by-platform](http://docs.unity3d.com/6000.3/Documentation/Manual/texture-choose-format-by-platform.html) — рекомендация ASTC для iOS (A8+), запасной вариант ETC/ETC2.
- [docs.unity3d.com — texture-compression-formats](http://docs.unity3d.com/6000.3/Documentation/Manual/texture-compression-formats.html) — общая структура документации по форматам сжатия текстур.
- [docs.unity3d.com — texture-type-sprite](https://docs.unity3d.com/6000.3/Documentation/Manual/texture-type-sprite.html) — Pixels Per Unit, Filter Mode, Generate Mipmap для спрайтов.
- [docs.unity3d.com — 2d-renderer-sorting](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-renderer-sorting.html) — Sorting Layers, Order in Layer.
- [docs.unity3d.com — sorting-group-reference (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/sprite/sorting-group/sorting-group-reference.html) — Sorting Group (не проверено отдельным WebFetch этим агентом, только через WebSearch-агрегацию).
- [docs.unity3d.com — Lights-2D-intro (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/Lights-2D-intro.html) — введение в 2D-освещение URP (не проверено отдельным WebFetch этим агентом, только через WebSearch-агрегацию).
- [docs.unity3d.com — script-CanvasScaler (com.unity.ugui@2.6)](https://docs.unity3d.com/Packages/com.unity.ugui@2.6/manual/script-CanvasScaler.html) — Canvas Scaler, UI Scale Mode, Screen Match Mode.
- [docs.unity3d.com — Screen-safeArea](https://docs.unity3d.com/ScriptReference/Screen-safeArea.html) — Screen.safeArea API.
- [divillysausages.com — performance-tips-for-unity-2d-mobile](https://divillysausages.com/2016/01/21/performance-tips-for-unity-2d-mobile/) — практические советы по 2D-производительности на мобильном (draw calls, overdraw, Canvas, физика, mipmaps); мнение практика, не официальная документация.
- [gamba04.itch.io — 2D Mobile Optimized Lighting System devlog](https://gamba04.itch.io/my-personal-devlog/devlog/145714/2d-mobile-optimized-lighting-system) — практический отчёт о цене Light2D на мобильном (не проверено отдельным WebFetch этим агентом, только через WebSearch).
- [github.com/simeonradivoev/Light2D](https://github.com/simeonradivoev/Light2D) — сторонняя система 2D-освещения с указанными цифрами по draw calls и мс/кадр на Nexus 4 (не проверено отдельным WebFetch этим агентом, только через WebSearch).
- unity.com/blog/games/optimize-your-mobile-game-performance-expert-tips-on-graphics-and-assets — официальный блог Unity про мобильную оптимизацию; при попытке WebFetch страница отдала HTTP 403 (защита от ботов), содержимое не включено в файл напрямую из-за невозможности открыть и процитировать дословно.







