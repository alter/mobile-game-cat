# Локальные уведомления и разрешения на iOS (Unity)

Дата сбора: 2026-08-24. Стек: Unity 6.3 LTS, пакет `com.unity.mobile.notifications`, iOS `UNUserNotificationCenter`, `AppTrackingTransparency`.

## Кратко

- Локальные уведомления на iOS планируются через `UNUserNotificationCenter.current().add(_:)` с `UNNotificationRequest`, который может содержать триггер, например `UNCalendarNotificationTrigger`. [Apple — UNUserNotificationCenter.add(_:withCompletionHandler:)](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter/add(_:withcompletionhandler:))
- Жёсткий лимит — не более 64 одновременно запланированных (pending) локальных уведомлений на приложение; это подтверждено инженером Apple на официальном форуме разработчиков, а не в публичной документации напрямую. [Apple Developer Forums — Does UNNotificationRequest have a 64-notification scheduling limit?](https://developer.apple.com/forums/thread/811171)
- В Unity 6.x для локальных уведомлений на iOS используется пакет `com.unity.mobile.notifications`; актуальная (на момент сбора данных) ветка — 2.4.x, версия 2.4.3 идёт в комплекте с редактором 6000.5. [Unity — Mobile Notifications changelog 2.4](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/changelog/CHANGELOG.html)
- Запрос разрешения на уведомления в Unity выполняется через `AuthorizationRequest` (корутина), а планирование — через `iOSNotificationCenter.ScheduleNotification(iOSNotification)` с `iOSNotificationCalendarTrigger` для «одно уведомление в заданное время суток». [Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)
- Provisional authorization (`UNAuthorizationOptions.provisional`) позволяет отправлять уведомления без диалога запроса — они тихо попадают в Центр уведомлений, и пользователь решает, оставить ли их, уже по факту увиденного контента. [Apple — UNAuthorizationOptions.provisional](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions/provisional)
- Надёжных, поддающихся проверке количественных данных о влиянии момента запроса разрешения на процент согласий (конкретно для push/локальных уведомлений на iOS) в рамках этого исследования найти не удалось — маркетинговые источники приводят цифры, но при проверке первоисточника цифры не подтверждаются. Подробности — в разделе 4.
- Для ATT (App Tracking Transparency) в Unity есть официальный пакет `com.unity.ads.ios-support`, предоставляющий класс `ATTrackingStatusBinding` с методами `RequestAuthorizationTracking()` и `GetAuthorizationTrackingStatus()`. [GitHub — Unity-Technologies/com.unity.ads.ios-support](https://github.com/Unity-Technologies/com.unity.ads.ios-support)
- `NSUserTrackingUsageDescription` в Info.plist обязателен для работы `ATTrackingManager.requestTrackingAuthorization(completionHandler:)` — без этого ключа запрос авторизации не работает как положено. [Apple — ATTrackingManager.requestTrackingAuthorization(completionHandler:)](https://developer.apple.com/documentation/apptrackingtransparency/attrackingmanager/requesttrackingauthorization(completionhandler:))

## 1. `UNUserNotificationCenter`: разрешение, планирование, лимит

### 1.1. Запрос разрешения и планирование (нативный Swift API)

Метод для планирования локального уведомления:

```swift
func add(_ request: UNNotificationRequest, withCompletionHandler completionHandler: (@Sendable ((any Error)?) -> Void)? = nil)

// вариант с async/await
func add(_ request: UNNotificationRequest) async throws
```

Официальное описание: «Schedules the delivery of a local notification… This method schedules local notifications only; you cannot use it to schedule the delivery of remote notifications… If the request does not contain a `UNNotificationTrigger` object, the notification is delivered right away.» Метод можно вызывать из любого потока приложения. [Apple — UNUserNotificationCenter.add(_:withCompletionHandler:)](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter/add(_:withcompletionhandler:))

Пример из документации Apple:

```swift
let center = UNUserNotificationCenter.current()
let content = UNMutableNotificationContent()
content.title = "My notification title"
content.body = "My notification body"
let notification = UNNotificationRequest(identifier: "com.example.mynotification", content: content, trigger: nil)
do {
    try await center.add(notification)
} catch {
    // Handle any errors.
}
```

[Apple — UNUserNotificationCenter.add(_:withCompletionHandler:)](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter/add(_:withcompletionhandler:))

### 1.2. Планирование одного уведомления в сутки на вечер: `UNCalendarNotificationTrigger`

Для «одно уведомление в сутки в заданное время» нужен именно календарный триггер, который задаётся через `DateComponents` — если указать только `hour`/`minute` (без `day`/`month`/`year`), система сама находит следующее подходящее время и, при `repeats: true`, повторяет уведомление каждый день в это время. Конкретные примеры кода для `UNCalendarNotificationTrigger` именно из документации Apple в рамках этого исследования не открывались — приведённый выше пример показывает только базовый `add(_:)` без триггера; для планового ежедневного вечернего уведомления в нативном Swift-коде используется класс `UNCalendarNotificationTrigger(dateMatching:repeats:)`, где `dateMatching` — `DateComponents` с заданным `hour`/`minute`. Эта конструкция не подтверждена отдельной цитатой Apple в рамках данного исследования — «не проверено» в части точной сигнатуры инициализатора.

### 1.3. Лимит в 64 запланированных уведомления

Апple не описывает этот лимит явно в публичной документации класса `UNUserNotificationCenter`, но инженер Apple подтвердил его напрямую на официальном форуме разработчиков: «Yes, there is a limit of 64 for how many simultaneous notification requests can be active/pending at one time per app. This is a system limit and there is no way around it.» [Apple Developer Forums — Does UNNotificationRequest have a 64-notification scheduling limit?](https://developer.apple.com/forums/thread/811171)

Практические следствия, которые сообщество выводит из этого лимита:
- Система удерживает 64 ближайших по времени срабатывания уведомления и отбрасывает остальные (при попытке запланировать больше).
- Рекомендуемый паттерн — держать в очереди только ближайшие ~64 срабатывания и пересчитывать/перепланировать их при каждом запуске приложения, вызывая `removeAllPendingNotificationRequests()` перед повторным планированием.
- Официального механизма увеличения лимита или исключения для конкретных приложений не существует.

[Apple Developer Forums — Does UNNotificationRequest have a 64-notification scheduling limit?](https://developer.apple.com/forums/thread/811171)

## 2. Unity Mobile Notifications (`com.unity.mobile.notifications`)

### 2.1. Версия для Unity 6.x

Пакет добавляет поддержку планирования локальных одноразовых или повторяющихся уведомлений на Android и iOS, с поддержкой push-уведомлений на iOS. На момент сбора данных (2026-08-24) актуальная ветка — 2.4.x, конкретно версия 2.4.3, поставляемая с редактором Unity 6000.5. Из изменений этой ветки, значимых для iOS: добавлен новый API `QueryLastRespondedNotification` — для получения деталей уведомления, по которому было выполнено касание при запуске приложения. [Unity — Mobile Notifications changelog 2.4](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/changelog/CHANGELOG.html)

Минимальная поддерживаемая версия Unity для пакета в целом — «Compatible with Unity 2021.3 or above»; пакет также поддерживает Push-уведомления через APNs, группировку уведомлений в треды (iOS 12+), вложения и кастомные действия. [Unity — Mobile Notifications manual (overview)](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html)

### 2.2. Запрос разрешения

Официальный пример Unity (корутина `RequestAuthorization`):

```csharp
IEnumerator RequestAuthorization()
{
    var authorizationOption = AuthorizationOption.Alert | AuthorizationOption.Badge;
    using (var req = new AuthorizationRequest(authorizationOption, true))
    {
        while (!req.IsFinished)
        {
            yield return null;
        };

        string res = "\n RequestAuthorization:";
        res += "\n finished: " + req.IsFinished;
        res += "\n granted :  " + req.Granted;
        res += "\n error:  " + req.Error;
        res += "\n deviceToken:  " + req.DeviceToken;
        Debug.Log(res);
    }
}
```

[Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

### 2.3. Планирование через `iOSNotificationCalendarTrigger`

`iOSNotificationCalendarTrigger` — структура в пространстве имён `Unity.Notifications.iOS`, реализующая `iOSNotificationTrigger`; используется, «когда нужно запланировать доставку локального уведомления в указанные дату и время». Не обязательно задавать все поля — если оставить `Year`/`Month`/`Day` незаполненными, система сама подбирает ближайшее подходящее время по оставшимся полям (`Hour`/`Minute`). [Unity — iOSNotificationCalendarTrigger API (2.1)](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.1/api/Unity.Notifications.iOS.iOSNotificationCalendarTrigger.html)

Официальный пример из руководства (уведомление на 12:00 дня, без повтора в примере):

```csharp
var calendarTrigger = new iOSNotificationCalendarTrigger()
{
    // Year = 2020,
    // Month = 6,
    // Day = 1,
    Hour = 12,
    Minute = 0,
    // Second = 0
    Repeats = false
};
```

[Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

Для нашей задачи (одно уведомление в сутки на вечер, например 20:00, с повтором каждый день) конструкция будет такой (составлена нами по задокументированным полям структуры, аналогично примеру выше):

```csharp
var eveningTrigger = new iOSNotificationCalendarTrigger()
{
    Hour = 20,
    Minute = 0,
    Repeats = true
};

var notification = new iOSNotification()
{
    Identifier = "daily_evening_reminder",
    Title = "Ваш кот заждался!",
    Body = "Зайдите в игру и сделайте новое фото.",
    ShowInForeground = true,
    ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
    CategoryIdentifier = "daily_reminder",
    ThreadIdentifier = "daily_reminder_thread",
    Trigger = eveningTrigger,
};

iOSNotificationCenter.ScheduleNotification(notification);
```

Метод планирования и поля `iOSNotification` (`Identifier`, `Title`, `Body`, `Subtitle`, `ShowInForeground`, `ForegroundPresentationOption`, `CategoryIdentifier`, `ThreadIdentifier`, `Trigger`) — из официального руководства Unity; конкретно сборка «на 20:00, каждый день» с этими значениями — наша компоновка по задокументированному API, а не дословная цитата единого примера. [Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

Метод отмены незапустившегося уведомления:

```csharp
iOSNotificationCenter.RemoveScheduledNotification(notification.Identifier);
```

[Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

### 2.4. Отложенный запрос разрешения (не при первом запуске)

В самом пакете `com.unity.mobile.notifications` нет отдельного встроенного «мастера отложенного запроса» — управление моментом запроса реализуется вручную в игровом коде: разработчик сам решает, когда вызвать `AuthorizationRequest` (например, после первого успешного фото кота, а не сразу на titlescreen). Документация пакета отмечает лишь техническую деталь: «If the user has already granted or denied authorization, the permissions request dialog doesn't display again» — то есть повторный вызов `AuthorizationRequest` безопасен и не покажет системный диалог дважды. [Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)

## 3. Provisional authorization

`UNAuthorizationOptions` — перечисление опций, определяющих разрешённые возможности локальных и удалённых уведомлений; один из вариантов — `.provisional`. Официальное описание: `.provisional` предоставляет «the ability to post noninterrupting notifications provisionally to the Notification Center», а соответствующий статус `UNAuthorizationStatus.provisional` означает, что приложению временно разрешено отправлять неперебивающие уведомления пользователю. [Apple — UNAuthorizationOptions](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions), [Apple — UNAuthorizationOptions.provisional](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions/provisional)

Как это работает на практике (по независимым разборам темы, не из первичной документации Apple): при запросе с опцией `.provisional` система **не показывает диалог** запроса разрешения — уведомления сразу тихо доставляются в Центр уведомлений, где у пользователя есть возможность либо оставить их, либо полностью отключить. Это способ дать пользователю «пробный период» с уведомлениями конкретного приложения без явного разрешительного диалога. Функция доступна с iOS 12. [Use Your Loaf — Provisional Authorization of User Notificatons](https://useyourloaf.com/blog/provisional-authorization-of-user-notificatons/)

**Стоит ли брать provisional authorization для нашей игры:** это компромисс. Плюс — не тратится «лимитированная попытка» показа системного диалога и не пугает пользователя лишним запросом; минус — уведомление приходит без звука и баннера (только в Центр уведомлений), то есть менее заметно, а значит хуже подходит, если цель — именно вернуть игрока в приложение звуковым/визуальным напоминанием. Прямых сравнительных данных (какой вариант эффективнее по возврату пользователей) не найдено — «надёжных источников не найдено».

## 4. Влияние момента запроса разрешения на долю согласий

По задаче отдельно указано: приводить числа только если найден источник с цифрами, иначе — «данных нет». В рамках этого исследования цепочка проверки такова:

- Блог vmobify.com утверждает: «apps that show a soft-ask modal at the moment of first value (rather than on first launch) achieve 55–70% opt-in versus 30–40% for apps that trigger the prompt immediately», ссылаясь на «Pushwoosh's opt-in rate research» с цифрой «30–50% higher acceptance rates». [vmobify — Push Notification Strategy 2026](https://vmobify.com/blog/push-notification-strategy)
- При открытии первоисточника (блог Pushwoosh, на который ссылается vmobify) эти конкретные цифры **не подтвердились** — статья Pushwoosh содержит только общую рекомендацию по выбору «момента высокого намерения» для показа запроса, без цифр по конверсии, без описания контролируемого эксперимента или опроса. [Pushwoosh — How to Increase Your Push Notification Opt-In Rate](https://www.pushwoosh.com/blog/increase-push-notifications-opt-in/)
- Другой источник (semnexus.com) содержит похожее качественное утверждение («apps that trigger the native prompt within the first 30 seconds of first launch typically see lower opt-in rates than apps that delay the ask»), но также без числовых данных и без указания проверяемого источника. [SEM Nexus — Push Notification Timing: What the Data Says About Opt-In Rates](https://semnexus.com/push-notification-timing-data-opt-in-rates)

**Вывод:** конкретные проценты («55–70% против 30–40%», «на 25–50% выше») по этой теме встречаются только в маркетинговых блогах, а при проверке по цепочке цитирования до первоисточника числа не подтверждаются документально проверяемым исследованием. Формально источник с цифрами найден (vmobify.com), но он недостоверен — ссылается на источник, который эти цифры не содержит. Поэтому для практических решений в проекте эти цифры использовать не стоит; корректная формулировка — «данных нет» (в смысле «нет источника, которому можно доверять»). Общая, качественная рекомендация (не привязывать запрос разрешения к первому запуску, показывать его после демонстрации ценности функции) в источниках повторяется многократно, но без поддающейся проверке количественной оценки эффекта.

## 5. ATT и `NSUserTrackingUsageDescription` в связке с Unity

### 5.1. Нативный API Apple

```swift
class func requestTrackingAuthorization(completionHandler completion: @escaping @Sendable (ATTrackingManager.AuthorizationStatus) -> Void)

// вариант с async/await
class func requestTrackingAuthorization() async -> ATTrackingManager.AuthorizationStatus
```

Ключевые правила использования, согласно документации Apple:
- Запрос одноразовый на установку приложения — система запоминает выбор пользователя и не спрашивает повторно, если приложение не было удалено и переустановлено.
- Перед повторным вызовом стоит проверять `trackingAuthorizationStatus` на `.notDetermined`.
- Диалог показывается только когда состояние приложения — `UIApplicationStateActive`.
- Диалог не появится, если уже есть другой ожидающий запрос разрешения (конкурентные запросы не сохраняются системой).
- Вызов из расширения приложения (app extension) не показывает диалог.
- **`NSUserTrackingUsageDescription` в Info.plist обязателен** — без этого ключа запрос авторизации не будет работать корректно.

[Apple — ATTrackingManager.requestTrackingAuthorization(completionHandler:)](https://developer.apple.com/documentation/apptrackingtransparency/attrackingmanager/requesttrackingauthorization(completionhandler:))

### 5.2. Пакет Unity `com.unity.ads.ios-support`

Официальное описание: пакет «provides support for App Tracking Transparency and SkAdNetwork API newly introduced in Apple iOS 14», включая пример настраиваемого экрана-«прогрева» перед запросом разрешения на трекинг. [GitHub — Unity-Technologies/com.unity.ads.ios-support](https://github.com/Unity-Technologies/com.unity.ads.ios-support)

Методы, доступные через `ATTrackingStatusBinding` (пространство имён `Unity.Advertisement.IosSupport`):

```csharp
public static void RequestAuthorizationTracking()
public static AuthorizationTrackingStatus GetAuthorizationTrackingStatus()
public static void SkAdNetworkUpdateConversionValue(int conversionValue)
```

[GitHub — Unity-Technologies/com.unity.ads.ios-support](https://github.com/Unity-Technologies/com.unity.ads.ios-support)

Официальный пример использования из документации Unity (docs.unity.com):

```csharp
using UnityEngine;
#if UNITY_IOS
// Include the IosSupport namespace if running on iOS:
using Unity.Advertisement.IosSupport;
#endif

public class AttPermissionRequest : MonoBehaviour {
  void Awake() {
#if UNITY_IOS
  // Check the user's consent status.
  // If the status is undetermined, display the request:
  if(ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED) {
    ATTrackingStatusBinding.RequestAuthorizationTracking();
  }
#endif
  }
}
```

[Unity — ATT Compliance guide](https://docs.unity.com/grow/en-us/ads/ios-sdk/ios14/att-compliance)

Автоматическая прописка `NSUserTrackingUsageDescription` в Info.plist через `PostProcessBuild` (официальный пример Unity):

```csharp
#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class PostBuildStep {
  const string k_TrackingDescription = "Your data will be used to provide you a better and personalized ad experience.";

  [PostProcessBuild(0)]
  public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToXcode) {
    if (buildTarget == BuildTarget.iOS) {
      AddPListValues(pathToXcode);
    }
  }

  static void AddPListValues(string pathToXcode) {
    string plistPath = pathToXcode + "/Info.plist";
    PlistDocument plistObj = new PlistDocument();
    plistObj.ReadFromString(File.ReadAllText(plistPath));
    PlistElementDict plistRoot = plistObj.root;
    plistRoot.SetString("NSUserTrackingUsageDescription", k_TrackingDescription);
    File.WriteAllText(plistPath, plistObj.WriteToString());
  }
}
#endif
```

[Unity — ATT Compliance guide](https://docs.unity.com/grow/en-us/ads/ios-sdk/ios14/att-compliance)

### 5.3. Порядок запроса

Официальная рекомендация Unity: ATT-запрос должен запускаться **до** инициализации любых SDK, которым нужен доступ к IDFA, поскольку Apple разрешает показывать этот диалог лишь один раз за установку, а пользователь в любой момент может изменить решение вручную в Настройках. Рекомендуемый порядок: 1) настроить `NSUserTrackingUsageDescription` в Info.plist (обязательно); 2) по желанию показать собственный экран-объяснение перед системным диалогом («ATT context screen»); 3) проверить `GetAuthorizationTrackingStatus()`, и если статус `NOT_DETERMINED` — показать системный запрос через `RequestAuthorizationTracking()`. [Unity — ATT Compliance guide](https://docs.unity.com/grow/en-us/ads/ios-sdk/ios14/att-compliance)

Для нашей игры (нет своей рекламы/SDK, требующих IDFA, если это так) ATT может быть вообще не нужен — запрашивать `NSUserTrackingUsageDescription`/`requestTrackingAuthorization` имеет смысл только если приложение реально трекает пользователя между приложениями/сайтами (например, через рекламный SDK с IDFA). Это общий вывод из документации Apple/Unity, а не отдельная явная цитата.

## Источники

- [Apple — UNUserNotificationCenter.add(_:withCompletionHandler:)](https://developer.apple.com/documentation/usernotifications/unusernotificationcenter/add(_:withcompletionhandler:))
- [Apple Developer Forums — Does UNNotificationRequest have a 64-notification scheduling limit?](https://developer.apple.com/forums/thread/811171)
- [Apple — UNAuthorizationOptions](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions)
- [Apple — UNAuthorizationOptions.provisional](https://developer.apple.com/documentation/usernotifications/unauthorizationoptions/provisional)
- [Apple — ATTrackingManager.requestTrackingAuthorization(completionHandler:)](https://developer.apple.com/documentation/apptrackingtransparency/attrackingmanager/requesttrackingauthorization(completionhandler:))
- [Use Your Loaf — Provisional Authorization of User Notificatons](https://useyourloaf.com/blog/provisional-authorization-of-user-notificatons/)
- [Unity — Mobile Notifications changelog 2.4](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/changelog/CHANGELOG.html)
- [Unity — Mobile Notifications manual (overview)](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/index.html)
- [Unity — iOS notifications manual](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.4/manual/iOS.html)
- [Unity — iOSNotificationCalendarTrigger API (2.1)](https://docs.unity3d.com/Packages/com.unity.mobile.notifications@2.1/api/Unity.Notifications.iOS.iOSNotificationCalendarTrigger.html)
- [GitHub — Unity-Technologies/com.unity.ads.ios-support](https://github.com/Unity-Technologies/com.unity.ads.ios-support)
- [Unity — ATT Compliance guide (docs.unity.com)](https://docs.unity.com/grow/en-us/ads/ios-sdk/ios14/att-compliance)
- [vmobify — Push Notification Strategy 2026](https://vmobify.com/blog/push-notification-strategy)
- [Pushwoosh — How to Increase Your Push Notification Opt-In Rate](https://www.pushwoosh.com/blog/increase-push-notifications-opt-in/)
- [SEM Nexus — Push Notification Timing: What the Data Says About Opt-In Rates](https://semnexus.com/push-notification-timing-data-opt-in-rates)
