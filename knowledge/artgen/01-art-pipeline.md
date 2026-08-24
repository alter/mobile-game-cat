# Конвейер порождения 2D-рисунков предметов вне движка

Дата сбора сведений: 2026-08-24.

## Кратко

- Google Imagen 4 уже отключён: официальная документация фиксирует «This model is deprecated and will be shut down on August 17, 2026» — на дату сбора (24 августа 2026) модель, скорее всего, недоступна; заменой Google называет семейство Nano Banana через Gemini API. [1]
- Актуальные модели генерации через Gemini API на 24 августа 2026: Gemini 2.5 Flash Image (Nano Banana), Gemini 3.1 Flash Image (Nano Banana 2), Gemini 3.1 Flash Lite Image (Nano Banana 2 Lite), Gemini 3 Pro Image (Nano Banana Pro) — у всех подтверждена цена и batch-тариф. [2]
- У OpenAI действует линейка `gpt-image-2`, `gpt-image-1.5`, `gpt-image-1-mini`, `gpt-image-1` с ценой за токен (не за изображение напрямую), параметром `background: "transparent"` для прозрачного фона и без параметра seed. [3][4]
- У Black Forest Labs (FLUX) подтверждена официальная цена по каждой модели линейки FLUX.2/FLUX.1/Kontext, за мегапиксель или флэт за изображение; FLUX.2 поддерживает референсные изображения (до 8 через API), явного подтверждения прозрачного фона и seed в открытых страницах не найдено. [5][6]
- Ни для одной проверенной модели не подтверждён детерминированный `seed`-параметр как публично документированная возможность — там, где это не удалось проверить, честно указано «данных не найдено».
- Единый стиль набора держат не одной настройкой, а связкой приёмов: фиксированный шаблон наказа + референсное изображение + (по возможности) LoRA, обученная на 20–30 референсных спрайтах; чистый seed закрывает не более «~80%» проблемы, по опыту практиков. [7]
- `rembg` (версия 2.0.81 на PyPI, 24 410 звёзд на GitHub, обновлён 18 августа 2026) и `backgroundremover` (0.4.5, 8020 звёзд, обновлён 10 июля 2026) остаются рабочими средствами удаления фона на Python. [8][9]
- Pillow даёт готовый набор для автообрезки и выравнивания: `Image.getbbox(alpha_only=True)`, `Image.crop(box)`, `Image.thumbnail(size)`, `Image.paste(im, box, mask)`. [10]
- Для Unity важны: степень двойки текстуры под платформу, единый PPU, отступы 2–4 px в атласе, компрессия ASTC/ETC2 на мобильных, Sprite Atlas для сокращения числа draw call. [11][12]
- Перекраску кота из слоёв в 2D делают через маски цвета и один шейдер (Replace Color / Color Mask в Shader Graph, либо ручной HLSL с RGB-масками), а не через отдельные текстуры на каждый вариант окраса. [13][14][15]

## Модели порождения изображений через API (август 2026)

### OpenAI: линейка gpt-image

По официальному руководству (developers.openai.com, проверено 2026-08-24) актуальны модели `gpt-image-2`, `gpt-image-1.5`, `gpt-image-1`, `gpt-image-1-mini`; для Responses API вызов идёт через модель вроде `gpt-5.6`, которая обращается к инструменту генерации изображений. [3]

Поддерживаемые размеры (`size`): `1024x1024`, `1536x1024`, `1024x1536`, `2048x2048`, `2048x1152`, `2160x3840`, `3840x2160`, `auto`. Ограничения: «максимальный размер края ≤ 3840px», «оба края кратны 16px», «коэффициент длинной к короткой стороне ≤ 3:1». [3]

Прозрачный фон подтверждён официально: нужно установить `background: "transparent"`; работает с форматами `png` и `webp`, но не с `jpeg`. [3]

Batch-режима как отдельного API для изображений в руководстве не найдено; параметр `n` лишь «генерирует несколько изображений сразу в одном запросе» — это не то же самое, что настоящий batch job. Отдельный `seed`-параметр в документации не упоминается; более того, сама документация признаёт ограничение консистентности: «модель может иногда испытывать сложности поддерживать консистентность» для повторяющихся персонажей. [3]

Прочие параметры: `quality` — `"low"`, `"medium"`, `"high"`, `"auto"`; `format` — `"png"` (по умолчанию), `"jpeg"`, `"webp"`; `output_compression` — 0–100% для JPEG/WebP; `moderation` — `"auto"` или `"low"`. [3]

Официальная цена (developers.openai.com/api/docs/pricing, проверено 2026-08-24) указана за 1 млн токенов, а не за изображение напрямую:

```
gpt-image-2:      input $8.00 / output $30.00 за 1M токенов
gpt-image-1.5:    input $8.00 / output $32.00 за 1M токенов
gpt-image-1-mini: input $2.50 / output $8.00  за 1M токенов
gpt-image-1:      input $10.00 / output $40.00 за 1M токенов
```
Batch-режим (для текстовых/токенных вызовов, не путать с «batch генерации изображений» выше) — цены в два раза ниже. [4]

Официальная таблица расхода токенов на одно изображение по `quality`/`size` (для моделей линейки до `gpt-image-2`):

| Quality | Square (1024×1024) | Portrait (1024×1536) | Landscape (1536×1024) |
|---|---|---|---|
| Low | 272 tokens | 408 tokens | 400 tokens |
| Medium | 1056 tokens | 1584 tokens | 1568 tokens |
| High | 4160 tokens | 6240 tokens | 6208 tokens |

Для `gpt-image-2` отдельной табличной раскладки токенов в открытой документации не найдено — расчёт предлагается через калькулятор внутри руководства. [3]

### Google: Imagen (отключается) и семейство Nano Banana

Официальная документация Gemini API прямо пишет про Imagen: «This model is deprecated and will be shut down on August 17, 2026; migrate to Nano Banana for image generation». Дата сбора сведений — 24 августа 2026, то есть срок уже прошёл: полагаться на Imagen 4 в новом проекте нельзя. [1]

Пока модель была активна, официальная цена по тарифам была: Fast — $0.02, Standard — $0.04, Ultra — $0.06 за изображение; поддерживались размеры 1K и 2K (2K — только Standard/Ultra), соотношения сторон 1:1, 3:4, 4:3, 9:16, 16:9, конфигурация через `numberOfImages` (1–4), `imageSize`, `aspectRatio`, `personGeneration`. Ни seed, ни референсных изображений, ни прозрачного фона, ни batch-режима для Imagen в документации не описано. [1]

Официальная замена — линейка Nano Banana через Gemini API. Официальная цена (ai.google.dev/gemini-api/docs/pricing, проверено 2026-08-24):

```
Gemini 2.5 Flash Image (Nano Banana):
  input $0.30 / 1M tokens
  output "$0.039 per image"
  batch: $0.15 / 1M input, $0.0195 per image output

Gemini 3.1 Flash Image (Nano Banana 2):
  input $0.50 / 1M tokens
  output $60 / 1M tokens ($0.045–0.151 per image, зависит от разрешения)
  batch: $0.25 / 1M input, $30 / 1M output

Gemini 3.1 Flash Lite Image (Nano Banana 2 Lite):
  input $0.25 / 1M tokens
  output $1.50 / 1M tokens ($0.0336 per image)
  batch: $0.125 / 1M input, $0.75 / 1M output

Gemini 3 Pro Image (Nano Banana Pro):
  input $2.00 / 1M tokens
  output $120 / 1M tokens ($0.134–0.24 per image)
  batch: $1.00 / 1M input, $6.00 / 1M output
```
[2]

Размеры: Nano Banana 2 Lite — 0.5K (512px) и 1K; Nano Banana 2 и Nano Banana Pro — 1K, 2K и 4K. [2]

Референсные изображения официально подтверждены: модели Nano Banana поддерживают «up to 14 reference images» для сохранения консистентности персонажей и предметов — лимит варьируется по уровню модели. [2]

Batch API подтверждён отдельно: «All of the image generation capabilities described on this page can also be run as batch jobs using the Batch API», с оговоркой «higher rate limits in exchange for a turnaround of up to 24 hours». [2]

Прозрачный фон и `seed`-параметр в проверенной документации не упомянуты — данных не найдено.

### Black Forest Labs: линейка FLUX

Официальная страница цен bfl.ai/pricing (проверено 2026-08-24, данные извлечены из встроенной в страницу JSON-разметки) даёт по каждой модели точную цену. Часть моделей тарифицируется за мегапиксель (первый мегапиксель дороже последующих), часть — плоской ценой за изображение:

```
FLUX.2 [max]:          $0.07 за первый Мп, $0.03 за каждый следующий Мп
                       референс-изображения: $0.03 / Мп
FLUX.2 [pro]:          $0.03 за первый Мп, $0.015 за каждый следующий Мп
                       референс-изображения: $0.015 / Мп
FLUX.2 [klein] 9B:     $0.015 за первый Мп, $0.002 за каждый следующий Мп
                       референс-изображения: $0.002 / Мп
FLUX.2 [klein] 4B:     $0.014 за первый Мп, $0.001 за каждый следующий Мп
                       референс-изображения: $0.001 / Мп
FLUX.2 [flex]:         плоская цена $0.05 / Мп

FLUX.1 Kontext [max]:  $0.08 за изображение
FLUX.1 Kontext [pro]:  $0.04 за изображение
FLUX 1.1 [pro] Ultra:  $0.06 за изображение
FLUX 1.1 [pro]:        $0.04 за изображение
FLUX.1 [pro]:          $0.05 за изображение
FLUX.1 [dev]:          $0.025 за изображение
FLUX.1 Fill [pro]:     $0.05 за изображение
```
Правило подсчёта мегапикселей указано текстом на самой странице: «for pricing, resolution is always rounded up to the next megapixel, separately for each reference image and for the generated image» и «1 megapixel is counted as 1024x1024 pixels». [5]

Референсные изображения (для консистентности стиля/персонажа) официально подтверждены документацией FLUX.2 (docs.bfl.ai/flux_2, проверено 2026-08-24): лимит зависит от модели — «[klein]: Recommended max 6», «[max] / [pro] / [flex]: Up to 8 (API), 10 (playground)». [6]

Прозрачный фон на выходе, `seed`-параметр и отдельный batch-эндпоинт со скидкой в проверенных официальных источниках не описаны — данных не найдено. На странице цен упомянуты только общие «volume discounts... for high-throughput workloads» по индивидуальным условиям, это не то же самое, что документированный batch API. [5]

## Приёмы удержания единого стиля в наборе

Практики сходятся в том, что единый стиль — это не одна настройка, а связка приёмов, применённая одновременно, а не по отдельности.

**Фиксированный шаблон наказа + seed закрывают не всё.** Один из практиков формулирует так: «Seed control and prompt templates only get you 80% of the way. Here's what closes the gap: Use a LoRA fine-tuned on your target art style» — и уточняет, что «Even a small LoRA (4-8 rank) trained on 20-30 reference sprites dramatically improves consistency». [7]

**Причина рассинхронизации стиля называется «style drift»**: «generating the same character twice can yield two completely different art styles», лечится «a combination of fixed seeds, detailed style prompts, and a reusable prompt template». [7]

**Схема через референсное изображение**: сначала генерируется одно эталонное изображение персонажа/стиля, затем оно передаётся как референс во все последующие вызовы вместе с тем же seed и тем же шаблоном наказа — «using the same seed value, same style settings, and same prompt structure across all generations for maximum consistency». [7]

**«Art bible» до первого вызова API**: один из источников советует до генерации набора явно зафиксировать текстом камеру, палитру, направление света, масштаб, размер тайла и материальные правила — «lock the camera, palette, light direction, scale, tile size, and material rules before generating batches» — потому что «if AI changes the camera by 5-10 degrees between generations, the set feels broken». [7]

**Порождение листом (sprite sheet) вместо поштучного порождения**: современные мультимодальные модели понимают явные указания на сетку кадров в одном наказе, например «Create a sprite sheet of the character running, 8 frames in 2 rows on grey background, side view, consistent proportions» — так получают один широкий лист, который дальше нарезается в движке, вместо N независимых вызовов с риском разъезжающегося стиля между кадрами. [7]

**Батч и отбор лучшего**: «generate 4 images per pose with consecutive seeds and pick the best» — быстрее, чем перегенерировать по одному изображению за раз. [7]

**ControlNet** (для моделей на базе Stable Diffusion) называется «the king of consistency and control» — позволяет держать позу/структуру по референсному изображению, глубине или карте позы при генерации листов и поворотов. [7]

Практическое напоминание из того же источника: сгенерированный PNG — это ещё не игровой актив, «a generated PNG is not a game asset, just a picture of one»; нарезка листа на кадры, точка опоры, коллизия, аниматор — отдельный обязательный этап после генерации. [7]

## Удаление фона и подготовка прозрачности на Python

### rembg

Текущая версия на PyPI — 2.0.81 (проверено 2026-08-24, `pip index versions rembg`); на GitHub — 24 410 звёзд, последний push 2026-08-18 (данные из GitHub API). Лицензия — MIT. [8]

Поддерживаемые модели включают `u2net`, `u2netp`, `isnet-general-use`, `isnet-anime`, семейство `birefnet-general` и его модификации, а также облачную модель по умолчанию `bria-rmbg` (~1.02 GB, для высокого качества). Для волос на портретах в документации отдельно рекомендуется `birefnet-portrait` с флагом деконтаминации цвета или альфа-матированием. Требование по версии Python: `>=3.11, <3.14`. Облачный вариант API имеет ограничение загрузки 20 MB. [8]

Пример использования из README:
```python
from rembg import remove
from PIL import Image

input_img = Image.open('input.png')
output_img = remove(input_img)
output_img.save('output.png')
```
[8]

### backgroundremover

Версия на PyPI — 0.4.5 (проверено 2026-08-24); на GitHub — 8020 звёзд, последний push 2026-07-10, репозиторий не заархивирован. [9]

### Pillow: обрезка по содержимому, приведение к единому размеру, выравнивание

Официальная документация Pillow (проверено 2026-08-24) даёт точные сигнатуры:

```python
Image.getbbox(*, alpha_only: bool = True) -> tuple[int, int, int, int] | None
```
Вычисляет ограничивающий прямоугольник ненулевых (непрозрачных, если `alpha_only=True`) областей — то есть основной инструмент автообрезки спрайта по содержимому. Возвращает `None`, если изображение пусто. [10]

```python
Image.crop(box: tuple[float, float, float, float] | None = None) -> Image
```
Обрезает по прямоугольнику `(left, upper, right, lower)` в пикселях. С версии Pillow 3.4.0 операция больше не ленивая. [10]

```python
im.thumbnail(size)  # например, size = (128, 128)
im.save(file + ".thumbnail", "JPEG")
```
Уменьшает изображение до `size`, сохраняя пропорции — подходит для приведения набора к единому «превью»-размеру перед атласированием. [10]

```python
Image.paste(
    im: Image | str | float | tuple,
    box: Image | tuple[int, int, int, int] | tuple[int, int] | None = None,
    mask: Image | None = None
) -> None
```
Вставка изображения или заливки цветом на холст нужного итогового размера; `mask` управляет прозрачностью вставляемой области — используется, чтобы выравнивать обрезанные спрайты по центру канвы фиксированного размера. [10]

Типичная цепочка для одного предмета: `getbbox()` → `crop(bbox)` → создать пустой холст фиксированного размера (`Image.new("RGBA", size, (0,0,0,0))`) → `paste(cropped, offset, cropped)`, где `offset` вычислен так, чтобы центрировать обрезанное содержимое.

## Подготовка спрайтов для Unity

Официальный блог и документация Unity сходятся на нескольких правилах. Текстуры желательно приводить к степени двойки на сторону: «ideally be powers of two on each side, as this ensures hardware can efficiently compress images» — при этом ширина и высота не обязаны совпадать. Настройку `Max Size` можно и нужно задавать отдельно на платформу («Import Settings allow you to define a Max Size and other compression settings per platform»), с компрессией ASTC (лучший баланс качество/размер на современных GPU) или ETC2 (более широкая совместимость) на мобильных. [11]

Для листов, нарезаемых автоматически, между спрайтами нужен отступ: «Unity's texture sampling does need... proper padding is needed specifically to avoid visual glitches», в отличие от некоторых других движков. [11]

Pixels Per Unit (PPU) должен быть согласован по всему проекту: «Consistent Pixels Per Unit (PPU) across all related assets is paramount for ensuring uniform scaling and avoiding visual inconsistencies», типичные значения — 16/32/64 в зависимости от масштаба арта; значение по умолчанию — 100. [11]

Sprite Atlas — официальный механизм упаковки нескольких спрайтов в общую текстуру ради сокращения числа draw call на мобильных устройствах. Официальная рекомендация Unity: «ideally all or most sprites that are active in the Scene should belong to the same Atlas», а также стоит «split Sprite Textures into multiple smaller Atlases according to their common usage». Пустое пространство между упакованными текстурами уменьшает итоговый размер атласа и проверяется через панель Pack Preview в инспекторе. Если `Max Texture Size` в platform-specific overrides меньше текущих размеров атласа, Unity уменьшает упакованную текстуру автоматически. [12]

Одно независимое (не официальное Unity) измерение производительности на мобильном устройстве: сцена с ~120 уникальными текстурами спрайтов на Android среднего уровня держала 38 fps при 6,2 мс CPU-времени на отрисовку; после упаковки в один атлас 2048×2048 та же сцена держала стабильные 60 fps при 1,4 мс CPU-времени, теми же артом, шейдерами и графом сцены. Это цифры стороннего источника, не официальной документации Unity — приводятся с пометкой источника. [16]

Итоговый чек-лист для 2D-мобильного пайплайна: тип текстуры — `Sprite (2D and UI)`; для пиксель-арта — фильтр `Point (No Filter)`; единый PPU по проекту; отступы 2–4 px между спрайтами в атласах; `Max Size` — степень двойки под целевую платформу; компрессия ASTC/ETC2 на мобильных; группировка активных на сцене спрайтов в общие Sprite Atlas; per-platform overrides для снижения размера текстур на мобильных устройствах. [11][12]

## Сборка кота из слоёв: перекраска через маски и шейдер

Принцип, который разбирают все найденные практические источники: вместо отдельной текстуры на каждый окрас кота держат одну обесцвеченную (или полутоновую) базовую текстуру и одну или несколько чёрно-белых масок, а конечный цвет собирает шейдер во время отрисовки.

### Через ноды Shader Graph (Cyanilux)

Самый простой вариант — тонирование через `Multiply`: полутоновая текстура умножается на цветовое свойство материала и идёт в Base Color/Albedo — «one of the simplest forms of adjusting colour is a tint using a Multiply node between a greyscale input texture and a given colour». [13]

Более гибкие узлы — `Replace Color` и `Color Mask`. Соответствующие им HLSL-функции (дословно из разбора):
```hlsl
float3 ReplaceColor(float3 In, float3 From, float3 To,
                    float Range, float Fuzziness){
    float Distance = distance(From, In);
    return lerp(To, In, saturate((Distance - Range) /
           max(Fuzziness, 1e-5)));
}

float3 ColorMask(float3 In, float3 MaskColor,
                 float Range, float Fuzziness){
    float Distance = distance(MaskColor, In);
    return saturate((Distance - Range) / max(Fuzziness, 1e-5));
}
```
Важная оговорка о мобильной производительности: методы, которые меняют UV-координаты на этапе фрагментного шейдера, создают «dependent texture read», которая «can prevent GPU texture pre-fetches and increase latency» — то есть дороже именно на мобильных GPU, и это стоит профилировать на целевом устройстве, а не считать теоретически. [13]

### Через маски цвета поверх обесцвеченной текстуры (4experience.co)

Пошагово: (1) убрать цвет из альбедо — либо нодой saturation, либо (предпочтительнее по производительности) заранее подготовленной чёрно-белой текстурой без цветовой информации; (2) заранее подготовить маски под каждую перекрашиваемую область (например, в Blender или любом графическом редакторе); (3) для каждой маски применить узел `Lerp`, где сама маска выступает альфа-значением, определяющим, где ложится новый цвет; (4) повторить для всех дополнительных масок и объединить результаты; (5) добавить настраиваемые параметры для опциональных деталей и общих масок (логотипов/узоров), требующих отдельного UV-маппинга. [14]

### Через явный RGB-канал маски в собственном шейдере (staraban.com)

Дословный пример ShaderLab-шейдера с тремя независимыми цветами, каждый привязан к своему каналу маски-текстуры (R/G/B):
```glsl
Shader "Particles/ColorTint" {
Properties {
_MainTex ("Particle Texture", 2D) = "white" {}
_TintColorRed ("Tint Color Red", Color) = (0.5,0.5,0.5,0.5)
_TintColorGreen ("Tint Color Green", Color) = (0.5,0.5,0.5,0.5)
_TintColorBlue ("Tint Color Blue", Color) = (0.5,0.5,0.5,0.5)
}
```
Фрагментный шейдер:
```glsl
fixed4 frag (v2f i) : COLOR
{
    float4 baseColor = tex2D(_MainTex, i.texcoord);
    float alpha = baseColor.a;
    baseColor = alpha * (baseColor.r * _TintColorRed +
                        baseColor.g * _TintColorGreen +
                        baseColor.b * _TintColorBlue);
    baseColor.a = 1.0f - step(alpha, 0.1);
    return baseColor;
}
```
И управляющий C#-скрипт, назначающий цвета материалу:
```csharp
public class Player : MonoBehaviour {
    public Transform head;
    public Transform body;
    public Color HairColor, EyeColor, SkinColor, BodyColor;

    void ColorTint() {
        if(head != null) {
            Material tempMaterial = new Material(
                head.GetComponent<Renderer>().sharedMaterial);
            tempMaterial.SetColor("_TintColorRed", SkinColor);
            tempMaterial.SetColor("_TintColorBlue", HairColor);
            tempMaterial.SetColor("_TintColorGreen", EyeColor);
            head.GetComponent<Renderer>().material = tempMaterial;
        }
    }
}
```
Применительно к коту: канал R маски — цвет базового меха, G — цвет пятен/полос узора, B — например, цвет ушей/лап; так один спрайт кота и одна маска дают произвольное число окрасов без роста числа текстур. [15]

Готовое решение уровня ассета для пиксель-арта — Mana Seed Shaders: перекраска происходит простым назначением материала на спрайт, «eliminating the need for extra palette swapped sheets», с предзагруженными палитрами. [13]

## Пакетный прогон на Python: организация

Официальный пример от OpenAI (`openai-cookbook/examples/api_request_parallel_processor.py`) описан как решение именно для параллелизации запросов к API с ограничением по лимитам: «parallelizes requests to the OpenAI API while throttling to stay under rate limits, streaming requests from a file to avoid running out of memory for giant jobs, making requests concurrently to maximize throughput, throttling request and token usage, retrying failed requests up to a configurable number of times, and logging errors». Пример запуска (для другого эндпоинта, но структура переносится на генерацию изображений заменой `request_url` и тела запроса):
```
python examples/api_request_parallel_processor.py \
  --requests_filepath ... \
  --request_url https://api.openai.com/v1/embeddings \
  --max_requests_per_minute 1500 \
  --max_tokens_per_minute 6250000 \
  --max_attempts 5
```
[17]

Официально рекомендованный OpenAI паттерн повторных попыток — декоратор `tenacity.retry` с `wait_random_exponential` (случайная экспоненциальная задержка, чтобы повторные запросы разных задач не били по API одновременно). Дословный пример из документации:
```python
from openai import OpenAI
from tenacity import (
    retry,
    stop_after_attempt,
    wait_random_exponential,
)  # for exponential backoff

client = OpenAI()


@retry(wait=wait_random_exponential(min=1, max=60), stop=stop_after_attempt(6))
def completion_with_backoff(**kwargs):
    return client.completions.create(**kwargs)


completion_with_backoff(
    model="gpt-3.5-turbo-instruct",
    prompt="Once upon a time,",
)
```
Документация отдельно оговаривает: «Tenacity is a third-party tool» — OpenAI не даёт гарантий по его надёжности, это готовый, но сторонний компонент. [18]

Практическая организация пакетного прогона набора предметов для игры, собранная из перечисленных выше официальных примеров и общих рекомендаций по конкурентности в Python:
1. Список наказов формируется заранее как структура данных (например, список словарей: `{"id": "item_042_sword", "prompt": "...", "size": "1024x1024"}`), а не строится на лету — это то же требование, что и «streaming requests from a file», только в обратную сторону (можно писать результаты сразу по мере готовности).
2. Одновременные обращения ограничиваются семафором или пулом (`asyncio.Semaphore`, либо счётчик запросов в минуту) — так же, как в примере от OpenAI, где троттлинг идёт по `max_requests_per_minute`/`max_tokens_per_minute`.
3. Повторные попытки — через `tenacity` с экспоненциальной задержкой и джиттером, как в официальном примере выше; неуспешные запросы логируются отдельно с `id` задачи, чтобы их можно было перезапустить точечно.
4. Сохранение — по осмысленному имени, включающему идентификатор предмета, а не порядковый номер вызова API (`item_042_sword_v1.png`), чтобы результат было легко сопоставить с исходным наказом при повторном запуске только неудачных позиций.
5. Для сценария «нужно 500+ изображений и не горит по времени» вместо синхронного пула запросов стоит смотреть в сторону официального Batch API (см. разделы про OpenAI и Google выше) — он даёт по данным официальных страниц пониженную цену за счёт отложенного (до 24 часов у Google) исполнения, а не за счёт параллелизма на стороне клиента.

## Источники

1. [Imagen — ai.google.dev/gemini-api/docs/imagen](https://ai.google.dev/gemini-api/docs/imagen)
2. [Gemini API pricing — ai.google.dev/gemini-api/docs/pricing](https://ai.google.dev/gemini-api/docs/pricing)
3. [Image generation guide — developers.openai.com/api/docs/guides/image-generation](https://developers.openai.com/api/docs/guides/image-generation)
4. [API pricing — developers.openai.com/api/docs/pricing](https://developers.openai.com/api/docs/pricing)
5. [FLUX API Pricing — bfl.ai/pricing](https://bfl.ai/pricing)
6. [FLUX.2 documentation — docs.bfl.ai/flux_2](https://docs.bfl.ai/flux_2)
7. [AI Game Asset Generation: How to Use AI to Build 2D Game Art Faster — Spritesheets.ai](https://www.spritesheets.ai/blog/ai-game-asset-generation-guide)
8. [danielgatis/rembg — GitHub](https://github.com/danielgatis/rembg)
9. [nadermx/backgroundremover — GitHub](https://github.com/nadermx/backgroundremover)
10. [Image module — Pillow documentation](https://pillow.readthedocs.io/en/stable/reference/Image.html)
11. [A Mobile Artist's Guide to Unity: Import Settings & Best Practices — Medium](https://medium.com/@chetan.balhara/a-mobile-artists-guide-to-unity-import-settings-best-practices-dcfdfa6c81a7)
12. [Optimize Sprite Atlas usage and size for improved performance — Unity Manual](https://docs.unity3d.com/6000.1/Documentation/Manual/sprite/atlas/workflow/optimize-sprite-atlas-usage-size-improved-performance.html)
13. [Swapping Colours — Cyanilux Shader Tutorials](https://www.cyanilux.com/tutorials/color-swap/)
14. [Tutorial: Asset color customization with shader graph and color masks — 4experience.co](https://4experience.co/asset-color-customization-with-shader-graph-and-color-masks/)
15. [2D Game Tutorial. Part 1. Character creating and tinting — staraban.com](https://staraban.com/en/2d-game-tutorial-part-1-simple-characters-with-cusomization-using-tint-shader/)
16. [Why Sprite Atlases Matter for Unity Mobile Games — I Love Sprites Blog](https://ilovesprites.com/blog/unity-sprite-atlas-mobile-games)
17. [api_request_parallel_processor.py — openai/openai-cookbook](https://github.com/openai/openai-cookbook/blob/main/examples/api_request_parallel_processor.py)
18. [Rate limits guide (retry with tenacity) — developers.openai.com/api/docs/guides/rate-limits](https://developers.openai.com/api/docs/guides/rate-limits)

