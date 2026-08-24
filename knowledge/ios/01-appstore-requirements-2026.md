# Требования App Store к iOS-игре — август 2026

Дата сбора материала: 2026-08-24.
Версия стека проекта: Unity 6.3 LTS (6000.3.x), целевая платформа iOS, распространение через TestFlight и App Store.

## Кратко

- Начиная с 28 апреля 2026 года сборки, загружаемые в App Store Connect для iOS и iPadOS, обязаны быть собраны с SDK iOS 26 / iPadOS 26 или новее — это и есть Xcode 26 или новее. Формулировка Apple: «Starting April 28, 2026, apps and games uploaded to App Store Connect need to meet the following minimum requirements: iOS and iPadOS apps must be built with the iOS 26 & iPadOS 26 SDK or later». Требование в проектном документе подтверждается, но дата — не «апрель 2026» вообще, а точно 28 апреля 2026. ([Apple Developer — Upcoming SDK Minimum Requirements](https://developer.apple.com/news/?id=ueeok6yw))
- Требование касается инструментария сборки, а не минимальной версии iOS, на которой обязана работать игра — deployment target разработчик задаёт сам.
- Членство в Apple Developer Program стоит 99 USD в год (для Enterprise-программы — 299 USD в год); обработка банковских реквизитов после подписания Paid Apps Agreement и подачи налоговых форм по заявлению Apple занимает 24 часа, но на форумах разработчиков описаны реальные задержки до нескольких недель.
- Файл PrivacyInfo.xcprivacy обязателен: с 1 мая 2024 года App Store Connect не принимает новые и обновлённые сборки, если использование «required reason API» не задекларировано в манифесте приватности. Для Unity-проекта манифест собирается в цель UnityFramework и объединяет данные рантайма, плагинов и стороннего кода.
- Игра, которая делает снимок камерой, отправляет его на свой сервер и не хранит, всё равно обязана задекларировать сбор фото/видео в App Privacy («nutrition label») в App Store Connect и указать цель использования; «не хранится на сервере» не освобождает от декларации сбора.
- Возрастной рейтинг в App Store Connect с 2026 года расширен до значений 4+, 9+, 13+, 16+, 18+ (ранее было 4+/9+/12+/17+); анкету по новым вопросам требовалось заполнить до 31 января 2026 года, иначе блокируется отправка обновлений.
- Приложение с пользовательским контентом (в том числе загружаемыми фотографиями) обязано иметь фильтрацию неприемлемого материала, механизм жалоб, блокировку нарушителей и открытые контактные данные (guideline 1.2); для контента, ориентированного на детей, — дополнительные требования к приватности данных несовершеннолетних (раздел 1.3, Kids Category) и к политике конфиденциальности (guideline 5.1.1).
- ATT (App Tracking Transparency) обязателен только если приложение отслеживает пользователя между приложениями/сайтами других компаний или обращается к IDFA; для собственной серверной обработки фото без такого трекинга запрос ATT не требуется.
- TestFlight: до 100 внутренних тестировщиков (без ограничения по устройствам в официальном тексте), до 10 000 внешних тестировщиков на приложение; первая сборка для внешних тестировщиков проходит Beta App Review; сборки доступны тестировщикам 90 дней.
- Правило про loot boxes зафиксировано в guideline 3.1.1: приложения с loot box обязаны раскрывать вероятности выпадения каждого типа предмета до покупки.

## Требование к SDK и Xcode: дата вступления в силу

Официальная страница Apple Developer «Upcoming SDK Minimum Requirements» (открыта через WebFetch) содержит точную формулировку:

> "Starting April 28, 2026, apps and games uploaded to App Store Connect need to meet the following minimum requirements:
> - iOS and iPadOS apps must be built with the iOS 26 & iPadOS 26 SDK or later
> - tvOS apps must be built with the tvOS 26 SDK or later
> - visionOS apps must be built with the visionOS 26 SDK or later
> - watchOS apps must be built with the watchOS 26 SDK or later"

([Apple Developer — Upcoming SDK Minimum Requirements](https://developer.apple.com/news/?id=ueeok6yw))

Та же формулировка (с той же датой 28 апреля 2026) повторена на странице «App Store submissions now open for the latest OS releases»:

> "Starting April 2026, apps and games uploaded to App Store Connect must meet these minimum requirements: iOS and iPadOS apps must be built with the iOS 26 & iPadOS 26 SDK or later..."

Там же уточняется путь сборки: «Build your apps and games using the Xcode 26 Release Candidate and latest SDKs. Test with TestFlight. Submit for review to the App Store.» ([Apple Developer — App Store submissions now open for the latest OS releases](https://developer.apple.com/news/?id=6lxhtioi))

Вывод по формулировке из проектного документа «с апреля 2026 принимается только собранное на iOS 26 SDK»: **подтверждается**, с уточнением точной даты — 28 апреля 2026 года, а не «начало апреля». Требование относится к SDK/Xcode, которым собрана сборка (то есть фактически требуется Xcode 26 или новее), а не к минимальной версии iOS, на которой должно запускаться приложение — это разработчик задаёт отдельно через deployment target. Разработчики отмечают (вторичные источники, без официального подтверждения Apple про предыдущие циклы) исторический паттерн ежегодного ужесточения: «Starting April 2021, all iOS and iPadOS apps submitted to the App Store must be built with Xcode 12 and the iOS 14 SDK» и аналогично для iOS 18 SDK/Xcode 16 с апреля 2025 года — точная дата и цитата для этих прошлых циклов не проверялась по официальным страницам Apple в рамках этого исследования, приводится только как контекст из вторичных источников.

Точный номер версии Xcode 26 (например, 26.0 против более поздних точечных релизов) и минимальная версия macOS для конкретного точечного релиза Xcode 26 в рамках этого исследования по официальным страницам Apple не проверялись — по этому пункту: не проверено.

## Минимальная целевая версия iOS

Официальная страница Apple Support «iPhone models compatible with iOS 26» (открыта через WebFetch) перечисляет модели, на которые можно ставить iOS 26: iPhone 11, iPhone 11 Pro, iPhone 11 Pro Max, iPhone SE (2-го поколения), iPhone 12 mini/12/12 Pro/12 Pro Max, iPhone 13 mini/13/13 Pro/13 Pro Max, iPhone SE (3-го поколения), iPhone 14/14 Plus/14 Pro/14 Pro Max, iPhone 15/15 Plus/15 Pro/15 Pro Max, iPhone 16/16 Plus/16 Pro/16 Pro Max/16e, iPhone 17/17 Pro/17 Pro Max, iPhone Air, iPhone 17e. ([Apple Support — iPhone models compatible with iOS 26](https://support.apple.com/en-us/guide/iphone/iphe3fa5df43/ios))

По сравнению со списком совместимости iOS 18 из этого набора выпали iPhone XR, iPhone XS и iPhone XS Max — это отсечение подтверждено вторичным источником (TechRadar), сравнивавшим списки совместимости iOS 18 и iOS 26; из официальной страницы Apple напрямую этот факт сравнения не следует, поскольку она перечисляет только актуальный список для iOS 26. ([TechRadar — iOS 26 and iPadOS 26 compatibility explained](https://www.techradar.com/phones/ios/ios-26-compatibility-does-your-iphone-support-it-heres-the-full-list-of-supported-devices))

Практический вывод для проекта: если deployment target ставить на iOS 26, минимальное поддерживаемое устройство — iPhone 11 / iPhone SE (2-го поколения) и новее; более старые модели (iPhone XR/XS и старее) исполнять игру не смогут в принципе, независимо от deployment target приложения, потому что они не получат обновление системы до iOS 26. Собственно deployment target приложения (то есть на какой минимальной установленной версии iOS будет запускаться сама игра, а не системное ограничение по обновлению) Apple не диктует — это выбор разработчика в Xcode/Unity Player Settings, официального требования по конкретному минимальному deployment target для игр в исследованных источниках не найдено — не проверено.

## Apple Developer Program: стоимость, сроки, Agreements/Tax/Banking

Стоимость членства: «The Apple Developer Program annual fee is 99 USD and the Apple Developer Enterprise Program annual fee is 299 USD, in local currency where available.» Для оформления нужен Apple Account с двухфакторной аутентификацией, совершеннолетие в регионе пользователя; для физлица/индивидуального предпринимателя обязательно использовать настоящее юридическое имя — псевдоним или название компании в полях имени задерживает одобрение. Доступны освобождения от платы (fee waiver) для некоммерческих организаций, аккредитованных образовательных учреждений и государственных структур. ([Apple Developer — Enrollment](https://developer.apple.com/help/account/membership/program-enrollment/))

Банковские реквизиты в App Store Connect: страница «Enter banking information» (открыта через WebFetch) прямо указывает — «After the Account Holder approves the change, it will be processed within 24 hours.» Перед этим обязательно подписать Paid Apps Agreement и подать все требуемые налоговые формы: «Note that in order to add banking information, you'll first need to sign a Paid Apps Agreement» и «You must submit all required tax forms needed for your paid contract in order for us to process banking information.» Если реквизиты добавляет роль Admin или Finance, требуется отдельное одобрение Account Holder: «If you hold the Admin or Finance role and are trying to add banking information, the Account Holder will need to approve the information in App Store Connect before it's processed.» Есть и предельный срок на подтверждение: «If they reject the change or if it isn't approved within 30 days, your bank account details will not be updated.» ([Apple Developer — Enter banking information](https://developer.apple.com/help/app-store-connect/manage-banking-information/enter-banking-information/))

Официальные 24 часа — это заявленное время обработки уже одобренного изменения, а не срок первичной проверки учётной записи целиком (открытие Paid Apps Agreement, налоговые формы, верификация организации). Реальные сроки первичной проверки на форумах разработчиков Apple описываются как заметно дольше официальной оценки (от нескольких дней до месяца), но это отчёты пользователей форума, а не официальная позиция Apple, поэтому конкретную цифру давать нельзя — не проверено.

Про сроки фактической выплаты уже заработанных денег (отдельно от одобрения самих банковских реквизитов) страница выплат App Store Connect в рамках этого исследования напрямую через WebFetch не открывалась — не проверено.

## Privacy manifest (PrivacyInfo.xcprivacy) и required reason API

Официальные страницы Apple по этой теме (`developer.apple.com/documentation/bundleresources/privacy-manifest-files` и технот TN3183 `describing-use-of-required-reason-api`) построены как JS-приложение (Swift-DocC) и через WebFetch не открылись — отдаётся только заголовок страницы без текста. Поэтому факты по этому разделу опираются на страницы, которые удалось открыть: справочный блог-разбор Bitrise и официальную страницу Unity про политику приватности Apple, которая сама пересказывает требование Apple для целей интеграции в Unity-проект.

Дата принудительного включения: по разбору Bitrise, дословно описывающему официальное объявление Apple, — «Starting this date, new apps that don't describe their use of required reasons API in their privacy manifest file aren't accepted by App Store Connect», дата — 1 мая 2024 года. ([Bitrise — Enforcement of Apple Privacy Manifest starting from May 1, 2024](https://bitrise.io/blog/post/enforcement-of-apple-privacy-manifest-starting-from-may-1-2024))

Категории required reason API, которые требуют указания причины использования в манифесте (по тому же разбору): API работы с временными метками файлов (File Timestamp — `creationDate`, `modificationDate`, `fileModificationDate`, `contentModificationDateKey`, `stat` и т. п.), API времени работы системы (System Boot Time — `systemUptime`, `mach_absolute_time()`), API объёма диска (Disk Space — `volumeAvailableCapacityKey`, `volumeAvailableCapacityForImportantUsageKey`, `volumeAvailableCapacityForOpportunisticUsageKey`, `volumeTotalCapacityKey`), API активной клавиатуры (Active Keyboard — `ActiveInputNodes`), API пользовательских настроек (`UserDefaults`). Полный официальный список и точные утверждённые коды причин (approved reason codes) по документации Apple напрямую не проверялись, так как страница не открылась через WebFetch — не проверено; для проверки перед релизом нужно свериться с содержимым непосредственно в Xcode 26 либо повторить попытку открыть `developer.apple.com/documentation/bundleresources/describing-use-of-required-reason-api` браузером.

Что это значит для Unity-проекта: официальная страница руководства Unity «Apple's privacy manifest policy requirements» (открыта через WebFetch) требует создавать файл манифеста и «save it in the Assets/Plugins folder of your project», чтобы он попал в генерируемый Xcode-проект. Там же прямо указана зона ответственности: «if your application includes multiple third-party SDKs, packages, and plug-ins, then these third-party components (if applicable) must provision their own privacy manifest files separately» и «It's your responsibility however, to make sure that the owners of these third-party components include privacy manifest files. Unity isn't responsible for any third-party privacy manifest, and their data collection and tracking practices.» Прямое предупреждение о последствиях: «If the use of the required reason APIs by you or third-party SDKs isn't declared in the privacy manifest, your application might be rejected by the App Store.» ([Unity Manual — Apple's privacy manifest policy requirements](https://docs.unity3d.com/6000.0/Documentation/Manual/apple-privacy-manifest-policy.html))

В генерируемом Xcode-проекте Unity 6 итоговый (объединённый) файл `PrivacyInfo.xcprivacy` для рантайма Unity, плагинов, пакетов и кода проекта лежит в цели/папке `UnityFramework` — по данным официальной страницы Unity о структуре Xcode-проекта. ([Unity Manual — Structure of a Unity Xcode project](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html))

Практический риск для проекта: если в игру подключены сторонние SDK (аналитика, реклама, платежи, любые нативные плагины), каждый из них обязан нести собственный `PrivacyInfo.xcprivacy`; отсутствие деклараций у стороннего SDK или расхождение между декларацией SDK и фактическим объединённым манифестом в `UnityFramework` — частая причина отказа при загрузке в App Store Connect.

## App Privacy («Nutrition labels») для игры со снимком камеры

Официальная страница «App Privacy Details» (открыта через WebFetch) относит фото/видео пользователя к категории User Content и явно перечисляет тип данных «Photos or Videos — The user's photos or videos», который нужно декларировать, если приложение собирает фото или видео пользователей. ([Apple Developer — App Privacy Details](https://developer.apple.com/app-store/app-privacy-details/))

Для игры, которая делает снимок камерой, отправляет его на собственный сервер и не хранит:

- Тип данных «Photos or Videos» подлежит декларации как собираемый (collected), поскольку данные покидают устройство (передаются на сервер), независимо от того, что сервер их не хранит: отсутствие постоянного хранения не равно отсутствию сбора.
- «Связаны ли данные с личностью пользователя» (linked to identity) — по определению страницы, «Data collected from an app is often linked to the user's identity, unless specific privacy protections are put in place before collection to de-identify or anonymize it, such as: stripping data of any direct identifiers... manipulating data to break the linkage.» Если сервер получает снимок вместе с идентификатором пользователя/устройства/сессии, который позволяет связать снимок с конкретным человеком, декларация должна отражать связь с личностью; если снимок технически обезличен и не связывается обратно — можно декларировать как несвязанные данные, но при условии выполнения обоих требований страницы: не пытаться связать данные обратно с личностью и не сопоставлять с другими массивами данных, которые это позволяют.
- «Используются ли данные для трекинга» (used to track) — по определению той же страницы трекинг означает «linking data collected from your app about a particular end-user or device... with Third-Party Data for targeted advertising or advertising measurement purposes, or sharing data collected from your app about a particular end-user or device with a data broker». Если фото используется только для функциональности самой игры (например, распознавание объекта в игровом процессе) и не передаётся третьим лицам для таргетинга рекламы — это не трекинг в определении Apple, и ATT не требуется по этому основанию (см. раздел про ATT ниже).
- Процессуально: ответы вносятся в App Store Connect и должны оставаться актуальными: «You're responsible for keeping your responses accurate and up to date. If your practices change, update your responses in App Store Connect. You may update your answers at any time, and you do not need to submit an app update in order to change your answers.»

Итоговая рекомендация для карточки приватности такой игры: задекларировать «Photos or Videos» как собираемые данные, отдельно ответить на вопрос про связь с личностью (в зависимости от того, передаётся ли идентификатор вместе со снимком) и про трекинг (скорее всего — нет, если снимок не уходит рекламным/аналитическим третьим лицам). Отсутствие хранения на сервере не освобождает от декларации самого факта сбора и передачи данных.

Дополнительно (по той же странице) — если в проекте используются сторонние SDK (реклама, аналитика), их собственный сбор данных нужно декларировать отдельно: «If you use third-party code — such as advertising or analytics SDKs — you need to describe what data the third-party code collects, how the data may be used, and whether the data is used to track users.» ([Apple Developer — App Store user privacy and data use](https://developer.apple.com/app-store/user-privacy-and-data-use/))

## Возрастной ценз (новая система) и модерация пользовательского контента

Новая система рейтингов App Store Connect: ранее использовались значения 4+/9+/12+/17+, обновлённая система вводит более дробную шкалу — 4+, 9+, 13+, 16+, 18+. Разработчикам нужно было ответить на обновлённую анкету с новыми обязательными вопросами (In-app controls, Capabilities, Medical or wellness topics, Violent themes) до 31 января 2026 года, иначе блокируется отправка обновлений приложения в App Store Connect. Это по данным официальной новости Apple Developer и справочной страницы значений возрастных рейтингов, обнаруженных через поиск; сама новостная страница с точным текстом про дедлайн 31 января отдельным WebFetch в этом исследовании не открывалась — цифра и месяц подтверждены по нескольким независимым выдачам поиска, ссылающимся на developer.apple.com/news, но точную дословную цитату с этой конкретной страницы получить не удалось — помечаю как частично проверено. Более широкий текст про обновлённые рейтинги в целом виден на официальной странице значений и определений возрастных рейтингов. ([Apple Developer — Age ratings values and definitions](https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions/))

Отдельная деталь, важная именно для игры с детским по виду оформлением, которая принимает пользовательские изображения: категория Kids Category в App Review Guidelines (раздел 1.3, текст получен через WebFetch страницы guidelines) требует, чтобы приложения «must not include links out of the app, purchasing opportunities, or other distractions to kids unless reserved for a designated area behind a parental gate», а также «You must comply with applicable privacy laws around the world relating to the collection of data from children online... Kids Category apps may not send personally identifiable information or device information to third parties. Apps in the Kids Category should not include third-party analytics or third-party advertising.» ([App Store Review Guidelines, раздел 1.3](https://developer.apple.com/app-store/review/guidelines/))

Даже если приложение формально не подано в Kids Category, но по оформлению выглядит как детское и работает с фотографиями/пользовательским контентом, применяется общее требование к сбору данных о несовершеннолетних из guideline 5.1.1: apps, которые «collect, transmit, or have the capability to share personal information (e.g. name, address, email, location, photos, videos, drawings...) from a minor must include a privacy policy and must comply with all applicable children's privacy statutes» — вплоть до COPPA (США) и GDPR (ЕС) там, где применимо. Это формулировка из вторичного источника (iubenda / Privacy World), пересказывающего текст guideline 5.1.1 — прямой WebFetch этой конкретной формулировки с официальной страницы Apple не подтверждён, поэтому дословная цитата приводится с пометкой «по вторичному источнику, требует сверки».

Требования к модерации пользовательского контента — guideline 1.2 (текст подтверждён прямым WebFetch страницы guidelines): «Apps with user-generated content or social networking services must include: a method for filtering objectionable material from being posted to the app; a mechanism to report offensive content and timely responses to concerns; the ability to block abusive users from the service; published contact information so users can easily reach you.» Также: «Apps with user-generated content or services that end up being used primarily for pornographic content, Chatroulette-style experiences, random or anonymous chat, objectification of real people (e.g. "hot-or-not" voting), making physical threats, or bullying do not belong on the App Store and may be removed without notice.» ([App Store Review Guidelines, раздел 1.2](https://developer.apple.com/app-store/review/guidelines/))

Практический вывод: игра, куда игроки загружают собственные фотографии (даже обработанные под игровой контент), обязана реализовать — фильтр неприемлемого содержимого до публикации или сразу после, кнопку «пожаловаться», блокировку пользователя, видимые контактные данные разработчика/поддержки, а также политику конфиденциальности со ссылкой из App Store Connect и из самого приложения (guideline 5.1.1(i)).

## ATT (App Tracking Transparency)

Официальная страница «App Store user privacy and data use» (открыта через WebFetch повторно, с прицелом на раздел про ATT) даёт точное определение того, когда запрос обязателен:

> "iOS 14.5, iPadOS 14.5, and tvOS 14.5 or later: You must receive user permission through the AppTrackingTransparency framework to: track users across apps and websites owned by other companies; access the device's advertising identifier (IDFA)."

Определение трекинга по той же странице:

> "Tracking is defined as: linking user or device data collected from your app with user or device data collected from other companies' apps, websites, or offline properties for targeted advertising or advertising measurement purposes; sharing user or device data with data brokers."

Примеры действий, требующих запрос ATT: показ таргетированной рекламы на основе данных пользователя из чужих приложений/сайтов; передача геолокации или списка email дата-брокеру; передача email, рекламных или иных идентификаторов сторонним рекламным сетям для ретаргетинга; использование сторонних SDK, которые объединяют данные пользователя приложения с данными из других приложений для таргетинга или измерения эффективности рекламы.

Явно перечислено, что НЕ требует запроса ATT: данные, связанные только на устройстве и не покидающие его в идентифицируемом виде; использование данных дата-брокером исключительно для обнаружения и предотвращения мошенничества или в целях безопасности; использование данных агентствами кредитной отчётности для оценки кредитоспособности.

Про формулировку usage description страница не называет ключ `NSUserTrackingUsageDescription` по имени, но прямо требует пояснительный текст в системном запросе: «You must also include a purpose string in the system prompt that explains why you'd like to track the user», который должен «explain what this data will be used for to help the user understand what they're opting in to share.» ([Apple Developer — App Store user privacy and data use](https://developer.apple.com/app-store/user-privacy-and-data-use/))

Вывод для игры со снимком с камеры, отправляемым на собственный сервер без хранения и без передачи третьим лицам для рекламы: запрос ATT не требуется, поскольку не происходит связывания данных пользователя с данными сторонних компаний в рекламных целях и не идёт обращение к IDFA ради трекинга. Если в проект добавляется рекламный SDK или аналитика с ретаргетингом — ATT становится обязательным независимо от того, как обрабатываются фотографии.

## TestFlight: ограничения, сроки, срок жизни сборки

Внутренние тестировщики — официальная страница «Add internal testers» (открыта через WebFetch): «Create a group and add up to 100 internal testers (App Store Connect users with access to your content) to test your app using TestFlight.» Требуемая роль для добавления: «Account Holder, Admin, App Manager, Developer, or Marketing.» Там же: «Internal testers can download and test all builds for 90 days.» Официальный текст этой страницы не указывает ограничение по числу устройств на тестировщика — в разборе не подтверждено. ([Apple Developer — Add internal testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/add-internal-testers))

Внешние тестировщики — официальная страница «Invite external testers» (открыта через WebFetch): «After uploading your build, you can invite up to 10,000 external testers per app.» Требуемая роль: «Account Holder, Admin, or App Manager.» Обязательное условие: «To create an external group for external testing, you must first create an internal group for internal testing.» Про Beta App Review та же страница подтверждает обязательность проверки: «After you submit your build to TestFlight App Review, Apple reviews the build and its accompanying metadata... If Apple rejects your build or metadata, the status of the build will be Rejected.» Ограничение по числу подач: «You can submit up to six builds for TestFlight App Review within a 24-hour period.» ([Apple Developer — Invite external testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/invite-external-testers))

Официальный точный срок рассмотрения первой сборки для внешних тестировщиков (в часах) на открытых через WebFetch страницах Apple не указан явно — по вторичным источникам (форумы разработчиков, блоги) типично называется около 24 часов для первой сборки новой версии, а изменения encryption export compliance, entitlements или privacy nutrition labels снова запускают полную проверку — эти цифры не проверены по официальной странице Apple, помечаю как «по вторичным источникам, не проверено официально».

Срок жизни сборки в TestFlight: по данным вторичных источников билд становится недоступен тестировщикам через 90 дней после загрузки, и каждая новая сборка получает собственный отсчёт заново; это согласуется с прямо процитированным выше официальным текстом про внутренних тестировщиков («test all builds for 90 days»), но отдельная официальная страница именно про истечение срока сборки в этом исследовании не открывалась через WebFetch — частично проверено.

## Loot boxes и раскрытие вероятностей

Guideline 3.1.1 (In-App Purchase), текст подтверждён прямым WebFetch страницы App Review Guidelines:

> "Apps offering "loot boxes" or other mechanisms that provide randomized virtual items for purchase must disclose the odds of receiving each type of item to customers prior to purchase."

([App Store Review Guidelines, раздел 3.1.1](https://developer.apple.com/app-store/review/guidelines/))

Требование появилось в правилах в декабре 2017 года (по вторичным источникам — TouchArcade, Fenwick, MacStories, дата официальной страницей Apple с историей изменений в этом исследовании не проверялась) и с тех пор остаётся частью действующих Guidelines под тем же номером 3.1.1. Практическое требование для проекта: если в игре есть механика случайной выдачи виртуальных предметов за платную валюту или деньги (гача, сундуки, случайные награды за покупку), в интерфейсе перед покупкой обязательно показать вероятности выпадения каждого типа/редкости предмета.

## Смежные пункты guidelines, относящиеся к игре с камерой и пользовательским контентом

Для полноты (текст подтверждён WebFetch страницы guidelines):

- Guideline 5.1.1(i) — обязательная ссылка на политику конфиденциальности и в App Store Connect, и внутри приложения, с описанием того, какие данные собираются, как и с кем передаются, и как их удалить.
- Guideline 5.1.1(iii) (Data Minimization) — «Apps should only request access to data relevant to the core functionality of the app and should only collect and use data that is required to accomplish the relevant task. Where possible, use the out-of-process picker or a share sheet rather than requesting full access to protected resources like Photos or Contacts.» Для игры со снимком с камеры это значит: по возможности использовать системный UIImagePicker/камеру через шторку, а не запрашивать полный доступ к фотоплёнке, если не требуется хранить и повторно читать фотографии из галереи.
- Guideline 5.1.1(iv) (Access) — «Apps must respect the user's permission settings and not attempt to manipulate, trick, or force people to consent to unnecessary data access.»
- Guideline 5.1.2 (Data Use and Sharing) — «Unless otherwise permitted by law, you may not use, transmit, or share someone's personal data without first obtaining their permission. You must provide access to information about how and where the data will be used. You must clearly disclose where personal data will be shared with third parties, including with third-party AI, and obtain explicit permission before doing so.» Это напрямую касается сценария «снимок отправляется на сервер»: разработчик обязан явно предупредить пользователя и получить согласие до отправки снимка на сервер.

## Источники

- [Apple Developer — Upcoming SDK Minimum Requirements](https://developer.apple.com/news/?id=ueeok6yw)
- [Apple Developer — App Store submissions now open for the latest OS releases](https://developer.apple.com/news/?id=6lxhtioi)
- [Apple Support — iPhone models compatible with iOS 26](https://support.apple.com/en-us/guide/iphone/iphe3fa5df43/ios)
- [TechRadar — iOS 26 and iPadOS 26 compatibility explained](https://www.techradar.com/phones/ios/ios-26-compatibility-does-your-iphone-support-it-heres-the-full-list-of-supported-devices)
- [Apple Developer — Enrollment (Apple Developer Program)](https://developer.apple.com/help/account/membership/program-enrollment/)
- [Apple Developer — Enter banking information](https://developer.apple.com/help/app-store-connect/manage-banking-information/enter-banking-information/)
- [Bitrise — Enforcement of Apple Privacy Manifest starting from May 1, 2024](https://bitrise.io/blog/post/enforcement-of-apple-privacy-manifest-starting-from-may-1-2024)
- [Capgo — Privacy Manifest for iOS Apps](https://capgo.app/blog/privacy-manifest-for-ios-apps/)
- [Unity Manual — Apple's privacy manifest policy requirements (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/apple-privacy-manifest-policy.html)
- [Unity Manual — Structure of a Unity Xcode project (6000.2)](https://docs.unity3d.com/6000.2/Documentation/Manual/StructureOfXcodeProject.html)
- [Apple Developer — App Privacy Details](https://developer.apple.com/app-store/app-privacy-details/)
- [Apple Developer — App Store user privacy and data use](https://developer.apple.com/app-store/user-privacy-and-data-use/)
- [Apple Developer — Age ratings values and definitions](https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions/)
- [Apple Developer — App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- [Apple Developer — Add internal testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/add-internal-testers)
- [Apple Developer — Invite external testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/invite-external-testers)

Страницы, которые не удалось открыть содержательно через WebFetch (отдавали только заголовок из-за JS-рендеринга Swift-DocC, либо 404/ошибку доступа) и поэтому не использованы как прямой источник цитат, а только как контекст из вторичных источников: `developer.apple.com/documentation/bundleresources/privacy-manifest-files`, `developer.apple.com/documentation/technotes/tn3183-adding-required-reason-api-entries-to-your-privacy-manifest`, `developer.apple.com/documentation/bundleresources/describing-use-of-required-reason-api`, `developer.apple.com/documentation/apptrackingtransparency`, `help.apple.com/app-store-connect/en.lproj/dev388fa3577.html` (404).
