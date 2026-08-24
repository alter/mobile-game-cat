# GameAnalytics в Unity 6.3 LTS (iOS): практическое применение

Дата составления: 2026-08-24.

Область: игра — 2D-головоломка, iOS, Unity 6.3 LTS (6000.3.x), C#. Средство выбрано (GameAnalytics), документ описывает применение: установку, настройку, вызовы девяти событий, ограничения, проверку доходимости, поведение без сети, требования Apple, работу в веб-кабинете и известные подводные камни.

Первоисточники: docs.gameanalytics.com (актуальная документация Unity SDK) и репозиторий GitHub GameAnalytics/GA-SDK-UNITY (файл `Runtime/Scripts/GameAnalytics.cs`, `CHANGELOG.md`, issues репозитория). Там, где данные не подтверждены документацией или исходным кодом, отмечено «не проверено».

## Кратко

- Текущая версия пакета — **8.1.0** (выпущена 2026-08-21, за три дня до составления документа). Минимальная поддерживаемая версия Unity **поднята до 2022.3 LTS**; отдельного явного подтверждения «Unity 6.x» в документации не найдено, но 2022.3+ формально покрывает линейку 6000.x. При этом в репозитории есть открытая проблема совместимости с Unity 6.5 (issue #57) и открытая проблема падения на Unity 6.3 в **Standalone**-сборке (issue #58, не iOS).
- Установка — через Unity Package Manager по scoped registry `package.openupm.com` (пакет `com.gameanalytics.sdk`), либо через `.unitypackage`, либо напрямую через OpenUPM CLI. Для iOS с версии 8.1.0 внешние нативные зависимости стали одним `GameAnalytics.xcframework`.
- Настройка ключей — не через код, а через объект-редактор: `Window → GameAnalytics → Select Settings` создаёт ScriptableObject-настройки в проекте; в нём отдельно добавляется платформа iOS («Add Platform»), и Game Key/Secret Key для iOS хранятся отдельно от Android.
- Инициализация — **ручная**, вызовом `GameAnalytics.Initialize()`; обязательно после того, как показан (или явно пропущен) диалог ATT, и после `Start()`, а не `Awake()`, чтобы гарантировать порядок инициализации.
- Все девять целевых событий укладываются в два вида GameAnalytics: **Progression** (для `level_start/level_win/level_fail`) и **Design** (для всех остальных шести). Ни Business, ни Resource, ни Ad, ни Error для них не подходят.
- У имени Design-события жёсткий серверный регламент: до 5 частей через «:», каждая часть 1–64 символа из ограниченного набора знаков; при нарушении событие **не отбрасывается сразу на клиенте**, а отклоняется коллектором на сервере — внешне это выглядит как «событие пропало».
- Debug-логи для Unity включаются не рантайм-вызовом, а **чекбоксами в инспекторе объекта настроек** (Info Log Build / Verbose Log Build) — это отличается от Android/iOS-native/JS SDK, где есть `SetEnabledInfoLog`/`SetEnabledVerboseLog` как публичные функции.
- Пакет сам ставит события в очередь и досылает их при восстановлении сети — писать собственную офлайн-очередь не нужно (подтверждено для семейства SDK, для Unity-обёртки это делегируется нативному iOS SDK).
- В пакете есть `PrivacyInfo.xcprivacy` внутри `GameAnalytics.xcframework` — обязателен для App Store и обнаружен непосредственно в исходниках репозитория. SDK не обязан показывать диалог ATT сам: показ ATT — действие разработчика (`GameAnalytics.RequestTrackingAuthorization()` вызывается по желанию); если его не вызывать, диалога ATT не будет, а `EnableAdvertisingIdTracking(false)` дополнительно отключает использование IDFA и любые связанные с рекламной атрибуцией эффекты.
- Задержка на сервере: обычные дашборды — до суток («под 24 часа» по неофициальным источникам сообщества), «живой» просмотр (Realtime) — события появляются в течение примерно 30 секунд, но только последние 50 событий.

## 1. Установка

### 1.1. Текущая версия и поддержка Unity

Из `CHANGELOG.md` репозитория (актуальная запись на верху файла):

```
## [8.1.0] - 2026-08-21

### Changed
- Raised the minimum supported Unity version to 2022.3 (LTS)
- iOS/tvOS: replaced the static libraries with a single `GameAnalytics.xcframework` (updated GameAnalytics iOS SDK to 5.0.2)
- Standalone (Windows/macOS/Linux): updated the native C++ SDK to 5.4.0

### Removed
- The External Dependency Manager (EDM4U) requirement — the Android SDK is now fully self-contained
- Support for deprecated platforms: UWP/WSA, Tizen, Samsung TV and Windows Phone 8.1
```

Это подтверждено релизом на GitHub: тег `8.1.0`, дата публикации `2026-08-21T09:06:31Z` (получено через `GET /repos/GameAnalytics/GA-SDK-UNITY/releases/latest`).

Формально минимальная версия — **Unity 2022.3 LTS**, что покрывает и Unity 6.3 LTS (6000.3.x), поскольку линейка 6000.x вышла позже и заявлена как совместимая (сама страница «Get Started» до недавнего изменения указывала «Unity 2019.4+» — формулировка устарела относительно CHANGELOG, поэтому за основу взят CHANGELOG как более свежий источник). Прямого предложения «протестировано на Unity 6.3» в документации не найдено — **не проверено** документацией явно, но есть практические сигналы:

- Issue **#57** («Unity 6.5 Compile Errors: `EditorApplication.hierarchyWindowItemOnGUI` и `EditorUtility.InstanceIDToObject` obsolete», открыт, репозиторий GA-SDK-UNITY): при апгрейде на Unity 6.5 (и вероятно последующие тех-стримы 6000.x) сборка падает с `CS0619`, потому что `Runtime/Scripts/GameAnalytics.cs` использует устаревший API редактора Hierarchy. На момент составления документа не подтверждено, проявляется ли это именно на 6.3.
- Issue **#58** («[Unity 6.3] Windows build crashes with gameanalytics.dll», открыт): подтверждённое падение именно на Unity 6.3 (`fileVersion: 6000.3.16.28451`), но в **Standalone** (Windows) сборке, а не iOS; трасса указывает на нативный мьютекс в `gameanalytics.dll` (кроссплатформенный C++-компонент). К iOS отношения не имеет напрямую, но показывает, что 8.1.0 на линейке 6000.3.x имеет открытые баг-репорты.

### 1.2. Способы установки

Из `README.md` репозитория и официальной документации (`docs.gameanalytics.com/.../unity`) — три способа:

**1) Unity Package Manager (git/scoped registry)**

В `Packages/manifest.json` добавляется зависимость и scoped registry:

```json
{
  "dependencies": {
    "com.gameanalytics.sdk": "[latest_version]"
  },
  "scopedRegistries": [
    {
      "name": "Game Package Registry by Google",
      "url": "https://unityregistry-pa.googleapis.com/",
      "scopes": ["com.google"]
    },
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.gameanalytics"]
    }
  ]
}
```

Официальная инструкция по-прежнему требует до этого установить **External Dependency Manager for Unity** (EDM4U) как `.tgz`-пакет — это первый шаг в текущей странице установки. Здесь есть противоречие с CHANGELOG 8.1.0, который заявляет «Removed: The External Dependency Manager (EDM4U) requirement — the Android SDK is now fully self-contained». Из этого следует: страница установки, вероятно, не обновлена под 8.1.0, либо EDM4U остаётся нужен для сопутствующих Google-пакетов (реестр `com.google`), а не для самого ядра GameAnalytics. **Не проверено** окончательно — стоит на практике попробовать установить без EDM4U и проверить, требуется ли он для iOS-сборки; для Android CHANGELOG прямо говорит, что не требуется.

**2) `.unitypackage`**

```
https://download.gameanalytics.com/unity/GA_SDK_UNITY.unitypackage
```

Именно этот способ используется, если нужен ILRD (Impression Level Revenue Data от рекламных сетей) — обычный UPM-путь для ILRD не подходит из-за зависимостей на Ad SDK, которых нет в UPM:

```
https://download.gameanalytics.com/unity/GA_ILRD_UNITY.unitypackage
```

**3) OpenUPM**

Пакет `com.gameanalytics.sdk` официально зарегистрирован на OpenUPM (`https://openupm.com/packages/com.gameanalytics.sdk/`, репозиторий указан как `GameAnalytics/GA-SDK-UNITY`), устанавливается тем же scoped-registry способом (см. п.1) либо через `openupm-cli`. Точный номер версии со страницы OpenUPM программно не подтверждён (страница рендерится через JS), но последний релиз GitHub — 8.1.0.

### 1.3. Что кладётся в проект

- Код и ресурсы пакета — в `Library/PackageCache/com.gameanalytics.sdk@...` (если ставили через UPM) со структурой репозитория: `Runtime/Scripts/*` (обёртка C#, публичный API `GameAnalytics`), `Runtime/Apple/GameAnalytics.xcframework/*` (нативный iOS/tvOS код + `PrivacyInfo.xcprivacy`, см. раздел 7), `Editor/*` (кастомный инспектор настроек, мастер логина, пост-процессинг сборки).
- В **пользовательской** части проекта (`Assets/...`) ничего автоматически не создаётся при установке пакета — объект настроек (см. раздел 2) создаётся отдельным действием `Window → GameAnalytics → Select Settings`, а GameObject-инициализатор — действием `Window → GameAnalytics → Create GameAnalytics object`, который добавляется в текущую открытую сцену.
- Размер в собранном iOS-приложении, по данным README: **≈242 Кб (armv7) / ≈259 Кб (armv8)** — это старые цифры для предыдущих версий SDK, актуальный размер для 5.0.2/xcframework отдельно не подтверждён («не проверено»).

## 2. Настройка

### 2.1. Объект настроек и вход в кабинет

Ключи вводятся не кодом, а через встроенный редакторский инструмент:

`Window → GameAnalytics → Select Settings` — если объекта настроек ещё нет, Unity создаёт его автоматически (это ScriptableObject-ассет, класс `GameAnalyticsSDK.Setup.Settings` — файл `Runtime/Scripts/Setup/Settings.cs`). Дальше в инспекторе:

1. Кнопка **Login** — вход учётными данными аккаунта GameAnalytics.
2. Выбор платформы и **Add Platform** — при этом Game Key и Secret Key подтягиваются автоматически из веб-кабинета для выбранной студии/игры; либо ключи можно ввести вручную.

### 2.2. Раздельная настройка для iOS

Из исходного кода `Settings.cs`: список платформ хранится как `List<RuntimePlatform> Platforms`, и для каждой платформы отдельно хранятся индексы `SelectedPlatformOrganization`, `SelectedPlatformStudio`, `SelectedPlatformGame`, а ключи читаются/пишутся по индексу платформы:

```csharp
public void AddPlatform(RuntimePlatform platform)
// ...
public string GetGameKey(int index)
public string GetSecretKey(int index)
public void UpdateGameKey(int index, string value)
public void UpdateSecretKey(int index, string value)
```

Доступные платформы (`AvailablePlatforms`): `Android`, `IPhonePlayer` (iOS), `LinuxPlayer`, `OSXPlayer`, `tvOS`, `WebGLPlayer`, `WindowsPlayer`. Из этого следует: iOS (`RuntimePlatform.IPhonePlayer`) и Android — это две **независимые** записи в одном объекте настроек, каждая со своей парой Game Key/Secret Key. При сборке под конкретную платформу SDK использует ключи, соответствующие текущей `RuntimePlatform`.

### 2.3. Инициализация

Инициализация SDK **ручная**, вызывается явно из своего скрипта:

```csharp
// SDK Key и SDK Secret берутся из объекта Settings
GameAnalytics.Initialize();
```

Документация подчёркивает два требования к порядку:

- Скрипт, вызывающий `Initialize()`, должен иметь **порядок выполнения (Script Execution Order), идущий после** скрипта GameAnalytics-объекта, если оба находятся в одной сцене — часть внутреннего кода GameAnalytics отрабатывает в `Awake()`, и это должно случиться раньше инициализации.
- Отправка событий должна происходить не раньше, чем в `Start()` (а не `Awake()`), потому что порядок `Awake()`-вызовов между разными `GameObject` не гарантирован, а GameAnalytics-объект настраивается именно в своём `Awake()`. Если событие отправлено раньше инициализации, в логе будет:

```
Warning/GameAnalytics: Could not add design event: Datastore not initialized
```

- В редакторе (Play Mode) события **не отправляются по-настоящему** — нативный код не компилируется/не используется в редакторе. Проверка боевой отправки требует сборки под целевую платформу (см. раздел 5).

### 2.4. GameAnalytics GameObject

Обязателен один (и только один) `GameObject` с компонентом GameAnalytics в стартовой сцене:

`Window → GameAnalytics → Create GameAnalytics object`

Объект не уничтожается при смене сцен (`DontDestroyOnLoad` внутри реализации), поэтому создавать его повторно в других сценах не нужно — более того, наличие больше одного такого объекта в игре — ошибка конфигурации, документация явно предупреждает об этом.

### 2.5. ATT и инициализация — порядок важен

Начиная с iOS 14.5 нужно запросить разрешение через App Tracking Transparency **до** инициализации SDK, если хотите, чтобы статус ATT корректно попал в события. Официальный пример из документации (обёртка для запроса через сам SDK):

```csharp
using UnityEngine;
using GameAnalyticsSDK;

public class MyScript : MonoBehaviour, IGameAnalyticsATTListener
{
    void Start()
    {
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            GameAnalytics.RequestTrackingAuthorization(this);
        }
        else
        {
            GameAnalytics.Initialize();
        }
    }

    public void GameAnalyticsATTListenerNotDetermined()  { GameAnalytics.Initialize(); }
    public void GameAnalyticsATTListenerRestricted()      { GameAnalytics.Initialize(); }
    public void GameAnalyticsATTListenerDenied()          { GameAnalytics.Initialize(); }
    public void GameAnalyticsATTListenerAuthorized()      { GameAnalytics.Initialize(); }
}
```

Документация прямо требует: SDK нужно инициализировать **в любом случае**, даже если пользователь отклонил разрешение — GameAnalytics использует IDFV как идентификатор пользователя на iOS и добавляет IDFA к событиям только если статус ATT «authorized». Подробный разбор — как обойтись вовсе без диалога ATT — в разделе 7.

### 2.6. Пользовательский ID (кратко)

```csharp
GameAnalytics.SetCustomId("myCustomUserId");
```

Задаётся **до** `Initialize()`, иначе не применится. Внедрение в уже выпущенную игру пересчитает существующих пользователей как новых — не делать этого постфактум.

## 3. Типы событий

Namespace для всех вызовов: `using GameAnalyticsSDK;`. Ниже — сигнатуры, взятые дословно из исходного файла `Runtime/Scripts/GameAnalytics.cs` (актуальная ветка `master`, версия 8.1.0), с указанием, когда каждый вид применяется.

### 3.1. Business Event — платежи

```csharp
// Без валидации чека
GameAnalytics.NewBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType)

// iOS — с чеком
GameAnalytics.NewBusinessEventIOS(string currency, int amount, string itemType, string itemId, string cartType, string receipt)

// iOS — с автоматическим получением чека
GameAnalytics.NewBusinessEventIOSAutoFetchReceipt(string currency, int amount, string itemType, string itemId, string cartType)

// Android (Google Play)
GameAnalytics.NewBusinessEventGooglePlay(string currency, int amount, string itemType, string itemId, string cartType, string receipt, string signature)
```

Применять при реальных денежных покупках (IAP) с поддержкой валидации чека на серверах GameAnalytics. В нашей игре ни одно из девяти целевых событий сюда не относится — платных покупок в списке нет.

### 3.2. Resource Event — виртуальная экономика

```csharp
GameAnalytics.NewResourceEvent(GAResourceFlowType flowType, string currency, float amount, string itemType, string itemId)
```

`flowType` — `GAResourceFlowType.Source` (начисление) или `GAResourceFlowType.Sink` (списание). Требует заранее зарегистрированных в кабинете валют и типов предметов (максимум 20 валют и 20 типов, см. раздел 4). Годится для учёта внутриигровых ресурсов (монеты, жизни, ходы как ресурс, а не как разовое нажатие). **В нашем списке событий Resource не используется** — `moves_button_tap` это факт нажатия кнопки (UI-действие), а не собственно списание/начисление ресурса «ходы»; если бы стояла задача считать баланс ходов, это был бы кандидат на Resource, но задача — «доля нажавших» (конверсия), для чего Design подходит точнее.

### 3.3. Progression Event — прогресс по уровням

```csharp
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, int score)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, int score)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03)
GameAnalytics.NewProgressionEvent(GAProgressionStatus progressionStatus, string progression01, string progression02, string progression03, int score)
```

`GAProgressionStatus` (`Runtime/Scripts/Enums.cs`, namespace `GameAnalyticsSDK`):

```csharp
public enum GAProgressionStatus
{
    Undefined = 0,
    Start = 1,
    Complete = 2,
    Fail = 3
}
```

Пример из документации:

```csharp
GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "World1", "Level1");
GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, "World1", "Level1", score);
GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, "World1", "Level1");
```

Это специализированный вид именно под «уровень/старт/победа/провал» — веб-кабинет строит по нему готовые KPI (воронки прогрессии, показатели Complete/Start и Fail/Complete). Наши `level_start`, `level_win`, `level_fail` ложатся сюда напрямую.

### 3.4. Design Event — произвольные пользовательские события

```csharp
GameAnalytics.NewDesignEvent(string eventName)
GameAnalytics.NewDesignEvent(string eventName, float eventValue)
```

Применяется для всего, что не покрыто прескриптивными видами (Business/Resource/Progression/Ad): показы экранов, нажатия кнопок, пользовательские шаги воронки. Имя — иерархическое, части разделены двоеточием (подробности — раздел 4). Для наших событий значение (`eventValue`) не требуется, поэтому используется однопараметрическая перегрузка `NewDesignEvent(string eventName)`.

### 3.5. Error Event

```csharp
GameAnalytics.NewErrorEvent(GAErrorSeverity severity, string message)
```

`GAErrorSeverity`: `Undefined, Debug, Info, Warning, Error, Critical`. Для сбора исключений/ошибок, не для бизнес-метрик. Не задействуется ни одним из девяти событий. Внутренний автосабмит ошибок (`GA_Debug.HandleLog`, если включена опция «Submit Errors» в настройках) ограничен `MaxErrorCount = 10` — не более 10 автоматических error-событий за игровую сессию/жизнь приложения (подтверждено в `Runtime/Scripts/Events/GA_Debug.cs`).

### 3.6. Ad Event — реклама

```csharp
GameAnalytics.NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement)
GameAnalytics.NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, long duration)
GameAnalytics.NewAdEvent(GAAdAction adAction, GAAdType adType, string adSdkName, string adPlacement, GAAdError noAdReason)
```

Только для показов/кликов рекламы (rewarded, interstitial, banner). В нашем списке нет рекламных событий — не используется.

### 3.7. Impression Event (ILRD)

`GameAnalyticsILRD.SubscribeXxxImpressions()` — подписка на данные о показах от конкретных рекламных сетей (AdMob, IronSource/LevelPlay, MAX, TopOn, Fyber, Aequus). Не относится к нашим девяти событиям.

### 3.8. Таблица соответствия: наше событие → вид события → точный вызов

| № | Наше событие | Вид события GameAnalytics | Точный вызов на C# |
|---|---|---|---|
| 1 | `app_open` | Design | `GameAnalytics.NewDesignEvent("app:open");` |
| 2 | `photo_screen_shown` | Design | `GameAnalytics.NewDesignEvent("photo:screen_shown");` |
| 3 | `photo_uploaded` | Design | `GameAnalytics.NewDesignEvent("photo:uploaded");` |
| 4 | `photo_rejected` | Design | `GameAnalytics.NewDesignEvent("photo:rejected");` |
| 5 | `level_start` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, levelId);` |
| 6 | `level_win` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, levelId);` |
| 7 | `level_fail` | Progression | `GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, levelId);` |
| 8 | `moves_button_tap` | Design | `GameAnalytics.NewDesignEvent("moves:button_tap");` |
| 9 | `notification_allowed` | Design | `GameAnalytics.NewDesignEvent("notification:allowed");` |

Пояснения к таблице:

- Имена Design-событий выше — предложенные варианты, соответствующие рекомендованной практике «`[категория]:[под-категория]:[исход]`» (см. раздел 4). Точные строки нужно зафиксировать до начала интеграции и больше не менять (смена имени = разрыв ряда в отчётах).
- `levelId` — строка вида `"Level1"` или `"World1:Level1"` (1–3 части иерархии внутри Progression, отдельно от 5-частной иерархии Design). Cardinality-лимит для Progression — 8000 уникальных сочетаний в день на игру (раздел 4) — с обычным количеством уровней головоломки это не проблема, но использовать в `progression01/02/03` сырые процедурные ID (не то же самое, что номер уровня из дизайна) не стоит.
- Для `level_win`/`level_fail`, если в игре считается счёт (score) за уровень, есть отдельная перегрузка с параметром `int score` — не обязательна для трёх целевых мер, но может пригодиться позже.
- Три требуемые меры считаются не отдельными вызовами, а как соотношение количества пользователей, дошедших до одного Design/Progression-события, к количеству дошедших до другого (воронка — см. раздел 8): доля дошедших до экрана съёмки = `photo:screen_shown` относительно `app:open`; доля загрузивших снимок = `photo:uploaded` относительно `photo:screen_shown`; доля нажавших «+5 ходов» = `moves:button_tap` относительно, например, `level_fail` (или другого события-знаменателя, которое определяет команда) — само по себе SDK меру не считает, это делается воронкой в веб-кабинете поверх присланных событий.

## 4. Ограничения на имена и значения

Эти ограничения — частая причина «событие не доходит», потому что нарушение **не выдаёт ошибку компиляции и не всегда явно логируется на клиенте** — коллектор GameAnalytics на сервере валидирует событие и отклоняет всё, что не проходит JSON-схему. Источник — официальная страница Collection API (`docs.gameanalytics.com/.../api/event-types/`), формулировка в документации: «The collector servers will validate these fields and reject any event not passing».

### 4.1. Design Event — точная серверная схема

Официальный JSON Schema (дословно, включая экранирование):

```
{
  "description": "Schema for design event",
  "id": "design",
  "type": "object",
  "extends": "shared",
  "properties": {
    "event_id": {
      "type": "string",
      "pattern": "^[A-Za-z0-9\\s\\-*\\.\\(\\)\\!\\?]{1,64}(:[A-Za-z0-9\\s\\-_\\.\\(\\)\\!\\?]{1,64}){0,4}$",
      "required": true
    },
    "value": {
      "type": "number",
      "required": false
    },
    "category": {
      "type": "string",
      "required": true,
      "pattern": "^design$"
    }
  }
}
```

Из регулярного выражения дословно следует:

- **Число частей**: от 1 до 5, разделены `:` (первая часть обязательна, ещё до 4 опциональны).
- **Длина каждой части**: 1–64 символа.
- **Допустимые знаки в первой части**: `A-Z a-z 0-9`, пробел, `-`, `*`, `.`, `(`, `)`, `!`, `?`.
- **Допустимые знаки во второй-пятой частях**: те же, но вместо `*` — `_` (подчёркивание допустимо только не в первой части, а `*` — только в первой; это асимметрия именно в регулярном выражении коллектора, не описка).
- Символ `,` (запятая), вопреки более свободной формулировке на странице Unity SDK («знаки `a-zA-Z, 0-9, -_.,:()!?»), в реальной серверной схеме **отсутствует** — за основу нужно брать регулярное выражение Collection API, а не описательный текст страницы Unity.

Практический вывод: не использовать в именах событий запятые, кириллицу, слэши, амперсанды и другие небезопасные символы; не начинать часть события с `_`.

### 4.2. Progression Event — схема

```
{
  "id": "progression",
  "properties": {
    "event_id": {
      "pattern": "^(Start|Fail|Complete):[A-Za-z0-9\\s\\-*\\.\\(\\)\\!\\?]{1,64}(:[A-Za-z0-9\\s\\-_\\.\\(\\)\\!\\?]{1,64}){0,2}$",
      "required": true
    },
    "attempt_num": { "type": "integer", "minimum": 0, "required": false },
    "score": { "type": "integer", "required": false }
  }
}
```

Итоговый `event_id` на сервере получается как `Start|Fail|Complete` + от 1 до 3 значений `progression01/02/03` — то есть в терминах Unity SDK допустимо 1–3 уровня иерархии (`progression01`, опционально `progression02`, опционально `progression03`), не 5, как у Design.

### 4.3. Cardinality-лимиты (число уникальных сочетаний в сутки на игру)

Официальная страница «Event Tracking and Cardinality Limits» (обновлена под политику, действующую с 1 октября 2025 года):

| Показатель | Порог |
|---|---|
| Всего событий на активного пользователя в сутки | 500 |
| Уникальных сочетаний (cardinality) Design-событий в сутки | 15 000 |
| Уникальных сочетаний Progression-событий в сутки | 8 000 |
| Уникальных сочетаний Resource-событий в сутки | 4 000 |

Поведение при превышении (с 1 октября 2025 г.): событие не теряется целиком, но его идентификатор в системе аналитики **заменяется на «null»**, из-за чего метрики по нему становятся недостоверными в AnalyticsIQ (дашборды, Explore, Funnels) и в MetricsAPI; в Data Export/Data Warehouse (сырые данные) искажений нет. Практическое следствие для головоломки: не использовать процедурные ID уровней, временные метки или координаты внутри `event_id` — только заранее известный конечный набор имён уровней/экранов.

### 4.4. Прочие числовые лимиты, встречающиеся в конфигурации Unity SDK

- Валюты для Resource Event: максимум 20, строка только из `[A-Za-z]`.
- Типы предметов (item type) для Resource Event: максимум 20, только буквенно-цифровые символы.
- `itemId` в Resource Event: строка, максимум 32 символа.
- `cartType` в Business Event: максимум 10 уникальных значений (указано на странице Business Event), максимум 32 символа по схеме API.
- Кастомные измерения (`SetCustomDimension01/02/03`): максимум 3 на игру, значения нужно заранее объявить в кабинете — иначе значение молча игнорируется («Any value which is not defined in the dashboard will be ignored!»).
- Автосабмит ошибок: не более 10 событий Error за время жизни приложения (см. раздел 3.5).

К нашим девяти событиям (все Design или Progression, без Resource/Business) непосредственно применимы только правила из 4.1–4.3.

## 5. Проверка, что события доходят

### 5.1. Debug-режим в Unity SDK — не через код, а через инспектор

Важное уточнение к формулировке задачи: в большинстве других SDK GameAnalytics (Android native, iOS native, JavaScript, standalone C#) действительно есть публичные рантайм-методы `SetEnabledInfoLog(true)` / `SetEnabledVerboseLog(true)`. Но в исходном коде Unity-обёртки (`Runtime/Scripts/GameAnalytics.cs`, актуальная 8.1.0) такого публичного статического метода **нет** — проверено прямым поиском по файлу (`grep` не находит `SetEnabledInfoLog`/`SetEnabledVerboseLog`). Вместо этого в Unity включение выполняется как настройка объекта Settings:

Официальная страница «Debugging | Unity»:

> The SDK consists of Unity code (C# wrapper) that call code inside some native libraries (iOS / Android). **When playing in the editor the native code is not compiled/used.**

Три режима:

1. **Info Log Editor** — работает уже в Play Mode редактора: показывает, что событие было добавлено из C#-кода, но реальной отправки и серверной валидации в этом режиме нет («The events are not validated yet»).
2. **Info Log Build** (чекбокс в инспекторе Settings) — базовая информация при боевой (native) сборке.
3. **Verbose Log Build** (чекбокс в инспекторе Settings) — выводит полный JSON события, которое реально отправляется на сервер GameAnalytics.

Внутри кода это читается как поля объекта настроек:

```csharp
if (SettingsGA.InfoLogBuild)
{
    GA_Setup.SetInfoLog(true);
}
if (SettingsGA.VerboseLogBuild)
{
    GA_Setup.SetVerboseLog(true);
}
```

Из этого следует: чтобы увидеть подробный лог на iOS, нужно **включить чекбоксы в инспекторе Settings до сборки**, собрать проект под iOS-устройство (реальное или симулятор) и смотреть вывод консоли Xcode — в редакторе полноценной проверки нет, только проверка факта вызова из C#.

Дополнительное важное предупреждение из документации: если отправить событие раньше готовности SDK, будет:

```
Warning/GameAnalytics: Could not add design event: Datastore not initialized
```

### 5.2. Просмотр в реальном времени (Realtime)

Официальная страница «Realtime» (`docs.gameanalytics.com/products-and-features/analytics-iq/realtime/overview/`):

- Вкладка **Live Events** показывает последние **50** событий, обновление «каждые несколько секунд», с фильтрами по типу события, билду, User ID (поддерживаются шаблоны с `*`).
- Прямая цитата: **«Events typically appear within 30 seconds of being sent by the client»** — то есть от отправки до появления в Live Events обычно проходит порядка 30 секунд.
- Есть режим просмотра «Raw JSON» — удобно свериться с точным содержимым события (имя, категория, value).
- Realtime предназначен для отладки/валидации интеграции, а не для долгосрочной аналитики — под это отдельно есть Dashboards/Explore/Data Export.

### 5.3. Задержка обычных отчётов и как отличить «не отправилось» от «ещё не обработалось»

Официальной точной цифры на сайте GameAnalytics для задержки именно стандартных дашбордов (Dashboards/Explore) в собранной документации не найдено — «не проверено» через первоисточник docs.gameanalytics.com. Косвенные данные из форумов сообщества (не первоисточник GameAnalytics, но многократно повторяются): порядка «до 24 часов» на обработку до полной агрегации в стандартных отчётах (обсуждение на forums.solar2d.com применительно к GameAnalytics: «it takes under 24 hours for data to get aggregated by our servers if there are no processing delays» — 2015 год, могло измениться).

Практический способ отличить «событие не отправилось» от «ещё не обработалось»:

1. Включить **Verbose Log Build** и посмотреть в консоли Xcode, что JSON события реально сформирован и уходит (значит, клиент не молчит).
2. Проверить **Live Events** в Realtime — если событие появилось там в течение ~30 секунд, значит коллектор его принял и провалидировал; если событие не появляется в Realtime вообще, вероятная причина — нарушение схемы валидации (раздел 4) или сетевая проблема, а не задержка обработки.
3. Если событие есть в Realtime, но не появилось в обычном дашборде — это, вероятно, штатная задержка агрегации, а не потеря события; ждать до суток и повторно смотреть Explore/Dashboards.
4. Использовать вкладку **SDK Status** в Realtime — она показывает, какие версии SDK активны и сколько событий каждая отправляет; полезно, если стоит гипотеза «на части устройств SDK вообще не достучался».

## 6. Поведение без сети

Подтверждено документацией (страница «SDK Features», применимо к семейству SDK GameAnalytics, включая используемое в Unity-обёртке поведение нативных iOS/Android библиотек):

> Offline: When a device is offline the events are still added to the queue. When the device is online it will submit.

То есть при отсутствии сети события не отбрасываются, а копятся в локальной очереди устройства и досылаются автоматически, когда соединение восстанавливается — писать собственный механизм отложенной отправки не требуется.

Дополнительно из страницы «Configuration | Unity» про механику сессий и очереди:

> When the session is active you will be able to track events (e.g. after the SDK has been initialized) and the event queue will be running on a low priority thread, batching events and sending them to the server every **8 seconds**.

Из этого следует: даже при наличии сети события не летят мгновенно поштучно — они батчатся и уходят раз в 8 секунд с низкоприоритетного потока. Это важно для интерпретации задержек при живой отладке (см. раздел 5): не увидев событие в Realtime сразу, стоит подождать хотя бы один цикл батчинга (8 секунд) плюс сетевую задержку, прежде чем считать событие потерянным.

Прямого указания на лимит объёма локальной очереди на iOS (сколько событий или сколько дней хранится офлайн до переполнения) в собранной документации не найдено — **не проверено**.

## 7. Требования Apple

### 7.1. Privacy manifest — есть, найден непосредственно в пакете

Прямой поиск по дереву репозитория GitHub (`GET /repos/GameAnalytics/GA-SDK-UNITY/git/trees/master?recursive=1`) подтверждает физическое наличие файла манифеста внутри нативного фреймворка, поставляемого пакетом:

```
Runtime/Apple/GameAnalytics.xcframework/ios-arm64/GameAnalytics.framework/PrivacyInfo.xcprivacy
Runtime/Apple/GameAnalytics.xcframework/ios-arm64_x86_64-simulator/GameAnalytics.framework/PrivacyInfo.xcprivacy
Runtime/Apple/GameAnalytics.xcframework/tvos-arm64/GameAnalyticsTVOS.framework/PrivacyInfo.xcprivacy
Runtime/Apple/GameAnalytics.xcframework/tvos-arm64_x86_64-simulator/GameAnalyticsTVOS.framework/PrivacyInfo.xcprivacy
```

Файл скачан и раскодирован (бинарный plist → XML через `plutil -convert xml1`), содержимое приведено дословно ниже — это фактический, а не декларируемый в маркетинговых материалах манифест текущей версии (8.1.0):

```xml
<key>NSPrivacyAccessedAPITypes</key>
<array>
  <dict>
    <key>NSPrivacyAccessedAPIType</key><string>NSPrivacyAccessedAPICategoryFileTimestamp</string>
    <key>NSPrivacyAccessedAPITypeReasons</key><array><string>C617.1</string></array>
  </dict>
  <dict>
    <key>NSPrivacyAccessedAPIType</key><string>NSPrivacyAccessedAPICategoryUserDefaults</string>
    <key>NSPrivacyAccessedAPITypeReasons</key><array><string>CA92.1</string></array>
  </dict>
</array>

<key>NSPrivacyCollectedDataTypes</key>
<array>
  <!-- Performance Data, Gameplay Content, Other Diagnostic Data, Crash Data,
       Product Interaction, Advertising Data, User ID, Device ID —
       для каждого из этих 8 типов: NSPrivacyCollectedDataTypeLinked = true,
       NSPrivacyCollectedDataTypeTracking = false,
       Purposes = [AppFunctionality, Analytics] -->
</array>

<key>NSPrivacyTracking</key><true/>
<key>NSPrivacyTrackingDomains</key>
<array><string>tracking.gameanalytics.com</string></array>
```

Дословный перечень собираемых типов данных (`NSPrivacyCollectedDataType…`), каждый — с `Linked = true` (данные привязаны к личности пользователя) и целями `App Functionality` + `Analytics`:

- `PerformanceData` (данные о производительности)
- `GameplayContent` (игровой контент — вероятно, прогресс/действия в игре)
- `OtherDiagnosticData` (прочие диагностические данные)
- `CrashData` (данные о сбоях)
- `ProductInteraction` (взаимодействие с продуктом)
- `AdvertisingData` (рекламные данные)
- `UserID` (идентификатор пользователя)
- `DeviceID` (идентификатор устройства)

Важный нюанс манифеста, о котором нужно знать при заполнении App Privacy: на верхнем уровне манифеста стоит `NSPrivacyTracking = true` и указан один трекинг-домен `tracking.gameanalytics.com` (это технически означает, что фреймворк объявляет себя использующим домен, попадающий под категорию «трекинг» в терминологии Apple), но при этом **у каждой отдельной записи** в `NSPrivacyCollectedDataTypes` стоит `NSPrivacyCollectedDataTypeTracking = false` (то есть ни один из перечисленных типов данных сам по себе не помечен как используемый для межплатформенного трекинга пользователя). Оба факта взяты дословно из самого файла — задача разработчика (не решается автоматически SDK) — свести это с ATT: если приложение не показывает ATT и не использует IDFA (см. 7.2), это не отменяет наличие `NSPrivacyTracking=true` и трекинг-домена в манифесте самого фреймворка, но означает, что фактическая передача данных для целей трекинга не активирована с вашей стороны, пока вы явно не включите такое использование.

### 7.2. Что задекларировать в App Privacy («Nutrition Labels» в App Store Connect)

Официальной страницы от GameAnalytics с готовой инструкцией «что поставить в App Store Connect» в собранной документации не найдено — **не проверено** первоисточником GameAnalytics. Ниже — вывод, сделанный из фактического содержимого `PrivacyInfo.xcprivacy` (раздел 7.1), а не выдумка: категории данных, которые нужно будет отразить в App Privacy вашего приложения как минимум из-за использования этого SDK:

- Идентификаторы (User ID, Device ID) — с целью «Analytics» и «App Functionality».
- Диагностика (Crash Data, Performance Data, Other Diagnostic Data) — с теми же целями.
- Данные о взаимодействии с продуктом (Product Interaction) и игровой контент (Gameplay Content) — то есть сами события, которые вы отправляете (`level_start`, `photo_uploaded` и т.д.), формально подпадают под эти категории.
- Рекламные данные (Advertising Data) — присутствует в манифесте независимо от того, включена ли у вас реклама; при выключенном сборе рекламного идентификатора (раздел 7.3) фактический сбор этой категории можно свести к отсутствию IDFA, но сама декларация в манифесте SDK эту категорию упоминает.

Точную формулировку итоговой декларации приложения нужно сверять с реальным поведением всего приложения целиком (не только GameAnalytics), поэтому финальное заполнение формы App Privacy — ответственность разработчика/юриста, а не то, что можно единолично вывести из одного манифеста.

### 7.3. IDFA и ATT — можно ли обойтись без диалога

Прямой ответ: **да, можно избежать показа диалога ATT**, если следовать описанному ниже, и это подтверждается официальной документацией и исходным кодом.

Ключевые факты:

1. GameAnalytics **не показывает диалог ATT сам по себе автоматически** — показ инициируется только явным вызовом `GameAnalytics.RequestTrackingAuthorization(this)`, который вызывает разработчик по желанию (см. пример кода в разделе 2.5). Если этот метод не вызывать — и не вызывать нативный `ATTrackingManager.requestTrackingAuthorization` где-либо ещё в проекте — диалог не появится вообще.
2. Если ATT не был запрошен, статус авторизации трекинга остаётся `notDetermined`, а не `authorized`. Документация прямо говорит: «The GameAnalytics Unity SDK uses IDFV (for iOS) as the user id and it will only add IDFA to events if ATT consent status is authorized» — то есть без запроса ATT никакой IDFA к событиям не добавляется, а идентификатором пользователя служит IDFV (Identifier for Vendor), для которого разрешение ATT не требуется.
3. Дополнительно есть явная программная настройка для отключения использования рекламного идентификатора вообще, найденная в `Configuration | Unity`:

```csharp
GameAnalytics.EnableAdvertisingIdTracking(false);
```

Дословно из документации: «This function will also force the default generated user id to be fully random on all platforms» — то есть вызов не только запрещает использование IDFA, но и переключает идентификатор пользователя на случайный (вместо IDFV) на всех платформах. Подтверждено также прямо в исходном коде — публичный метод `EnableAdvertisingIdTracking(bool flag)` присутствует в `GameAnalytics.cs` (строка `1141`, актуальная 8.1.0).
4. SDK по-прежнему нужно инициализировать в любом случае, даже если ATT не запрашивался и рекламный идентификатор отключён — это никак не блокирует остальную функциональность SDK (Design/Progression-события работают одинаково).

Итоговая рекомендация для этой игры (раз стоит цель избежать диалога ATT): **не вызывать** `GameAnalytics.RequestTrackingAuthorization()`, инициализировать SDK напрямую (`GameAnalytics.Initialize()`), и дополнительно вызвать `GameAnalytics.EnableAdvertisingIdTracking(false)` до инициализации — это даёт явный, декларативный сигнал «не собираем рекламный идентификатор», а не просто отсутствие запроса.

Известный практический риск при работе с ATT-диалогом (даже если решите когда-нибудь его включить) — issue **#23** в репозитории (`iOS Crashes After "Allow" or "Ask App Not to Track" IDFA Consent Dialog`, открыт): краш iOS-приложения сразу после выбора пользователем варианта в диалоге ATT, если в `Info.plist` не добавлен ключ `NSUserTrackingUsageDescription` — приложение переживает краш один раз, дальше (поскольку выбор уже сохранён) диалог больше не показывается и краша не происходит. Так как в нашем случае диалог показываться не будет вовсе, эта проблема неактуальна — но фиксируется как довод в пользу выбранного решения «не запрашивать ATT».

## 8. Веб-кабинет

### 8.1. Построение воронки по нашим событиям

Официальная страница «Funnels» (`docs.gameanalytics.com/products-and-features/analytics-iq/funnels/`):

Шаги создания:

1. **Funnels → Create**.
2. Выбрать тип: **Standard Funnel** (поддерживает Design, Resource и Progression события вместе — то, что нужно нам, так как наши девять событий смешивают Design и Progression) либо **Progression Funnel** (только Progression, зато даёт дополнительно метрики Complete/Start и Fail/Complete).
3. Кнопка **Steps** — добавить события шагами воронки (например: `app:open` → `photo:screen_shown` → `photo:uploaded`, отдельная воронка `level_fail`/иное релевантное событие → `moves:button_tap`).
4. Порядок шагов можно менять, дублировать, удалять.
5. **Process** — построить первую версию воронки.
6. Опциональные фильтры для сегментации результата.
7. **Save**.

Важная особенность модели воронки в AnalyticsIQ: по умолчанию это воронка **«в любом порядке» (Any Order)** — пользователь засчитывается прошедшим шаг, если выполнил его и все предыдущие шаги, но не обязательно в хронологическом порядке. Строгий порядок (**Strict Order**) доступен только в SegmentIQ, отдельном продукте. Для честного расчёта «дошёл ли до экрана съёмки после открытия игры» это стоит учитывать — Any Order может немного завышать конверсию по сравнению с интуитивным «строго друг за другом».

Показатели, которые доступны в результатах воронки (обе разновидности): Total conversion, Total churn, Total users, Biggest drop, Step completion, Churn, Total completion; только для Progression Funnel — Complete/Start ratio и Fail/Complete ratio.

### 8.2. Удержание (retention)

Отдельная страница **Retention** (`docs.gameanalytics.com/products-and-features/analytics-iq/engagement-tools/retention`) и раздел **Dashboards** (`docs.gameanalytics.com/products-and-features/analytics-iq/dashboards/overview/`), где есть готовый блок «Retention (D1, 7, 30, etc.)». Отдельно на странице метрик (`events-metrics-and-filtering/metrics`) дано определение: «Retention reports the daily percent of users who installed on a specific day and then returned N days later», по умолчанию для D1–D7 и D14.

### 8.3. Выгрузка в CSV

Подтверждено на странице Funnels: результаты воронки можно выгрузить — «Download the data in a CSV format to analyze in other products», плюс переключение отображения между целыми числами и процентами. Помимо воронок, для более полной выгрузки сырых событий существует отдельный продукт **Data Export** (PipelineIQ) — предназначен для полного экспорта событий/полей/измерений, а не только результатов одной воронки.

## 9. Подводные камни (по практике сообщества и issue-трекеру)

Все пункты ниже взяты из реальных issues репозитория `GameAnalytics/GA-SDK-UNITY` на GitHub (получены через GitHub API, отсортированы по последнему обновлению) — не выдумка, ссылки приведены в разделе «Источники».

- **№57 (открыт) — компиляция ломается на новых тех-стримах Unity 6000.x.** При апгрейде до Unity 6.5 (и вероятно, будущих версий 6000.x) сборка падает с `CS0619`, потому что `GameAnalytics.cs` использует устаревшие `EditorApplication.hierarchyWindowItemOnGUI` и `EditorUtility.InstanceIDToObject`, которые Unity полностью удаляет в новых версиях. На момент составления документа автор сообщил об этом для 6.5; воспроизводимость именно на 6.3 (наша целевая версия) не проверена — стоит протестировать компиляцию сразу после установки, до написания игровой логики.
- **№58 (открыт) — падение именно на Unity 6.3 в Standalone-сборке.** Крах `gameanalytics.dll` (нативный кроссплатформенный компонент, попытка залочить неинициализированный/уничтоженный мьютекс). Платформа — Windows, не iOS, но показывает, что 8.1.0 под 6000.3.x имеет незакрытые проблемы стабильности нативного слоя в принципе.
- **№23 (открыт) — краш iOS сразу после выбора в диалоге ATT**, если не добавлен `NSUserTrackingUsageDescription` в `Info.plist`. Неактуально при выбранной стратегии «не показывать ATT» (раздел 7.3), но критично, если решение изменится.
- **№54 (открыт) — при включённом ручном управлении сессиями (`Manual Session Handling`) `EndSession`-события всё равно отправляются автоматически.** Автор сообщает, что несмотря на ручные вызовы `StartSession()`/`EndSession()`, SDK продолжает сам закрывать сессии — то есть ручной режим сессий не полностью изолирует от автоматики. Мы используем автоматическое управление сессиями (документация рекомендует не переключаться на ручное без крайней необходимости) — риск не касается нашей интеграции, но стоит держать в уме, если возникнет соблазн включить ручной режим.
- **№50 (открыт) — `null` в кастомном измерении (custom dimension) роняет нативный код на Android**, а не просто игнорируется, как ожидал бы разработчик (`SDK.SetCustomDimension01(condition ? "value" : null)` — плохой код). Хотя баг зафиксирован на Android, сама логика (что `null` должен «очищать» значение, а не ронять SDK) может быть неочевидна и на iOS — стоит избегать передачи `null` в `SetCustomDimension0N`, если это не задокументированное явно поведение для конкретной платформы.
- **№46 (открыт) — запрос сделать External Dependency Manager (EDM4U) необязательным.** Частично закрыт изменениями 8.1.0 (Android стал самодостаточным), но страница установки для UPM всё ещё требует установки EDM4U как первого шага — см. противоречие в разделе 1.2, стоит проверить на практике, действительно ли для чистой iOS-интеграции (без Android, без AdMob) EDM4U обязателен.
- **№41 (открыт) — ошибки в примерах кода в китайской/локализованной версии документации** (репортер отмечает несостыковки в примерах). Общий урок: не копировать код бездумно из старых блог-постов/форумов — сверяться с `GameAnalytics.cs` в репозитории (как сделано в этом документе) при малейшем сомнении в сигнатуре.
- **Общая практика сообщества (Stack Overflow, форумы Unity/Solar2D/Roblox)** — многочисленные темы «события не появляются в дашборде» почти всегда сводятся к одной из трёх причин: (а) тестирование в редакторе вместо боевой сборки (раздел 5.1: в редакторе события реально не уходят); (б) нарушение схемы валидации имени события (раздел 4) — событие тихо отклоняется сервером; (в) SDK не был инициализирован до отправки события (раздел 2.3) — в логе тогда есть явное предупреждение `Datastore not initialized`, которое легко пропустить, если Info Log не включён.

## Источники

- GameAnalytics Unity SDK — Get Started: https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity
- GameAnalytics Unity SDK — Configuration: https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/configuration
- GameAnalytics Unity SDK — Event Tracking: https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/event-tracking
- GameAnalytics Unity SDK — Debugging: https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/debug/
- Design Events (описание вида события): https://docs.gameanalytics.com/events-metrics-and-filtering/event-types/design-events
- Event Tracking and Cardinality Limits: https://docs.gameanalytics.com/event-tracking-and-integrations/data-retention-and-limits/event-tracking-and-cardinality-limits
- Collection API — Event Types (точные JSON-схемы валидации): https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/api/event-types/
- Realtime — Overview: https://docs.gameanalytics.com/products-and-features/analytics-iq/realtime/overview/
- Funnels: https://docs.gameanalytics.com/products-and-features/analytics-iq/funnels/
- Retention: https://docs.gameanalytics.com/products-and-features/analytics-iq/engagement-tools/retention
- Metrics (определение Retention D1–D14): https://docs.gameanalytics.com/events-metrics-and-filtering/metrics
- Dashboards — Overview: https://docs.gameanalytics.com/products-and-features/analytics-iq/dashboards/overview/
- Репозиторий GA-SDK-UNITY (GitHub): https://github.com/GameAnalytics/GA-SDK-UNITY
  - `CHANGELOG.md`: https://raw.githubusercontent.com/GameAnalytics/GA-SDK-UNITY/master/CHANGELOG.md
  - `README.md`: https://raw.githubusercontent.com/GameAnalytics/GA-SDK-UNITY/master/README.md
  - Исходный код публичного API: `Runtime/Scripts/GameAnalytics.cs`
  - Перечисления: `Runtime/Scripts/Enums.cs`
  - Объект настроек: `Runtime/Scripts/Setup/Settings.cs`
  - Обработка ошибок/автосабмит: `Runtime/Scripts/Events/GA_Debug.cs`
  - Design-события (клиентская сторона): `Runtime/Scripts/Events/GA_Design.cs`
  - Privacy manifest (фактическое содержимое файла): `Runtime/Apple/GameAnalytics.xcframework/ios-arm64/GameAnalytics.framework/PrivacyInfo.xcprivacy`
  - Последний релиз (версия/дата): https://api.github.com/repos/GameAnalytics/GA-SDK-UNITY/releases/latest (тег `8.1.0`, `2026-08-21T09:06:31Z`)
  - Issues, использованные в разделе 9: №23, №41, №46, №50, №54, №57, №58 — https://github.com/GameAnalytics/GA-SDK-UNITY/issues
- OpenUPM — страница пакета: https://openupm.com/packages/com.gameanalytics.sdk/

