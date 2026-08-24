# Unity 6.3 LTS — политика релизов, версии, новое в 6.3, Box2D v3, требования к iOS-сборке

Дата сбора: 2026-08-24
Версия стека проекта: Unity 6.3 LTS (линейка 6000.3.x), сборка под iOS, C#, .NET Standard 2.1, URP 2D Renderer.

## Кратко

- Начиная с Unity 6 существует два типа релизов: **LTS (Long Term Support)** и **Update release** (пришёл на смену старой концепции Tech Stream). Оба проходят одинаковый по строгости цикл QA. [unity.com/releases/unity-6/support](https://unity.com/releases/unity-6/support) (открыть напрямую не удалось, 403; факт подтверждён через [endoflife.date/unity](https://endoflife.date/unity) и связанные источники ниже).
- **6000.3 (Unity 6.3) — это LTS-релиз**, вышел 2025-12-04. Обычная поддержка — до 2027-12-04, расширенная (Unity Enterprise/Industry, +1 год) — до 2028-12-04. [endoflife.date/unity](https://endoflife.date/unity)
- **6000.0 (Unity 6.0) — тоже LTS**, вышел 2024-04-29, обычная поддержка до 2026-10-16, расширенная — до 2027-10-16. [endoflife.date/unity](https://endoflife.date/unity)
- Между LTS-релизами вышли Update-релизы **6000.1, 6000.2, 6000.4, 6000.5** — они не LTS и поддерживаются только до выхода следующего релиза (обычной или LTS ветки). [endoflife.date/unity](https://endoflife.date/unity)
- На дату сбора (2026-08-24) последний патч-релиз линейки 6000.3.x — **6000.3.22f1** (вышел 2026-08-13 по данным endoflife.date; отдельно подтверждено содержимое релиза 6000.3.22f1 через unityreleases.com). [endoflife.date/unity](https://endoflife.date/unity), [unityreleases.com/releases/6000.3.22f1](https://unityreleases.com/releases/6000.3.22f1)
- Главное новое в 6.3 для 2D — низкоуровневый **LowLevelPhysics2D API на Box2D v3**, который работает параллельно со старым Rigidbody2D/Collider2D, не заменяя его. [docs.unity3d.com — 2d-physics-api-introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-introduction.html)
- Для iOS: Unity 6.3 рекомендует **Xcode 16 или новее** для разработки; поддерживаются устройства с **A8 SoC и iOS 15+**; однако для публикации в App Store Apple отдельно требует более новый Xcode (см. раздел про требования). [docs.unity3d.com — ios-requirements-and-compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-requirements-and-compatibility.html), [docs.unity3d.com — system-requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)
- После апгрейда на 6.3 в Unity Discussions зафиксированы жалобы на **регрессии производительности** (BiRP, HDRP, XR/URP post-processing) — не 2D-специфичные, но релевантные при выборе версии для нового проекта. [discussions.unity.com — performance-regression-in-unity-6-3-birp](https://discussions.unity.com/t/performance-regression-in-unity-6-3-birp/1700256)
- Официальный список «Planned breaking changes in Unity 6.3» содержит в том числе удаление Legacy ETC compressor и изменения в URP Compatibility Mode — не связаны напрямую с 2D-геймплеем, но важны при апгрейде существующих ассетов. [discussions.unity.com — planned-breaking-changes-in-unity-6-3](https://discussions.unity.com/t/planned-breaking-changes-in-unity-6-3/1646418)

## Политика релизов Unity 6: LTS vs Update release

Начиная с Unity 6 Technologies отказалась от старой модели «Tech Stream / LTS» в пользу двух типов релизов:

- **LTS (Long Term Support)** — выходит раз в год, поддерживается два года исправлениями ошибок и критическими обновлениями платформ; пользователи Unity Enterprise и Unity Industry получают дополнительный третий год поддержки.
- **Update release** — несколько раз в год; в отличие от старых Tech Stream-релизов (которые были, по сути, «ранним тестированием» новых функций), Update release проходит тот же цикл QA, что и LTS, и считается production-ready. Поддерживается исправлениями ошибок и критическими обновлениями платформ **только до выхода следующего релиза** (Update или LTS).

Эти формулировки подтверждаются агрегированными данными поиска по официальной странице unity.com/releases/unity-6/support (страница отдаёт HTTP 403 при прямом открытии — блокировка бот-трафика; содержание перепроверено по независимому источнику endoflife.date, который явно транслирует ту же политику):

> "Starting with Unity 6, there are two kinds of releases: update releases and long-term support (LTS) releases. Both kinds of releases undergo the same rigorous quality assurance and stability testing. LTS releases are published once a year, supported for two years with bug fixes and critical platform updates... Unity Enterprise and Unity Industry users benefit from an additional year of support."

[endoflife.date/unity](https://endoflife.date/unity) — открыта напрямую, страница явно подтверждает: "There are multiple update releases per year. They are supported with bug fixes and critical platform updates until the next release (update or LTS) is published. LTS releases are published once a year. They are supported for two years with bug fixes and critical platform updates."

Практический вывод для проекта: поскольку 6.3 — LTS с поддержкой до конца 2027 года (расширенно — до конца 2028), это разумная база для продакшн-разработки мобильной игры на несколько лет вперёд. Использовать более новые Update-релизы (6000.4, 6000.5) в новом долгоживущем проекте не рекомендуется именно из-за короткого окна поддержки (Update release поддерживается только до следующего релиза — в случае 6000.4 поддержка уже закончилась 2026-06-17 по данным endoflife.date).

## Текущие версии линейки Unity 6 (по состоянию на 2026-08-24)

Данные ниже получены прямым открытием [endoflife.date/unity](https://endoflife.date/unity) (страница явно помечена как обновлённая 20 августа 2026 года):

| Ветка | LTS? | Дата релиза ветки | Последняя известная версия | Поддержка |
|---|---|---|---|---|
| 6000.0 (6.0) | Да | 2024-04-29 | 6000.0.82f1 (2026-08-19) | обычная до 2026-10-16, расширенная до 2027-10-16 |
| 6000.1 (6.1) | Нет | 2025-04-23 | 6000.1.17f1 | закончилась 2025-08-12 |
| 6000.2 (6.2) | Нет | 2025-08-12 | 6000.2.15f1 | закончилась 2025-12-04 |
| 6000.3 (6.3) | Да | 2025-12-04 | 6000.3.22f1 (2026-08-13) | обычная до 2027-12-04, расширенная до 2028-12-04 |
| 6000.4 (6.4) | Нет | 2026-03-18 | 6000.4.12f1 | закончилась 2026-06-17 |
| 6000.5 (6.5) | Нет | 2026-06-15 | 6000.5.9f1 (2026-08-19) | активна на момент сбора данных |

Источник таблицы: [endoflife.date/unity](https://endoflife.date/unity).

Дополнительно последний f-релиз 6000.3.x проверен по независимому агрегатору релизов: страница [unityreleases.com/releases/6000.3.22f1](https://unityreleases.com/releases/6000.3.22f1) подтверждает версию **6000.3.22f1**, дата релиза **13 августа 2026**, changeset `1c726e1fb402`, "59 total notes with 36 fixes and 13 package updates". На странице упомянуты, в частности, исправления для мобильных/iOS: устранена проблема с обрывом звука при обращении к iOS Control Center, исправлен сбой merging рендер-проходов для MSAA depth на Apple GPU families 1-3, скорректирован shader warmup для старых Apple-устройств.

Важно: по состоянию на дату сбора выпуски 6000.3.x продолжают выходить регулярно (патч-релизы f-версий), поэтому перед началом или возобновлением работы над проектом стоит перепроверить актуальную f-версию через `unity.com/releases/editor/archive` (страница недоступна для автоматического открытия из-за защиты от ботов — открывать вручную через браузер или Unity Hub).

## Что нового в 6.3: акцент на 2D и мобильные платформы

Официальная страница Unity Manual "New in Unity 6.3" (`WhatsNewUnity63.html`) открыта напрямую:

**2D:**
- "Added low-level 2D physics APIs that are an integration of Box2D v3, which is the latest actively developed version of Box2D." — подробности в отдельном разделе ниже. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "The 2D Renderer now supports rendering the Mesh Renderer and Skinned Mesh Renderer together with 2D sprites in the same scene." — то есть 2D URP Renderer научился рендерить обычные 3D Mesh Renderer/Skinned Mesh Renderer вместе со спрайтами в одной сцене. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)

**Мобильные платформы (Android/iOS):**
- "Added scrolling support for TalkBack (Android), VoiceOver (iOS), and Narrator (Windows)" — улучшение поддержки экранных читалок/accessibility на iOS/Android. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "Updated the minimum supported Android version to 7.1 (API level 25)". [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "UnityWebRequest now uses HTTP/2 protocol by default, providing improved loading times and faster networking capabilities" на Android и других платформах. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "Unity now uses Gradle version 9.1.0 and Android Gradle Plugin (AGP) version 9.0.0" — важно при обновлении CI/сборочных пайплайнов под Android. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)
- "You can now use the new Kawase and Dual filtering options for Bloom post-processing to improve performance, especially on low-end hardware and platforms" — прямо касается мобильной 2D-графики, если в проекте используется Bloom. [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)

Дополнительные детали (не с официальной страницы Manual, а из независимого разбора релиза 6000.3.0f1, статья открыта напрямую) — по 2D/производительности/мобильным темам:
- "Improved instantiation performance of GameObjects from Tiles; SpriteAtlas previews now packed asynchronously."
- "LowLevelPhysics2D renderer now performs orthographic render culling, improving debug rendering performance."
- Известные проблемы на момент 6000.3.0f1: "Metal: [iOS] Screen flashing after the iOS splash screen (UUM-121453)" и фикс SSAO precision issues на мобильных, а также "fixed Spotlights with small angles not rendering on mobile."
- Единственная упомянутая на этой странице устаревшая (deprecated) вещь, косвенно касающаяся физики: "`Physics.autoSyncTransforms` is deprecated. Use `Physics.SyncTransforms` instead" (это 3D Physics, не Physics2D, но упомянуто в том же контексте релиза).

[omitram.com — Unity 6.3 LTS (6000.3.0f1) Full Release Notes & Breakdown](https://omitram.com/unity-6-3-lts-6000-3-0f1-full-release-notes-breakdown/)

## Новое низкоуровневое 2D-физическое API на Box2D v3

Страница Unity Manual "Introduction to the LowLevelPhysics2D API" открыта напрямую: [docs.unity3d.com — 2d-physics-api-introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-introduction.html)

Что это:
> "The `LowLevelPhysics2D` API lets you create and control 2D physics objects in C# scripts."
> "The API is based on version 3 of the Box2D physics system."

Сосуществование со старым API — **это два полностью независимых, не взаимодействующих друг с другом слоя**:
> "The API doesn't interact with or affect the built-in Unity 2D physics components such as Rigidbody 2D and Collider 2D. The two systems are separate."

Совместимость с render pipeline и платформами:
> "The API is compatible with the Universal Render Pipeline (URP), the High Definition Render Pipeline (HDRP), and the Built-In Render Pipeline."
> "[The API] works on platforms that support compute shaders."

Дополнительные технические свойства (подтверждены через WebSearch по официальной документации, страница ссылается на них как на преимущества API):
- поддержка 64 слоёв коллизий вместо стандартных 32;
- бо́льшая часть API — потокобезопасна, что позволяет запускать физику в Job System на нескольких потоках;
- объекты возвращаются как структуры (struct), что упрощает использование с DOTS.

Официальная страница "New in Unity 6.3" описывает мотивацию появления API так:
> "Added low-level 2D physics APIs that are an integration of Box2D v3, which is the latest actively developed version of Box2D, including multi-threaded performance improvements, enhanced determinism, visual debugging support for both Editor and Runtime, improved gizmos, and more."
[docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html)

Устройство API (workflow, страница открыта напрямую): чтобы добавить 2D-физические объекты, сначала нужно создать `PhysicsWorld`, затем `PhysicsBody` (задаёт позицию/поворот/скорость, но не форму), и прикрепить к нему один или несколько `PhysicsShape` (задают форму, которая взаимодействует с другими формами). [docs.unity3d.com — 2d-physics-api-workflow](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-workflow.html) (страница открыта через WebSearch-агрегацию содержимого; полный текст не перепроверен отдельным WebFetch — помечаю как частично проверено).

**Стоит ли брать в новом проекте:** сама документация Unity **не даёт прямой рекомендации** "используйте/не используйте в новых проектах" — прямая цитата с такой рекомендацией отсутствует. Фактически API на 6.3 существует как параллельная, более низкоуровневая система (ручное управление PhysicsWorld/PhysicsBody/PhysicsShape в коде, а не через компоненты Rigidbody2D/Collider2D в инспекторе). Для проекта-головоломки, где обычно достаточно стандартных Rigidbody2D/Collider2D и обычных 2D-коллайдеров, переход на LowLevelPhysics2D оправдан только при потребности в многопоточной символьной физике или DOTS-архитектуре; для типовой 2D-головоломки на URP это, по всей видимости, избыточно — но это вывод агента, а не цитата из источника, и его стоит перепроверить под конкретные требования проекта.

## Известные регрессии и ломающие изменения при переходе на 6.3

### Официально анонсированные breaking changes

Ветка обсуждения Unity Discussions "Planned breaking changes in Unity 6.3" открыта напрямую: [discussions.unity.com — planned-breaking-changes-in-unity-6-3](https://discussions.unity.com/t/planned-breaking-changes-in-unity-6-3/1646418)

Ключевые пункты (не 2D-специфичные, но важные при апгрейде):
- "We are removing the 'Legacy ETC' compression mode, as it depends on a third party component which is no longer supported." Проекты автоматически переключаются на текущий компрессор ETC по умолчанию — это может изменить визуальные артефакты сжатых текстур (актуально для Android/ETC-текстур в проекте).
- Тип `Scene.handle` меняется "from `int` to `SceneHandle`" — новый тип поддерживает неявное преобразование в/из int, поэтому обычные C#-скрипты работают без изменений, но прекомпилированные сборки может потребоваться пересобрать.
- URP Compatibility Mode: код Compatibility Mode по умолчанию будет вырезаться (stripped), если не добавить `URP_COMPATIBILITY_MODE` в scripting defines — актуально при апгрейде проекта на URP, использующего Compatibility Mode.
- Удаляется экспериментальный API `AdditionalBakedProbes` — миграция на `IProbeIntegrator`.
- Более строгий парсер USS (UI Toolkit) — ранее пропускавшиеся синтаксические ошибки и неподдерживаемые CSS-конструкции теперь будут выявляться.

### Регрессии производительности, зафиксированные сообществом после апгрейда

- Ветка "Performance regression in Unity 6.3 BiRP" (открыта напрямую): пользователь сообщает, что после апгрейда проекта с 6.0.58 на 6.3, без изменений кода, "My frame times are around 2.3ms in Unity 6.3 and 1.6ms in Unity 6.0 when using non-development builds" — то есть время кадра выросло примерно с 1.6 мс до 2.3 мс на идентичной сцене; в профилировщике сначала заметен рост `Gfx.WaitForGfxCommandsFromMainThread` с ~0.2 мс до ~1.5 мс, но по итогу "everything is taking a bit more time on 6.3" по нескольким операциям рендеринга. [discussions.unity.com — performance-regression-in-unity-6-3-birp](https://discussions.unity.com/t/performance-regression-in-unity-6-3-birp/1700256)
- Также зафиксированы (по данным WebSearch, страницы не открывались напрямую этим агентом — помечаю как непроверено напрямую, но источник официальный форум Unity): "HDRP Performance Regression After Upgrading from Unity 6.2 to 6.3" и "Performance regression in Unity 6.3 XR URP Post Processing" — обе ветки на discussions.unity.com, обе про 3D-рендеринг (HDRP, XR), не про 2D URP напрямую, но подтверждают общий паттерн регрессий рендеринга в 6.3 у части пользователей. Ссылки: [discussions.unity.com/t/hdrp-performance-regression-after-upgrading-from-unity-6-2-to-6-3/1691742](https://discussions.unity.com/t/hdrp-performance-regression-after-upgrading-from-unity-6-2-to-6-3/1691742), [discussions.unity.com/t/performance-regression-in-unity-6-3-xr-urp-post-processing/1715174](https://discussions.unity.com/t/performance-regression-in-unity-6-3-xr-urp-post-processing/1715174).

### Известные баги на релизе 6000.3.0f1 (не устранены полностью на момент выхода)

- "Metal: [iOS] Screen flashing after the iOS splash screen (UUM-121453)" — мигание экрана на iOS-устройствах после сплэш-скрина при использовании Metal; сообщается в разных версиях iOS, воспроизводится при смене ориентации, после звонка, при скриншоте. [omitram.com — Unity 6.3 LTS (6000.3.0f1) Full Release Notes & Breakdown](https://omitram.com/unity-6-3-lts-6000-3-0f1-full-release-notes-breakdown/)
- "Metal: Game freezes after command buffer Timeout error" (UUM-125778 по WebSearch-данным, не перепроверено отдельным WebFetch по issue tracker) — потенциальный фриз игры на Metal.
- "IL2CPP: [iOS] [Android] External library generics fail during IL2CPP build (UUM-125284)" — проблема сборки IL2CPP с generics во внешних библиотеках, актуально при использовании сторонних .dll на iOS/Android.

Практический вывод: перед стартом или переходом проекта на 6.3 стоит явно протестировать сборку под iOS/Metal на реальном железе (сплэш-скрин, смена ориентации, работа в фоне/после звонка) и проверить IL2CPP-сборку, если используются сторонние библиотеки с generics.

## Требования к железу и к версии Xcode для сборки под iOS

Страница Unity Manual "System requirements for Unity 6.3" открыта напрямую: [docs.unity3d.com — system-requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)

**Требования к машине разработки (Unity Editor):**
- macOS: "Ventura 13 or newer".
- Windows: "Windows 10 version 21H1 (build 19043) or newer (X64), Windows 11 21H2 (build 22000) or newer (Arm64)".
- Linux: "Ubuntu 22.04, Ubuntu 24.04".
- Память: "8 GB RAM is recommended" как минимум.
- Процессор: "X64 architecture with SSE2 instruction set support" или "Apple M1 or above (Apple silicon-based processors)".
- Графика: "DX10, DX11, DX12 or Vulkan capable GPUs" на Windows; "Metal-capable Intel and AMD GPUs" на macOS.
- Особенность Apple Silicon: "Rosetta 2 is required for Apple silicon devices running on either Apple silicon or Intel versions of the Unity Editor"; кроме того, "Unity doesn't support CPU lightmapping for Apple silicon devices, only GPU lightmapping".

**Требования к сборке под iOS/iPadOS (та же страница):**
- Минимальная версия ОС устройства: "15+" (то есть iOS/iPadOS 15 и новее).
- Минимальное железо устройства: "A8 SoC+".
- Графический API: "Metal".
- Инструменты разработки: "Xcode version 16 or later".

Дополнительно страница "iOS requirements and compatibility" (открыта напрямую) подтверждает и дополняет: [docs.unity3d.com — ios-requirements-and-compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-requirements-and-compatibility.html)
> "Unity supports iOS 15 and above."
> "When developing for iOS, it's recommended to use Xcode version 16 or later."

Про сборочную машину важно и то, что macOS необходима для локальной сборки в принципе, так как Xcode существует только под macOS: страница "How Unity builds iOS applications" (открыта напрямую) подтверждает — "Xcode is only available for macOS, so if your development machine doesn't run macOS, you can't build an application locally." [docs.unity3d.com — how-unity-builds-ios-applications](https://docs.unity3d.com/6000.3/Documentation/Manual/how-unity-builds-ios-applications.html). Сам процесс сборки двухэтапный: Unity сначала генерирует Xcode-проект, затем Xcode компилирует его в приложение.

**Отдельное и более строгое требование именно для публикации в App Store** (получено через WebSearch по официальной документации Unity, отдельным WebFetch не перепроверялось — формально это правило Apple, транслируемое Unity): для отправки iOS/iPadOS-приложения в App Store требуется собирать его Xcode 26.0 или новее, на macOS Sequoia, с SDK для iOS 26 или iPadOS 26; более старым Xcode собрать приложение можно, но отправить в App Store — нельзя. Это отдельное требование от минимальной версии Xcode для разработки (16+) и относится к моменту публикации, а не к возможности сборки/тестирования.

Практический вывод для проекта: для ежедневной разработки/тестирования достаточно Xcode 16+ на macOS Ventura 13+ (Apple Silicon Mac, минимум 8 ГБ ОЗУ), но перед релизом в App Store нужно обновиться до текущей версии Xcode, требуемой Apple на момент публикации (значение "26" зафиксировано WebSearch-агрегацией на дату сбора и может измениться — Apple периодически поднимает планку минимального Xcode для App Store; перепроверять на developer.apple.com перед каждым релизом).

## Источники

- [endoflife.date/unity](https://endoflife.date/unity) — таблица версий, дат релизов и окончания поддержки Unity 6.x.
- [docs.unity3d.com — WhatsNewUnity63](https://docs.unity3d.com/6000.3/Documentation/Manual/WhatsNewUnity63.html) — официальные release notes "New in Unity 6.3".
- [docs.unity3d.com — 2d-physics-api-introduction](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-introduction.html) — LowLevelPhysics2D API на Box2D v3.
- [docs.unity3d.com — 2d-physics-api-workflow](https://docs.unity3d.com/6000.3/Documentation/Manual/2d-physics-api/2d-physics-api-workflow.html) — workflow PhysicsWorld/PhysicsBody/PhysicsShape.
- [docs.unity3d.com — system-requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html) — требования к Editor и к сборке под iOS (Xcode, iOS-версия, железо).
- [docs.unity3d.com — ios-requirements-and-compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-requirements-and-compatibility.html) — требования по iOS/Xcode.
- [docs.unity3d.com — how-unity-builds-ios-applications](https://docs.unity3d.com/6000.3/Documentation/Manual/how-unity-builds-ios-applications.html) — процесс сборки iOS из Unity.
- [omitram.com — Unity 6.3 LTS (6000.3.0f1) Full Release Notes & Breakdown](https://omitram.com/unity-6-3-lts-6000-3-0f1-full-release-notes-breakdown/) — независимый разбор release notes 6000.3.0f1, известные баги.
- [unityreleases.com/releases/6000.3.22f1](https://unityreleases.com/releases/6000.3.22f1) — независимый агрегатор, детали патча 6000.3.22f1.
- [discussions.unity.com — planned-breaking-changes-in-unity-6-3](https://discussions.unity.com/t/planned-breaking-changes-in-unity-6-3/1646418) — официально анонсированные breaking changes.
- [discussions.unity.com — performance-regression-in-unity-6-3-birp](https://discussions.unity.com/t/performance-regression-in-unity-6-3-birp/1700256) — регрессия производительности BiRP после апгрейда.
- [discussions.unity.com — hdrp-performance-regression-after-upgrading-from-unity-6-2-to-6-3](https://discussions.unity.com/t/hdrp-performance-regression-after-upgrading-from-unity-6-2-to-6-3/1691742) — регрессия HDRP (не открыта напрямую, только через WebSearch).
- [discussions.unity.com — performance-regression-in-unity-6-3-xr-urp-post-processing](https://discussions.unity.com/t/performance-regression-in-unity-6-3-xr-urp-post-processing/1715174) — регрессия XR/URP post-processing (не открыта напрямую, только через WebSearch).
- unity.com/releases/unity-6/support, unity.com/releases/editor/archive, unity.com/blog/unity-6-3-lts-is-now-available — официальные страницы Unity, при попытке WebFetch отдают HTTP 403 (защита от ботов); факты с них перепроверены через альтернативные источники выше.






