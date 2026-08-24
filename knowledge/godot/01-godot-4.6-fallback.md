# Godot как запасной путь при самостоятельном издании

Дата сбора сведений: 2026-08-24.

## Кратко

- Godot 4.6-stable вышел 26 января 2026, ветка дошла до 4.6.3-stable (20 мая 2026). Но уже вышла ветка 4.7: 4.7-stable — 18 июня 2026, текущий патч на дату сбора — 4.7.2-stable (18 августа 2026). [1][2]
- По политике проекта стабильная ветка активно поддерживается только до первого патча преемника; 4.7.1 вышел 14 июля 2026 — значит формально ветка 4.6 уже перешла в режим «частичной» поддержки, а не полноценной. Планировать разработку именно на 4.6.3 как на «текущий стабильный» неточно: актуальная стабильная ветка на дату сбора — 4.7.x. [3]
- Экспорт под iOS требует macOS с установленным Xcode; версии macOS/Xcode в официальной документации не названы явно. [4]
- Официальная документация по-прежнему называет экспорт C#-проектов на iOS «экспериментальным, с ограничениями», хотя ряд обзорных статей 2026 года пишет о нём как о рабочем канале. [4][5]
- Измеренного размера пустой iOS-сборки Godot 4.6 в открытых источниках не найдено.
- Живые плагины для iOS: AdMob — `godot-sdk-integrations/godot-admob` (112 звёзд, обновлён 27 мая 2026); StoreKit 2 — `godot-sdk-integrations/godot-storekit2` (19 звёзд, обновлён 27 апреля 2026, авторы прямо пишут, что API нестабилен). Старый официальный плагин на StoreKit 1 считается устаревшим и уязвимым. [6][7][8]
- MCP-серверов для Godot на GitHub много и они активно живут; самый крупный по звёздам — `Coding-Solo/godot-mcp` (5348 звёзд), самый свежий по коммитам на дату сбора — `hi-godot/godot-ai` (1890 звёзд, коммит в день сбора). [9]
- GDScript в 4.6 получил оптимизации байткода, но по компьютерно-тяжёлым задачам C# остаётся быстрее; разрыв сократился, но не исчез. [5]
- `.tscn`/`.tres` — текстовый формат из пяти секций; ломается ручным редактированием чаще всего на несовпадении `id`/`uid` в ссылках `ExtResource(...)`/`SubResource(...)` и на путях `parent="..."`.
- Переход с Unity на Godot ради самостоятельного издания оправдан прежде всего экономией на лицензиях и меньшим весом рантайма; стоимость — переписывание C#/Unity-специфичного кода, менее зрелая экосистема IAP/Ads-плагинов для iOS и вероятно более слабое знание GDScript у языковых моделей по сравнению с C#/Unity API.

## Версии и даты выпуска

Официальная страница релиза 4.6 называется «It's all about your flow» и перечисляет как основные изменения новую тему редактора Modern, объединённую систему докинга, Jolt Physics по умолчанию для новых 3D-проектов, новый фреймворк IK и переписанные Screen Space Reflections. [1]

Даты релизов ветки 4.6.x и 4.7.x, полученные напрямую из GitHub API (`gh api repos/godotengine/godot/releases`, проверено 2026-08-24):

```
4.6-stable      2026-01-26T14:05:33Z
4.6.1-stable    2026-02-16T20:26:38Z
4.6.2-stable    2026-04-01T19:12:33Z
4.6.3-stable    2026-05-20T20:49:16Z
4.7-stable      2026-06-18T12:06:17Z
4.7.1-stable    2026-07-14T18:03:10Z
4.7.2-stable    2026-08-18T16:12:28Z
```
[2]

Godot 4.6.3 — рядовой релиз обслуживания: «41 contributors submitted 86 fixes for this release... no known incompatibilities with the previous Godot 4.6.2 release». [10]

### Состояние 4.7

Godot 4.7 официально вышел 18 июня 2026 под кодовым названием «Lights, Camera, Action!». Ключевые новшества: поддержка HDR-вывода на десктопе (iOS и веб — не поддерживаются), узел `AreaLight3D`, `DrawableTexture2D`, независимые transform-офсеты для Control-узлов, обновлённый Asset Store вместо старой Asset Library, встроенный виртуальный джойстик для тачскринов, инструменты автономной сборки и публикации под Android. На дату сбора вышло уже два релиза обслуживания (4.7.1, 4.7.2). [11][12]

Практический вывод: если проект стартует сейчас, разумнее ориентироваться на актуальную ветку 4.7.x, а не «замораживаться» на 4.6.3 — если, конечно, нет специфической причины остаться на 4.6 (например, зависимость от плагина, ещё не портированного под 4.7).

## Политика поддержки версий

Официальная документация о релизах: «Stable branches are supported at minimum until the next stable branch is released and has received its first patch update» — то есть минимальный срок поддержки ветки 4.6 истёк 14 июля 2026, с выходом 4.7.1. После этой точки ветка получает не приоритетные, а лишь «best effort» исправления, «for as long as they have active users who need maintenance updates». [3]

Статус LTS (Long-Term Support) в Godot присваивается предыдущей стабильной ветке в момент выхода новой *мажорной* версии (например, ветка 3.x стала LTS с выходом 4.0) — «the team does their best to provide fixes for issues encountered by users of that branch who cannot port complex projects to the new major version». Отдельного официального LTS-статуса у минорных веток внутри линии 4.x (типа «4.6 LTS») нет — это обычная стабильная ветка с обычным сроком поддержки. [3]

Внутри цикла обслуживания критерии для попадания исправления в патч-релиз стабильной ветки жёсткие: «no new features (unless necessary to enable platform support), and no risky bugfixes unless absolutely critical», рассматриваются в первую очередь исправления безопасности и требования новых платформенных политик. [13]

## Экспорт под iOS из Godot 4.6/4.7

### Требования и шаги

Официальная документация фиксирует жёсткое требование: «You must export for iOS from a computer running macOS with Xcode installed» — точные версии macOS/Xcode в тексте руководства не называются. [4]

Порядок действий по документации:
1. Editor → Manage Export Templates — загрузить экспортные шаблоны.
2. Project → Export — открыть окно экспорта, добавить пресет iOS.
3. Заполнить обязательные параметры App Store Team ID и Bundle Identifier — «Leaving them blank will cause the exporter to throw an error».
4. Экспортировать проект — Godot создаёт Xcode-проект (`.xcodeproj`), который дальше собирается и подписывается стандартными средствами Xcode. [4]

Частая проблема — `xcode-select` указывает не туда: «Godot is trying to find the Platforms folder containing the iPhone SDK inside the /Library/Developer/CommandLineTools/ folder, but the Platforms folder with the iPhone SDK is actually located under /Applications/Xcode.app/Contents/Developer» — лечится командой `xcode-select` с указанием правильного пути. [4]

### Размер пустой сборки

Измеренных данных о размере пустой iOS-сборки Godot 4.6 (ни в официальной документации, ни в найденных независимых источниках) не найдено.

### C# на iOS

Официальная страница экспорта под iOS (актуальная стабильная версия документации на дату сбора) прямо пишет: «Projects written in C# can be exported to iOS as of Godot 4.2, but support is experimental and some limitations apply». [4] Обзорные материалы 2026 года формулируют мягче — «C# exports work for Android and iOS», называя веб (HTML5) единственной официально недостижимой для C# платформой, консольные экспорты через W4 Games — в бета-статусе. Оба утверждения не противоречат друг другу: технически экспорт работает, но официальный статус — экспериментальный с ограничениями, и полагаться на него для продакшн-релиза стоит с запасом времени на обход возможных проблем. [5]

## Плагины покупок в приложении и рекламы на iOS

### AdMob

Действующий и поддерживаемый вариант — `godot-sdk-integrations/godot-admob`: «A Godot plugin that provides a unified GDScript interface for integrating Google Mobile Ads SDK on Android and iOS», поддерживает баннеры, интерстициальные, rewarded, rewarded-interstitial, app-open и native-форматы, посредничество (медиацию) с до 15 дополнительными рекламными сетями, встроенный UMP-флоу согласия (GDPR) и обработку iOS App Tracking Transparency. Проверено на GitHub API 2026-08-24: 112 звёзд, последний push 2026-05-27, не архивирован. [6]

Альтернатива — монорепозиторий `poingstudios/godot-admob-plugin` («Complete AdMob... Supports GDScript and C#»); их прежний отдельный iOS-репозиторий `cengiz-pz/godot-ios-admob-plugin` заархивирован (последний push 2025-08-05, `archived: true` по данным GitHub API), что подтверждает миграцию в монорепозиторий. [14]

### StoreKit / внутриигровые покупки

Действующий современный плагин — `godot-sdk-integrations/godot-storekit2`: «iOS plugin for Godot integrating the StoreKit 2 API». Проверено на GitHub API 2026-08-24: 19 звёзд, последний push 2026-04-27. Разработчики сами предупреждают: «this plugin is still in ongoing development so the API isn't stable and there might be bugs». [7]

Старый официальный плагин `inappstore` (часть `godot-sdk-integrations/godot-ios-plugins`) использует устаревший StoreKit 1: в issue-трекере прямо указано на уязвимость — «unlike the Android Billing plugin, there is no way to query_purchases() and find out what the user has purchased/subscribed to when the app starts up» — и на то, что «Storekit 1 is deprecated» (объявлено на WWDC 24). Для нового проекта разумнее сразу закладывать StoreKit 2 через один из современных плагинов. [8]

Альтернатива на Swift-обвязке — `atlasapplications/godot-store-kit` (версия 1.5, совместима с Godot 4.5.1, SwiftGodot 0.74.0, iOS 17+); авторы отмечают, что часть API (в частности подписки) реализована не полностью. [15]

## Формат сцен `.tscn`/`.tres`

Файл `.tscn` — «text scene» — текстовое представление дерева сцены, состоит из пяти секций: file descriptor, external resources, internal resources (sub-resources), nodes, connections. [16]

Пример заголовка (file descriptor), который обязан идти первым в файле:
```
[gd_scene format=3 uid="uid://cecaux1sm7mo0"]
```
Внешний ресурс и обращение к нему:
```
[ext_resource type="Material" uid="uid://c4cp0al3ljsjv" path="material.tres" id="1_7bt6s"]
...
material = ExtResource("1_7bt6s")
```
Внутренний ресурс:
```
[sub_resource type="CapsuleShape" id=2]
radius = 0.5
height = 3.0
```
Свойства, равные значению по умолчанию, в файл не пишутся: «properties equal to the default value are not stored in scene/resource files». В Godot 4 введены строковые UID вместо инкрементных целочисленных идентификаторов — именно они позволяют движку не терять ссылку на файл при его перемещении в файловой системе. [16]

Почему такой формат удобно править агентом: это обычный человекочитаемый текст (в отличие от бинарного `.scn`), построчная структура, git-диффы читаемы. Опасные места, которые агент может сломать при неосторожном редактировании:
- ссылки `ExtResource("id")` / `SubResource("id")` — опечатка или рассинхронизация `id` при удалении/добавлении ресурса рвёт связь без явной ошибки парсинга;
- атрибут `parent="..."` у `[node ...]` — задаёт место узла в дереве через путь; неверный путь ломает иерархию;
- `NodePath(...)` внутри значений свойств — тоже путь по дереву сцены, не проверяется на этапе редактирования текста;
- `uid://...` в заголовке и во `ext_resource` — должен соответствовать реальному UID-индексу проекта; ручная правка uid без синхронизации с `.godot/uid_cache.bin` может привести к тому, что редактор Godot не найдёт файл.

## MCP-серверы для Godot

Данные по звёздам и дате последнего push получены напрямую через GitHub API (`gh api repos/<owner>/<repo>`, проверено 2026-08-24):

| Репозиторий | Звёзды | Последний push | Архивирован |
|---|---|---|---|
| [Coding-Solo/godot-mcp](https://github.com/Coding-Solo/godot-mcp) | 5348 | 2026-04-16 | нет |
| [hi-godot/godot-ai](https://github.com/hi-godot/godot-ai) | 1890 | 2026-08-24 | нет |
| [IvanMurzak/Godot-MCP](https://github.com/IvanMurzak/Godot-MCP) | 223 | 2026-08-16 | нет |
| [n24q02m/better-godot-mcp](https://github.com/n24q02m/better-godot-mcp) | 34 | 2026-08-23 | нет |
| [mkdevkit/godot-mcp](https://github.com/mkdevkit/godot-mcp) | 11 | 2026-06-09 | нет |
| [hybridindie/godot-mcp](https://github.com/hybridindie/godot-mcp) | 1 | 2026-08-09 | нет |

`Coding-Solo/godot-mcp` — самый популярный, «provides tools for launching the editor, running projects, and capturing debug output», использует прямые команды для простых операций и встроенный GDScript-файл для сложных (создание сцен, добавление узлов). [9]

`hi-godot/godot-ai` — «Production-grade MCP server and AI tools for the Godot engine», по README даёт «120+ операций через ~43 MCP инструмента» для сцен, узлов, сигналов, материалов, анимаций; требует Godot 4.5+ и `uv` для Python-части сервера; устанавливается из исходников, ZIP-релиза или через Asset Library/Asset Store. [17]

`IvanMurzak/Godot-MCP` написан на C#, «AI-powered game development assistant for the Godot Editor», 42 встроенных инструмента в 12 группах, с опциональным облачным подключением к ai-game.dev, лицензия Apache-2.0. [9]

## GDScript против C#

Быстродействие: Godot 4.6 получил «bytecode and method-call optimisations» для GDScript, «gains most pronounced for typed GDScript». При этом «C# still holds a performance edge... the gap narrowed but did not close, and C# remains measurably faster than GDScript for compute-heavy work». [5]

iOS: GDScript не имеет платформенных ограничений («GDScript has none of these platform restrictions... exports everywhere including the web»); C# официально экспериментален на iOS согласно официальной документации (см. раздел про экспорт выше). Важное отдельное ограничение C#: он не может напрямую вызывать GDExtension — «you cannot call GDExtensions directly from C#, and if that's an immediate must-have for your project, you should not use C#». [4][5]

Знание моделей о языке: прямых измерений в открытых источниках не найдено. Косвенно можно предположить, что качество ответов LLM по GDScript ниже, чем по C#/Unity API, просто в силу существенно меньшего объёма обучающих материалов и меньшей популярности движка по сравнению с Unity — это оценочное суждение, а не подтверждённая цифра.

## Честная сводка: когда переход с Unity на Godot оправдан

Переход имеет смысл, если совпадает несколько условий:
- игра издаётся самостоятельно, без паблишера, у которого уже есть требования к конкретному движку/SDK;
- команда готова взять на себя риск менее зрелой экосистемы IAP/Ads-плагинов на iOS (см. разделы выше — плагины живые, но малые по числу звёзд и сами разработчики предупреждают о нестабильности API, в отличие от официальных Unity IAP/Ads пакетов);
- проект достаточно прост в 2D-части, чтобы не упереться в экспериментальный статус C# на iOS — то есть логика пишется на GDScript;
- важны открытая лицензия и отсутствие роялти/подписки на движок (у Godot их нет в принципе — это не требует проверки цен, поскольку факт архитектурный, а не коммерческий).

Стоимость перехода складывается из: переписывания игровой логики с C#/Unity API на GDScript (или принятия рисков экспериментального C#-экспорта), пересборки шейдерного и анимационного пайплайна под систему Godot, повторной интеграции покупок и рекламы через менее обкатанные плагины, и, вероятно, более медленной работы с ИИ-агентами из-за более скромного объёма обучающих данных по GDScript по сравнению с C#. Числовой оценки стоимости в часах/деньгах в открытых источниках не найдено — любая такая цифра была бы выдумкой.

## Источники

1. [Godot 4.6 Release: It's all about your flow — godotengine.org](https://godotengine.org/releases/4.6/)
2. [godotengine/godot — Releases (GitHub API)](https://github.com/godotengine/godot/releases)
3. [Godot release policy — docs.godotengine.org (stable)](https://docs.godotengine.org/en/stable/about/release_policy.html)
4. [Exporting for iOS — docs.godotengine.org (stable)](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_ios.html)
5. [GDScript vs C# in Godot 2026: Choosing Your Scripting Language — StraySpark](https://www.strayspark.studio/blog/gdscript-vs-csharp-godot-2026-choosing-scripting-language)
6. [godot-sdk-integrations/godot-admob — GitHub](https://github.com/godot-sdk-integrations/godot-admob)
7. [godot-sdk-integrations/godot-storekit2 — GitHub](https://github.com/godot-sdk-integrations/godot-storekit2)
8. [Storekit 1 is deprecated · Issue #68 — godot-sdk-integrations/godot-ios-plugins](https://github.com/godot-sdk-integrations/godot-ios-plugins/issues/68)
9. [Godot MCP server GitHub search results / repositories](https://github.com/Coding-Solo/godot-mcp)
10. [Maintenance release: Godot 4.6.3 — godotengine.org](https://godotengine.org/article/maintenance-release-godot-4-6-3/)
11. [Godot 4.7 Release — godotengine.org](https://godotengine.org/releases/4.7/)
12. [What's New in Godot 4.7? — Vagon](https://vagon.io/blog/what-s-new-in-godot-4-7)
13. [Maintenance release process — contributing.godotengine.org](https://contributing.godotengine.org/en/latest/other/release_management/maintenance_releases.html)
14. [cengiz-pz/godot-ios-admob-plugin — GitHub (archived)](https://github.com/cengiz-pz/godot-ios-admob-plugin)
15. [atlasapplications/godot-store-kit — GitHub](https://github.com/atlasapplications/godot-store-kit)
16. [TSCN file format — docs.godotengine.org (stable)](https://docs.godotengine.org/en/stable/engine_details/file_formats/tscn.html)
17. [hi-godot/godot-ai — GitHub](https://github.com/hi-godot/godot-ai)
18. [endoflife.date/godot](https://endoflife.date/godot)
