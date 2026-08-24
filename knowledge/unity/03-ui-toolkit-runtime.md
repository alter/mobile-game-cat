# UI Toolkit в runtime: состояние на Unity 6.3 LTS

Дата сбора материала: 2026-08-24.
Версия стека: Unity 6.3 LTS (версия сборки документации Unity — 6000.3.x); часть страниц Unity ещё не переиздана под 6.3 и цитируется по версиям 6000.0–6000.4 (Unity держит почти идентичный текст между патч-релизами 6.x, расхождения отмечены отдельно).

Область: исключительно runtime UI (игра во время выполнения на мобильном устройстве), не редакторские окна Unity.

---

## Кратко

- Официальная позиция Unity: для runtime Unity по-прежнему рекомендует uGUI как основной вариант, а UI Toolkit — как альтернативу; для редакторских инструментов — наоборот, UI Toolkit основной. Это прямо написано в странице сравнения систем. [Unity Manual: Comparison of UI systems in Unity](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- У UI Toolkit нет: сериализуемых событий (как UnityEvent в инспекторе), авторинга прямо в сцене (элементы не GameObject), интеграции с Animation Clips и Timeline. У uGUI, в свою очередь, нет: системы data binding, переходных анимаций USS, глобального управления стилями, поддержки SVG и RTL-языков. [Comparison of UI systems](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- Раскладка построена на Yoga (подмножество Flexbox); единицы измерения USS — только `px` и `%`, других CSS-единиц (em, rem, vh, vw) нет. [USS data types](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-PropertyTypes.html)
- Runtime data binding — сравнительно новая возможность (задокументирована для линейки Unity 6), позволяет привязывать свойства обычного C#-объекта к свойствам элемента без ручного цикла обновления, но у неё есть заметные накладные расходы (см. ниже жалобу про 664 привязки). [Data binding](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-data-binding.html), [жалоба на производительность рантайм-биндингов](https://discussions.unity.com/t/ui-toolkit-runtime-bindings-performance/1593988)
- Перетаскивание элементов (drag-and-drop) в runtime не имеет готового компонента — реализуется вручную через `PointerDownEvent`/`PointerMoveEvent`/`PointerUpEvent`/`PointerCaptureOutEvent` в кастомном `PointerManipulator`. Официальный пример есть только для Editor-окон, но использует тот же API, который работает и в runtime. [Create a drag-and-drop UI](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-create-drag-and-drop-ui.html)
- Безопасная зона (вырез/чёлка на iPhone) не имеет встроенного решения в UI Toolkit — `Screen.safeArea` даёт прямоугольник с началом координат внизу слева (в отличие от UI Toolkit, где начало координат вверху слева), поэтому координаты нужно инвертировать вручную; готовых Unity-компонентов для этого нет, есть сторонние пакеты. [обсуждение и пакет artstorm/ui-toolkit-safe-area](https://github.com/artstorm/ui-toolkit-safe-area)
- Производительность на мобильных телефонах ниже уровня «безусловно лучше uGUI»: разработчики фиксировали падение FPS до 30 на слабых Android-устройствах (Xiaomi Redmi Note 4) из-за дорогого универсального («uber») шейдера UI Toolkit с большим количеством ветвлений; инженер Unity подтвердил это как известное ограничение архитектуры (оптимизация под CPU-bottleneck ценой более дорогого шейдера на GPU). [UIToolkit rendering is extremely slow on older Android devices](https://discussions.unity.com/t/uitoolkit-rendering-is-extremely-slow-on-older-android-devices/1561024)
- World Space UI (UI в мировых координатах, не оверлей) есть только с Unity 6.2+, настраивается через `PanelSettings.renderMode = World Space` и `Pixels Per Unit`; интеграция с 2D sorting layers прямо не поддерживается. [World Space UI](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/world-space-ui.html), [Create a World Space UI](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/create-world-space-ui.html)
- Практики Unity рекомендуют классы USS (`AddToClassList`/`RemoveFromClassList`) вместо инлайновых стилей — инлайновые стили создают накладные расходы на каждый элемент и не поддерживают псевдоклассы (`:hover` и т.п.). [Best practices for USS](https://docs.unity3d.com/Manual/UIE-USS-WritingStyleSheets.html)
- Мнения практиков расходятся: часть разработчиков жалуется на избыточно сложную иерархию классов, «сотни строк USS только чтобы убрать рантайм-стили по умолчанию» и бесполезный отладчик в игровом режиме; другие показывают тестами (не от Unity, а от студии) кратное превосходство UI Toolkit по draw call'ам и памяти. Однозначного консенсуса «переходить всем» нет. [UI Toolkit frustrations](https://discussions.unity.com/t/ui-toolkit-frustrations/1685389), [Angry Shark Studio: UI Toolkit vs UGUI 2025](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)

---

## 1. Сравнение UI Toolkit и uGUI (официальная позиция Unity)

Источник — актуальная для 6.3 страница «Comparison of UI systems in Unity». [Unity Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)

**Рекомендации по применению:**
- Runtime: основная система — uGUI («easy referencing from MonoBehaviours»), альтернатива — UI Toolkit (когда в проекте много экранов UI и нужен знакомый воркфлоу для художников/дизайнеров).
- Editor: основная система — UI Toolkit («better reusability and decoupling», «visual tools for authoring UI»), альтернатива — IMGUI.
- Статус разработки: UI Toolkit активно развивается и получает новые возможности каждый релиз; uGUI и IMGUI — «established and production-proven UI systems that are updated infrequently» (зрелые и проверенные боем, но обновляются редко).

**Чего нет в UI Toolkit по сравнению с uGUI** (данные страницы сравнения):
- сериализуемых событий (UnityEvent-подобных полей в инспекторе);
- авторинга непосредственно в сцене (элементы UI Toolkit — не GameObject, их нельзя тащить как объекты сцены);
- интеграции с Animation Clips и Timeline.

**Чего нет в uGUI по сравнению с UI Toolkit:**
- системы data binding;
- USS-анимаций переходов (transition);
- глобального управления стилями (аналог темы/каскада);
- поддержки SVG;
- поддержки языков с письмом справа налево (RTL).

Практический вывод для мобильной 2D-головоломки: если в игре важны анимации через Timeline/Animator на UI, интеграция с существующими uGUI-ассетами или сериализуемые UnityEvent в инспекторе — это минус UI Toolkit, который придётся компенсировать кодом.

---

## 2. Устройство: UIDocument, PanelSettings, VisualTreeAsset, StyleSheet

Схема связей (по официальному руководству «Creating your first runtime UI»): [Unity Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html)

1. **PanelSettings** — ассет, который задаёт настройки экрана панели: режим масштабирования, разрешение, порядок отрисовки (sort order), под каким именем UI будет виден в UI Toolkit Debugger. Создаётся через `Assets > Create > UI Toolkit > Panel Settings Asset`.
2. **UIDocument** — компонент на GameObject в сцене. Ссылается на `PanelSettings` и на корневой `VisualTreeAsset` (UXML-файл, «Source Asset»). При входе в Play Mode автоматически загружает назначенный UXML.
3. **VisualTreeAsset** — то, как Unity представляет UXML-файл в C#; обычный ассет проекта, можно загружать через `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>` или ссылаться полем в инспекторе.
4. **StyleSheet** — то, как Unity представляет USS-файл в C#; так же является обычным ассетом.

Оба типа (`VisualTreeAsset`, `StyleSheet`) — «regular Unity assets», их можно подключать стандартными способами Unity (перетаскиванием в инспектор, загрузкой по пути). [Load UXML and USS in C# scripts](https://docs.unity3d.com/Manual/UIE-manage-asset-reference.html)

**Что класть в сцену:** один GameObject с `UIDocument` на экран/панель (или общий на всё приложение, если экраны переключаются видимостью корневых `VisualElement`). Корень визуального дерева — `UIDocument.rootVisualElement` (в редакторе — `EditorWindow.rootVisualElement`), от него строится вся иерархия `VisualElement`. [Introduction to visual elements and the visual tree](https://docs.unity3d.com/Manual/UIE-VisualTree.html)

**Пример из официального руководства** (структура UXML-файла со списком персонажей):

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:uie="UnityEditor.UIElements" editor-extension-mode="False">
    <Style src="MainView.uss" />
    <ui:VisualElement name="background">
        <ui:VisualElement name="main-container">
            <ui:ListView focusable="true" name="character-list" />
```

Шаги настройки сцены (дословно из руководства): `GameObject > UI Toolkit > UI Document`, затем перетащить `MainView.uxml` в поле `Source Asset` компонента `UIDocument`. [Create a list view runtime UI](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html)

**Важная деталь жизненного цикла:** когда UI перезагружается (например, при смене UXML-файла на лету), все связанные `MonoBehaviour`-компоненты «disabled before the reload, and then re-enabled after» — поэтому код инициализации UI следует держать в `OnEnable()`/`OnDisable()`, а не в `Start()`. [Creating a Runtime UI with UIDocument](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html)

Пример контроллера из руководства:

```csharp
void OnEnable()
{
    var uiDocument = GetComponent<UIDocument>();
    var characterListController = new CharacterListController();
    characterListController.InitializeCharacterList(
        uiDocument.rootVisualElement, m_ListEntryTemplate);
}
```

Также в новых версиях Unity 6 появился компонент **Panel Renderer** — альтернативный способ подключения панели к GameObject-иерархии рендеринга; он инициализирует визуальное дерево при создании компонента, при изменении `PanelSettings`/`VisualTreeAsset` либо при включении компонента. [Panel Renderer component](https://docs.unity3d.com/6000.6/Documentation/Manual/ui-systems/panel-renderer-component.html) (страница документирована для версии 6000.6, для 6.3 не проверено — возможно, компонента ещё нет в этой сборке).

**Тема по умолчанию:** при добавлении первого `UIDocument` в проект автоматически генерируется ассет темы `Assets/UI Toolkit/UnityThemes/UnityDefaultTheme.tss` (Theme Style Sheet, TSS); чтобы штатные контролы (кнопки, поля и т.п.) выглядели и работали правильно, этот файл темы нужно импортировать, а дальше можно переопределять/дополнять стили поверх него. [Theme Style Sheet (TSS)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-tss.html) (не проверено WebFetch напрямую — взято из результатов поиска).

---

## 3. Синтаксис UXML и USS

### Единицы измерения

UI Toolkit поддерживает только два типа единиц длины: пиксели (`px`, абсолютные) и проценты (`%`, относительно родителя). Если единица не указана явно, значение считается пикселями; исключение — `0`, для которого единица не обязательна. Числовые (не-length) значения задаются как float или integer-литералы, например `flex: 1.0`. [USS data types](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-PropertyTypes.html)

Других CSS-единиц (`em`, `rem`, `vh`, `vw`, `pt` и т.п.) в USS нет — это отличие от полного CSS, важное при переносе вёрстки из веба.

В C# длину можно задавать через структуру `Length`:

```csharp
new Translate(new Length(10f, LengthUnit.Percent), new Length(50f, LengthUnit.Pixel))
// эквивалент через неявные преобразования:
new Translate(Length.Percent(10), 50)
```

### Раскладка (Flexbox / Yoga)

Движок раскладки UI Toolkit построен на Yoga — реализации подмножества Flexbox из мира HTML/CSS; свойства UI Toolkit соответствуют поведению Yoga и покрывают «most properties in Flexbox». По умолчанию все элементы участвуют в раскладке, а контейнер раскладывает дочерние элементы по вертикали. [Position element with the layout engine](https://docs.unity3d.com/Manual/UIE-LayoutEngine.html)

Основные поддержанные свойства Flexbox (проверено по манулу):
- `flex-direction` (Flex > Direction) — направление главной оси; `row` переключает раскладку в горизонтальную.
- `flex-grow` (Flex > Grow) — доля роста элемента по главной оси относительно братских элементов; `1` у двух соседей даёт по 50% доступного места родителя.
- `justify-content` (Align > Justify Content) — выравнивание по главной оси (`flex-start`, `flex-end` и т.д., значения зависят от `flex-direction`).
- `position: absolute` — переводит элемент вне потока Flexbox-раскладки («invisible to the default Flexbox-based layout engine, as if it no longer takes any space»); абсолютно позиционированные элементы отображаются поверх относительно позиционированных братьев.
- `display: flex | none` — USS-свойство `display` поддерживает только небольшое подмножество ключевых слов CSS `display` (не полный список из веба).
- `overflow: visible | hidden` — по умолчанию `visible` (контент не обрезается), `hidden` обрезает по границам элемента (полезно для масок).

[Introduction to UI Toolkit / layout summary via поиск, подтверждено по частям через UIE-LayoutEngine.html]

### Особенность блочной модели

Модель блока в USS соответствует установке CSS-свойства `box-sizing: border-box` — то есть padding и border включены в заданную ширину/высоту элемента, а не добавляются к ней (в отличие от классической CSS content-box модели по умолчанию). [USS properties reference](https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html)

### Свойства USS: наследование и анимируемость

Большинство layout- и позиционных свойств **не наследуются**. Наследуются текстовые свойства: `color`, `font-size`, `letter-spacing`, `text-shadow`, `-unity-font`, `-unity-font-style`, `-unity-text-align`, `white-space`. [USS properties reference](https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html)

Свойства делятся на три категории по анимируемости: полностью анимируемые (большинство размеров, отступов, цвета, трансформаций), дискретные (направление раскладки, режим отображения, шрифт, режим позиционирования) и неанимируемые (курсор, `display`, сами переходы, настройки текстового генератора).

Unity добавляет собственные свойства с префиксом `-unity-`, которых нет в стандартном CSS: `-unity-font`, `-unity-font-definition`, `-unity-material` (кастомные материалы рендеринга), `-unity-slice-*` (9-slice масштабирование изображений), `-unity-text-outline` и другие текстовые свойства.

### Трансформации без перестроения раскладки

Свойства `translate`, `rotate`, `scale` анимируемы и **не** вызывают пересчёт раскладки соседних элементов — это явно выделено в документации как способ подешевле анимировать элементы. [USS transform](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-Transform.html)

Примеры синтаксиса (дословно из мануала):

```css
/* translate */
translate: 80%;
translate: 35px;
translate: 5% 10px;
translate: 24px 0%;

/* scale */
scale: 2.5;
scale: -1 1;
scale: none;

/* rotate */
rotate: 45deg;
rotate: -100grad;
rotate: -3.14rad;
rotate: 0.75turn;
rotate: none;
```

Формулировка мануала: «Applying transform to an element reduces recalculations because it doesn't change the layout of other elements in the hierarchy» — то есть трансформации предпочтительны для анимаций вместо прямого изменения `position`/`margin`.

### border-radius и проценты

Отдельная особенность (найдена через WebSearch по документации, не подтверждена дословно повторным WebFetch той же формулировки, но воспроизводится в нескольких версиях мануала): если задать `border-radius` в процентах, Unity сначала переводит процент в пиксели, а затем ограничивает итоговый радиус половиной этого пиксельного значения — то есть для элемента 100×100 px любой радиус больше 50px будет обрезан до 50px.

---

## 4. Data binding (привязка данных) в Unity 6

Data binding — то, чего у uGUI нет вовсе (см. раздел 1), и это одно из главных архитектурных отличий UI Toolkit. В Unity 6 есть **две системы привязки**: [Data binding](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-data-binding.html)

- **Runtime data binding** — привязывает свойства произвольного C#-объекта («plain C# object», не обязательно Unity-объект) к свойствам контрола UI. Работает и в runtime UI, и в editor UI (кроме сериализованных данных).
- **SerializedObject data binding** — привязка к `SerializedObject`, работает только в editor UI (даёт undo/redo и поддержку мультивыделения) — для игрового runtime не актуальна.

Официальная формулировка: «Data binding synchronizes properties of non-UI objects, such as a string property on a MonoBehaviour, with properties of UI objects, such as the value property of a TextField.»

### Создание привязки в C#

Общий порядок действий: [Create a runtime binding in C# scripts](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-runtime-binding-types.html)

1. Создать объект `DataBinding`.
2. Задать `dataSource` (объект-источник) и `dataSourcePath` (путь от источника к нужному свойству).
3. Задать режим привязки (binding mode) и триггер обновления (update trigger).
4. Зарегистрировать привязку на визуальном элементе через `SetBinding()`, при необходимости добавить конвертеры типов.

Режимы привязки, встречающиеся в примерах API: `ToTarget` (источник → UI), `ToSource` (UI → источник), `TwoWay` (двусторонняя), `ToTargetOnce` (однократно из источника в UI).

Пример регистрации привязки (дословно из документации):

```csharp
vector3Field.SetBinding("value", new DataBinding
{
    dataSourcePath = new PropertyPath(nameof(ExampleObject.vector3Value))
});
```

Дополнительные методы управления привязками: `GetBinding()`, `TryGetBinding()`, `HasBinding()`, `ClearBinding()`. Конвертация типов между источником и UI — через `sourceToUiConverters.AddConverter()`.

**Важное ограничение:** UI Toolkit не отслеживает изменения `element.style` и `element.resolvedStyle` — привязку можно нацелить на resolved style элемента, но отслеживать изменения в нём через binding нельзя. [Data binding manual, через WebFetch UIE-data-binding.html и сопутствующий поиск]

**UXML-декларация привязки** (по данным поиска, структура `<Bindings>`/`<ui:DataBinding>` с атрибутами `property`, `data-source-path`, `binding-mode` — сам UXML-пример не открыт напрямую через WebFetch, отмечаю как не проверено дословно).

### Производительность биндингов — см. раздел 7 (там разобрана реальная жалоба на 664 привязки в `ScrollView`).

---

## 5. Обработка ввода и событий

### Система событий по умолчанию

При входе в Play Mode UI Toolkit сам создаёт «default event system that is not part of any scene, and provides basic support for most input devices» — то есть в простом сценарии (только UI Toolkit, без uGUI) отдельный `EventSystem` в сцену добавлять не нужно. [Runtime UI event system](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-Runtime-Event-System.html)

`EventSystem`-компонент становится нужен, когда UI Toolkit сочетается с uGUI: при добавлении первого uGUI-элемента в сцену Unity автоматически добавляет `EventSystem` и `Standalone Input Module`. Модуль ввода нужно выбирать по активной системе ввода проекта:
- **Standalone Input Module** — для легаси Input Manager, «dispatches events to UI Toolkit elements».
- **Input System UI Input Module** — для пакета Input System; вместе со своим `EventSystem` «ensure that the events from both UI Toolkit and uGUI elements are properly dispatched».

Страница явно не описывает специфику тач-ввода отдельно от указателя — тач обрабатывается в общей модели указательных (pointer) событий.

### Pointer-события

Базовый класс всех событий указателя — `PointerEventBase`. [Pointer events](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Pointer-Events.html)

- **PointerDownEvent** — отправляется при нажатии указателя; цель — элемент, захвативший указатель (pointer capture), либо верхний по Z-порядку выбираемый элемент под курсором.
- **PointerMoveEvent** — при изменении состояния указателя (движение, смена нажатых кнопок и т.п.), таргетинг аналогичен PointerDown.
- **PointerUpEvent** — при отпускании указателя внутри визуального элемента; при срабатывании также «removes the pointer coordinates».

Свойства: `pointerId` («returns an integer that identifies the pointer that sends the event» — критично для мультитача, т.к. у каждого касания свой id); `pressure` (сила нажатия touch, `1.0f`, если устройство её не сообщает).

Все три события: «Trickles down: Yes», «Bubbles up: Yes», «Cancellable: Yes» — то есть идут по стандартной модели распространения (сначала сверху вниз по дереву — capture/trickle-фаза, потом фаза цели, потом снизу вверх — bubble). Отключённые (`disabled`) элементы `PointerDownEvent` не получают.

### ClickEvent

`ClickEvent` — «occurs when the user clicks the left mouse button (or the first button on a pointing device) over a VisualElement». Клик — это `PointerDownEvent`, за которым следует `PointerUpEvent` **на том же VisualElement**; между ними указатель может двигаться, лишь бы down и up произошли над одним и тем же элементом. [Click events](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Click-Events.html)

Пример обработчика (дословно из мануала):

```csharp
private void OnBoxClicked(ClickEvent evt)
{
    if (evt.propagationPhase != PropagationPhase.AtTarget)
        return;

    var targetBox = evt.target as VisualElement;
    targetBox.style.backgroundColor = GetRandomColor();
}
```

`ClickEvent` полезен, чтобы ловить клики по произвольным `VisualElement`, не только по кнопкам — например, реализация `Toggle` использует `ClickEvent` для переключения состояния и показа галочки.

### Перетаскивание (drag-and-drop) — критично для игры с перетаскиванием предметов

Готового runtime-компонента drag-and-drop в UI Toolkit нет. Официальный пример (написан для Editor-окон, но построен на общем API `PointerManipulator`, который работает и в runtime) — класс `DragAndDropManipulator : PointerManipulator`: [Create a drag-and-drop UI inside a custom Editor window](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-create-drag-and-drop-ui.html)

Структура манипулятора:
- Конструктор сохраняет `target` и ссылку на корень визуального дерева (родителя).
- `PointerDownHandler` — сохраняет стартовую позицию `target` и указателя, вызывает захват указателя (`target.CapturePointer(pointerId)`), помечает, что перетаскивание началось.
- `PointerMoveHandler` — если перетаскивание активно и указатель захвачен, пересчитывает новую позицию `target` в пределах окна по дельте движения указателя.
- `PointerUpHandler` — проверяет состояние перетаскивания и захвата, отпускает указатель.
- `PointerCaptureOutHandler` — при потере захвата ищет все слоты, определяет пересекающиеся, находит ближайший и либо примагничивает объект к слоту, либо возвращает его на исходную позицию.
- `RegisterCallbacksOnTarget()` / `UnregisterCallbacksFromTarget()` — регистрируют/снимают все четыре колбэка с `target`.

Регистрация колбэков (по общей документированной практике для перетаскиваемых элементов): нужно зарегистрировать `PointerDownEvent`, `PointerMoveEvent`, `PointerUpEvent` (и `PointerCaptureOutEvent`, чтобы корректно обработать прерывание захвата — например, если палец соскользнул с экрана):

```csharp
target.RegisterCallback<PointerDownEvent>(PointerDownHandler);
target.RegisterCallback<PointerMoveEvent>(PointerMoveHandler);
target.RegisterCallback<PointerUpEvent>(PointerUpHandler);
target.RegisterCallback<PointerCaptureOutEvent>(PointerCaptureOutHandler);
```

Практический вывод для «перетаскивания предметов» в головоломке: логику придётся писать самим поверх pointer-событий и `PointerManipulator`, готового решения «из коробки» Unity не даёт ни в uGUI, ни в UI Toolkit (в uGUI есть `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`, что несколько ближе к «из коробки», но тоже требует ручной реализации логики слотов).

### Что не покрыто официальной документацией

Отдельная FAQ-страница по вводу и событиям не содержит сравнения touch vs mouse, не описывает мультитач-жесты и ограничения — там разобраны только вопросы ремаппинга клавиш, `EventSystem.current.IsPointerOverGameObject`, `panel.Pick()` и навигация фокусом. [FAQ for input and event systems with UI Toolkit](https://docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html) — то есть по жестам (pinch-to-zoom, свайпы, мультитач-распознавание) официальной документации UI Toolkit нет; на уровне общего Unity-ввода разработчики решают это через сторонние библиотеки (TouchScript, TouchKit) даже вне UI Toolkit, что говорит об отсутствии единого штатного решения для сложных жестов в принципе.


## 6. Масштабирование под разные экраны и безопасная зона

### PanelSettings: режимы масштабирования

`PanelSettings.scaleMode` (тип `PanelScaleMode`) поддерживает три значения: [PanelScaleMode enum](https://docs.unity3d.com/2021.2/Documentation/ScriptReference/UIElements.PanelScaleMode.html), [Panel Settings properties reference](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Runtime-Panel-Settings.html)

- **Constant Pixel Size** (`ConstantPixelSize`) — «Elements stay the same size, in pixels, regardless of screen size.» Параметр — `Scale` (должен быть больше 0).
- **Constant Physical Size** (`ConstantPhysicalSize`) — «Elements stay the same physical size (displayed size) regardless of screen size and resolution.» Параметры — `Reference DPI` и `Fallback DPI`.
- **Scale With Screen Size** (`ScaleWithScreenSize`) — «Elements get bigger when the screen size increases, and smaller when it decreases.» Ключевые параметры — `Screen Match Mode` и `Reference Resolution`.

Для `Scale With Screen Size`:
- `Screen Match Mode`: `Match Width or Height` (с интерполяцией через `Match Value`, где 0 = по ширине, 1 = по высоте, 0.4 = 40% интерполяции), `Shrink` (обрезать канвас), `Expand` (увеличить канвас).
- `Reference Resolution` — «Set the resolution that this panel's UI is designed for.»

Прочие свойства `PanelSettings`: `Sort Order` («Set the order that the UI System draws panels»), `Target Texture` (для рендера UI на 3D-геометрию), `Theme Style Sheet`, `Text Settings`, `Target Display`.

Это по сути прямой аналог `Canvas` + `CanvasScaler` из uGUI: «The Panel Setting asset is the UI Toolkit's version of the Canvas and Canvas Scaler from the old UGUI system» (по данным поиска, не проверено дословно повторным WebFetch).

### Известная проблема: resolvedStyle игнорирует scaleMode

Зафиксированная в Unity Discussions жалоба: `visualElement.worldBound.height`, `visualElement.resolvedStyle.height` и `visualElement.layout.height` **игнорируют** масштабирование, заданное через `uiDocument.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize`; `uiDocument.panelSettings.scale` тоже не возвращает фактический масштаб в этой версии обсуждения. Предложенный обходной путь: [Get actual size of a VisualElement considering PanelSettings PanelScaleMode.ScaleWithScreenSize](https://discussions.unity.com/t/get-actual-size-of-a-visualelement-considering-panelsettings-panelscalemode-scalewithscreensize/906698)

```csharp
scale = Screen.height / uiDocument.rootVisualElement.resolvedStyle.height
```

В более поздних ответах в этой же теме отмечено, что Unity впоследствии добавил свойство `scale` прямо на объект панели, что снимает нужду в ручном пересчёте (версия, с которой это исправлено, в обсуждении не названа точно).

### Безопасная зона (вырез/чёлка на iPhone)

Готового штатного решения «безопасная зона + UI Toolkit» в самой документации Unity не найдено — есть только сторонние решения и статьи практиков. Ключевые технические моменты по `Screen.safeArea`:

- `Screen.safeArea` определяется относительно окна плеера (Player window), а не физического экрана устройства: если `PlayerSettings.Android.renderOutsideSafeArea` отключён, Unity сам подгоняет окно плеера под безопасную область устройства, и тогда `Screen.safeArea` фактически равен `Rect(0, 0, Screen.width, Screen.height)`, поскольку окно плеера уже не включает небезопасные зоны.
- **Гочта с системой координат**: начало координат `Screen.safeArea` — внизу слева экрана, а начало координат в UI Toolkit — вверху слева; при переносе координат безопасной зоны в панель UI Toolkit ось Y нужно инвертировать.
- Практический приём (по статьям практиков, официально не задокументирован Unity): вместо anchor-подхода uGUI, для UI Toolkit безопасную зону оборачивают в контейнер и задают ему `padding`, вычисленный из `Screen.safeArea`, используя `RuntimePanelUtils.ScreenToPanel(panel, screenPoint)` для перевода экранных координат в координаты панели.

Готовый сторонний пакет — [`artstorm/ui-toolkit-safe-area`](https://github.com/artstorm/ui-toolkit-safe-area): предоставляет кастомный контрол `SafeArea Container`, который нужно класть самым верхним элементом иерархии, чтобы он занимал весь экран. Возможности пакета (дословно по README):
- «The container margins and the safe area is collapsed by default» (при коллапсе margin и safe area берётся большее значение; при отключении — margin добавляется поверх safe area).
- Можно исключать отдельные рёбра (left/right/top/bottom) из расчёта безопасной зоны.
- «This option excludes the safe area values for all edges on tvOS.»
- Отдельный флаг форсирует опрос (polling) безопасной зоны для корректного обновления при повороте экрана на 180° (например, Landscape Left → Landscape Right) — это компенсация известного бага, когда обычное обновление безопасной зоны не срабатывает при быстром повороте на 180°.

Практический вывод для мобильной головоломки: safe area с UI Toolkit придётся считать вручную (через `Screen.safeArea` + `RuntimePanelUtils.ScreenToPanel` + инверсия Y) или подключать готовый сторонний контрол — штатного аналога uGUI-компонента `Safe Area` в UI Toolkit нет.

## 7. Производительность на мобильных устройствах

### Что вызывает перестроение раскладки (layout rebuild)

Официальное руководство по оптимизации фиксирует: пересчёт раскладки («layout rebuild / relayout») запускается изменениями размера, позиции или выравнивания элемента, например изменением размера панели или перемещением элементов; частые пересчёты раскладки дорого стоят. Рекомендация — использовать трансформации (`translate`/`rotate`/`scale`, см. раздел 3) для анимаций вместо прямого изменения позиционных свойств, потому что это не меняет раскладку соседей. [Optimizing performance](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html)

Пересчёт стилей (repaint / style resolution) запускается изменением классов или стилей — например, добавлением класса или сменой цвета. Рекомендация — не переключать классы для изменения стиля в больших иерархиях во время анимаций, а обновлять свойства напрямую (инлайново) в таких горячих путях.

### Батчинг и ограничение на 8 текстур

При превышении лимита в восемь текстур на батч система батчинга вынуждена разбивать отрисовку на отдельные батчи, что увеличивает накладные расходы; решение — использовать динамический атлас текстур или Sprite Atlas, чтобы объединить текстуры и сохранить эффективность батчинга.

### Динамический атлас (dynamic atlas)

UI Toolkit автоматически добавляет и удаляет текстуры из динамического атласа по мере того, как визуальные элементы на них ссылаются. Настройки атласа (Dynamic Atlas Settings) находятся в `PanelSettings`; там же есть фильтры, определяющие, какие текстуры попадают в атлас (например, фильтр по размеру — крупные текстуры не атласятся). [Control textures of the dynamic atlas](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-control-textures-of-the-dynamic-atlas.html)

Цена динамических атласов — фрагментация: «When textures are added or removed from the atlas, it can lead to fragmentation, creating small spaces where previous textures were, which are too small to reallocate to other textures.» Для сброса атласа в исходное состояние есть `RuntimePanelUtils.ResetDynamicAtlas()` — рекомендуется вызывать этот метод, когда одновременно удаляется или добавляется много визуальных элементов.

Для мобильных устройств с ограниченной памятью документация прямо советует уменьшать `Max Atlas Size` относительно значения по умолчанию — например, `2048` пикселей вместо `4096`.

### Прочие мобильные рекомендации из официального руководства по оптимизации

- Использовать прямоугольные маски на основе шейдеров вместо трафаретных (stencil-based) масок, чтобы избежать разрывов состояния рендера.
- Использовать `ListView` для прокручиваемого контента ради виртуализации (не создавать сразу все элементы списка — см. раздел 9).
- Устанавливать `DisplayStyle.None`, а не `opacity = 0`, чтобы полностью убрать элемент из рендеринга (а не просто сделать его прозрачным, но всё ещё занимающим ресурсы отрисовки).
- Применять usage hint `DynamicTransform` к анимируемым элементам.

### Реальные жалобы на производительность на слабых Android-устройствах

Зафиксированный в Unity Discussions случай с конкретными числами: на устройстве **Xiaomi Redmi Note 4 (Android 7)** — 60+ FPS без фонового стиля, 55 FPS при добавлении только цвета фона, 30 FPS при добавлении цвета фона и текстуры одновременно. Пользователь диагностировал причину как «extensive branching in the UnityUIE.cginc shader, particularly when selecting a texture» (избыточное ветвление во внутреннем шейдере UI Toolkit при выборе текстуры). [UIToolkit rendering is extremely slow on older Android devices](https://discussions.unity.com/t/uitoolkit-rendering-is-extremely-slow-on-older-android-devices/1561024)

Ответ инженера Unity (AlexandreT-unity), дословно: «UI Toolkit has been heavily optimized assuming that the UI bottleneck is on CPU... the consequence is that our shader is more expensive» — то есть архитектурно UI Toolkit жертвует стоимостью GPU-шейдера ради снижения нагрузки на CPU, и на слабых мобильных GPU это может быть невыгодным компромиссом. Также подтверждено, что в качестве «low-end device» для тестирования производительности Unity использует Mali-T720 MP2. Из предложенных решений — кастомные шейдеры для `VisualElement`, `ImmediateModeElement` с упрощённым шейдером, и появившийся в Unity 6.3 beta usage hint `LargePixelCoverage`. При этом сама версия 6.3 подняла минимальный уровень Android API до 25, что исключает часть старых устройств вроде тестового Redmi Note 4 из зоны применимости этого улучшения.

Другие зафиксированные жалобы на форуме Unity Discussions:
- Плохая производительность полноэкранного `ScrollView` примерно с 20 элементами (`VisualElement` с фоновым изображением) на Samsung Galaxy A3 2016.
- Просадки при инициализации приложения на мобильных: «We are currently using the UI Toolkit for a mobile project, and are running into some performance issues when the application initializes, as others have noted in other forum posts.»
- Отсутствие официальных бенчмарков от Unity: разработчики прямо спрашивали Unity про данные по энергопотреблению и загрузке CPU/GPU на мобильных, и такой конкретики от Unity в найденных материалах нет — только качественные объяснения архитектуры. [Performance of UI Toolkit](https://discussions.unity.com/t/performance-of-ui-toolkit/1563732)

### Стоимость runtime data binding

Отдельная задокументированная жалоба: пользователь с `ScrollView` на ~100 элементов (около 20 видимых одновременно) получил **664 привязки** суммарно, и это вызвало заметную просадку производительности независимо от режима привязки — «I tested all binding modes and update triggers with no results in performance». Причина — не само обновление значений, а проверка необходимости обновления: «It not updating them, but checking if they need to be updated and that's the issue here» (функция `ShouldUpdateBindings` съедала заметное время кадра даже без реальных изменений). Скрытые через `DisplayStyle.None` или `flex: none` элементы всё равно продолжают обрабатываться системой привязок — выключение отображения не отключает биндинги. [UI Toolkit Runtime Bindings performance](https://discussions.unity.com/t/ui-toolkit-runtime-bindings-performance/1593988)

Ответ разработчика Unity (martinpa_unity): «Runtime bindings... have an overhead compared to handcrafted code»; рекомендация — сокращать число одновременных привязок через `ListView` (в этом конкретном случае переход на `ListView` снизил число привязок с 664 до примерно 135). Среди заявленных будущих улучшений: возможность «отключать» привязку, когда `display` не `flex`, использование кодогенерации, чтобы пропускать ненужные обновления, и предварительный проход (pre-pass) по источникам данных.

**Практический вывод для головоломки:** если UI будет включать сетку ячеек/предметов с большим количеством одновременно активных элементов, привязки данных через `SetBinding`/`DataBinding` на каждый элемент могут оказаться дороже, чем ручное обновление значений в коде — особенно если элементы не используют виртуализацию `ListView`/`GridView`.

## 8. Подводные камни и жалобы разработчиков

### World Space UI: ограничения

Работа UI в мировых координатах (не оверлей на экран) появилась только с Unity 6.2+ (для 6.3 подтверждена та же страница мануала). Настройка: [World Space UI](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/world-space-ui.html), [Create a World Space UI](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/create-world-space-ui.html)

- Режим рендера `PanelSettings` переключается на `World Space» («Set the render mode of the Panel Settings asset to World Space to create a World Space UI»).
- `Pixels Per Unit` (по умолчанию 100) — сколько пикселей панели соответствует одной мировой единице.
- Настройка Panel Input Configuration — через `GameObject > UI Toolkit > Panel Input Configuration` либо кнопку в инспекторе, если `PanelSettings` уже в режиме World Space.
- Размер контейнера задаётся USS-свойствами `size`/`position`; в инспекторе `UIDocument` есть режим `World-Space Dimensions`: `Dynamic` (по содержимому) или `Fixed` (вручную), плюс выбор точки pivot (9 позиций) и `Pivot Reference Size` (`Bounding Box` — включает все элементы, либо `Layout` — по системе раскладки).
- **Явное ограничение из документации**: «Integration with 2D sorting layers isn't currently supported» — порядок сортировки корневых документов зависит от расстояния по Z до камеры, а не от 2D sorting layers, что важно для 2D-игры, где обычно вся сортировка идёт именно через sorting layers/order in layer.

Для мобильной 2D-головоломки это значит: если понадобится UI, прикреплённый к игровому объекту в мировых координатах (например, подсказка над предметом), его сортировка не впишется напрямую в существующие 2D sorting layers спрайтов — потребуется либо обходной путь по Z, либо использовать для таких элементов не World Space UI, а оверлей-панель с ручным пересчётом экранных координат объекта.

### Собранные из Unity Discussions прямые жалобы на runtime UI Toolkit

**Сложность системы классов.** Разработчик Tom163: «every little bit of UI has at least two, often three different classes that do essentially the same thing» — неочевидно, какой класс переопределять для нужного поведения. [UI Toolkit frustrations](https://discussions.unity.com/t/ui-toolkit-frustrations/1685389)

**Стили по умолчанию мешают.** Тот же автор: «My stylesheet is literally hundreds of lines by now that only remove the runtime default styling» — то есть значительная часть кастомного USS уходит не на свой дизайн, а на нейтрализацию стилей темы по умолчанию. Отдельно отмечена путаница с псевдоклассами: «Styling :hover is pointless, you need to style :hover:enabled in almost all use cases.»

**Бесполезный отладчик в игровом режиме.** «the debugger is mostly useless», потому что захват ввода игровым видом мешает поставить UI на паузу для инспекции.

**Слабый data binding для MVVM/MVC.** Другой участник (aberroarman): «I can't even bind button to an action» — то есть привязка не позволяет напрямую связать нажатие кнопки с методом, как это ожидается в полноценном MVVM; существующие сторонние MVVM-надстройки требуют, чтобы «both View and ViewModel classes... has to be inherited from a base class», что снижает применимость в реальных проектах.

**Хрупкость ссылок в UXML.** Ссылки на типы в UXML не обновляются автоматически при переносе типов между сборками/пространствами имён, в отличие от обычных ссылок в коде C#.

**Нестабильность рантайм-пересборки UI.** В отдельной теме — предупреждение «UI was recreated and no companion MonoBehaviour found, some UI functionality may have been lost» с минимумом диагностической информации; один из участников: «How can I debug this to actually see where the issue is coming from?» и далее «Can someone from Unity who built this crap stuff explain how to debug it?». Проблема проявлялась в конкретном проекте даже на новой пустой сцене с новым UI Document, а не только в старых сценах. Итоговая оценка автора темы: «I thought prefabs were the most sensitive things in Unity, but I guess we have a new champion UI Toolkit», и «I've already lost two days on this warning and can't move forward because of it.» [What's wrong with UI Toolkit?](https://discussions.unity.com/t/whats-wrong-with-ui-toolkit/1693143)

Эти жалобы (кроме прямо помеченных мобильными/производительностью в разделе 7) не специфичны исключительно для мобильных платформ, но напрямую касаются runtime-разработки, а не только редакторских инструментов.

## 9. Практические приёмы

### Список/сетка через ListView

`ListView` — «most commonly used list-based control in UI Toolkit», обеспечивает виртуализацию: инстанцируются и рендерятся только видимые элементы, что критично для производительности при больших наборах данных. [Create a list view runtime UI](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html), [ListView UXML element](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-uxml-element-ListView.html)

UXML-атрибуты `ListView` (дословно из мануала): `item-template` — «A UXML template that constructs each recycled and rebound element within the list»; `fixed-item-height` (float) — «The height of a single item in the list, in pixels»; `reorderable` (bool) — «Gets or sets a value that indicates whether the user can drag list items to reorder them»; `selection-type`; `virtualization-method` (`FixedHeight` или `DynamicHeight`).

Пример UXML:

```xml
<UXML xmlns="UnityEngine.UIElements">
    <ListView class="the-uxml-listview" fixed-item-height="20" />
</UXML>
```

Заполнение из C# через пару функций `makeItem`/`bindItem` (стандартный паттерн виртуализации — `makeItem` создаёт визуальные элементы «as needed when the ListView needs more items to render», `bindItem` связывает переиспользуемый (recycled) элемент с данными по индексу):

```csharp
Func<VisualElement> makeItem = () => new Label();
Action<VisualElement, int> bindItem = (e, i) => ((Label)e).text = items[i];

var listView = container.Q<ListView>();
listView.makeItem = makeItem;
listView.bindItem = bindItem;
listView.itemsSource = items;
```

Важная особенность архитектуры: визуальные элементы `ListView` — не `GameObject`, поэтому на них нельзя навесить `MonoBehaviour`-компонент напрямую; данные привязываются через `userData` элемента и отдельный класс-контроллер (`CharacterListEntryController` в официальном примере), у которого есть `SetVisualElement` для получения ссылки на элементы шаблона и `Set`-метод для обновления отображаемых данных при переиспользовании элемента.

Для сетки предметов головоломки (несколько колонок) официальной документацией отдельный `GridView`-контрол не описан в найденных источниках — практический путь: `ListView` с горизонтальной раскладкой строк через USS (`flex-direction: row`, `flex-wrap: wrap` на контейнере строки) поверх той же виртуализации, либо ручная реализация на основе `ScrollView` с собственным пулом элементов, если нужна двумерная виртуализация (не проверено официальным источником — экстраполяция из документированных Flexbox-свойств раздела 3).

### Смена внешнего вида из кода: классы вместо инлайновых стилей

Официальная рекомендация («Best practices for USS»): «Use USS files instead of inline styles when you can for more efficient memory usage» — инлайновые стили хранятся на каждом элементе отдельно и быстро увеличивают потребление памяти при масштабировании на много элементов. Кроме того, инлайновым стилем нельзя задать псевдоклассы (`:hover` и подобные). [Best practices for USS](https://docs.unity3d.com/Manual/UIE-USS-WritingStyleSheets.html)

Рекомендованный способ смены состояния элемента в коде — переключение класса, а не прямое присвоение `element.style.*`:

```csharp
element.RemoveFromClassList("common");
element.AddToClassList("legendary");
```

Классы рекомендуется добавлять в конструкторе кастомного элемента через `AddToClassList()`, включая классы для дочерних элементов, которые этот конструктор инстанцирует.

**Соглашение об именовании — BEM** (Block Element Modifier): блок — самостоятельная сущность (`menu`, `button`); элемент блока — через двойное подчёркивание (`menu__item`); модификатор — через двойной дефис (`menu--disabled`). Пример из мануала:

```xml
<VisualElement class="menu">
    <Label class="menu__item" text="Banana" />
    <Label class="menu__item menu__item--disabled" text="Orange" />
</VisualElement>
```

**Стоимость селекторов на рантайме.** Все USS-селекторы применяются во время выполнения, так что архитектура классов влияет на производительность инициализации; сложность оценивается примерно как N1 × N2, где N1 — число классов на элементе, N2 — число применимых USS-файлов. Обычно это не проблема, поскольку каждый USS-файл превращается в таблицу поиска, но отдельно выделен риск: «Avoid using `:hover` pseudo-class in selectors on elements with many descendants, such as `.yellow:hover > * > Button`», потому что движение мыши тогда инвалидирует всю связанную иерархию. Рекомендуется предпочитать дочерние селекторы (`>`) селекторам-потомкам, когда нужно частичное сопоставление.

Инлайновые стили в коде/UXML уместны для одноразовых или экспериментальных случаев; при необходимости их можно позже вынести в USS-класс.

## 10. UI Toolkit или uGUI для 2D-игры: доводы за и против

Прямых тредов на Reddit r/Unity3D с развёрнутым мнением найти не удалось (поиск по `site:reddit.com` и по ключевым фразам не дал релевантных результатов) — надёжных источников по Reddit конкретно не найдено. Ниже — доводы из официальной документации Unity, форума Unity Discussions и опубликованного разбора практиков (Angry Shark Studio).

### За UI Toolkit

- Официально позиционируется Unity как система, которая активно развивается и получает новые возможности каждый релиз, в отличие от «updated infrequently» uGUI/IMGUI. [Comparison of UI systems](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- Есть встроенная система data binding, USS-переходы, глобальное управление стилями, поддержка SVG и RTL — всего этого у uGUI нет вовсе.
- Архитектурно элементы не GameObject — по утверждению практиков это снижает нагрузку по сравнению с «every UI element creates a GameObject» в uGUI. Независимый (не от Unity) тест студии Angry Shark для Unity 2022.3.10f1 LTS показал в их сценарии: 9-кратное сокращение draw call'ов (5 против 45), в 3 раза быстрее CPU frame time (4.2 мс против 12.5 мс), в 2.6 раза меньше памяти (48 МБ против 125 МБ), «Smooth at 10,000+» элементов в скролле против «Stutters at 500+» у uGUI, в 5.7 раза быстрее инстанцирование 100 элементов (15 мс против 85 мс). **Важно:** это цифры одной сторонней студии в одном тестовом сценарии, не официальный бенчмарк Unity — годятся как ориентир, но не как гарантия для любого проекта. [Angry Shark Studio: UI Toolkit vs UGUI 2025](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)
- Автоматический батчинг и встроенная виртуализация списков (`ListView`) без сторонних плагинов.
- Раскладка через Flexbox лучше подходит для адаптивных под разные экраны интерфейсов, чем anchor-система uGUI, если верстается с нуля.

### Против UI Toolkit (доводы за uGUI)

- Сама Unity официально рекомендует uGUI как основной вариант именно для runtime, UI Toolkit — только как альтернативу. [Comparison of UI systems](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- Нет сериализуемых событий (UnityEvent в инспекторе) и интеграции с Animation Clips/Timeline — если в проекте уже завязана анимация UI на Timeline/Animator, это прямая потеря функциональности.
- Реальные жалобы на производительность именно на слабых/старых Android-устройствах — просадки FPS из-за дорогого «uber»-шейдера, подтверждённые инженером Unity как архитектурный компромисс (CPU дешевле, GPU дороже). [UIToolkit rendering is extremely slow on older Android devices](https://discussions.unity.com/t/uitoolkit-rendering-is-extremely-slow-on-older-android-devices/1561024)
- Runtime data binding имеет задокументированный на практике overhead при большом числе одновременных привязок (664 привязки → заметная просадка), который признал сам разработчик Unity. [UI Toolkit Runtime Bindings performance](https://discussions.unity.com/t/ui-toolkit-runtime-bindings-performance/1593988)
- Drag-and-drop, safe area, сложные жесты — везде нужно писать логику самому поверх низкоуровневых pointer-событий; готовых компонентов «из коробки» для этих сценариев UI Toolkit не даёт (в uGUI похожая ситуация с жестами/safe area, но drag-and-drop через `IDragHandler` чуть более стандартизован).
- World Space UI появился только в Unity 6.2+ и не интегрируется с 2D sorting layers — для 2D-игры, где вся сортировка спрайтов обычно построена на sorting layers, это существенное ограничение, если понадобится мировой (не оверлейный) UI.
- Разработчики жалуются на архитектурную избыточность классов, агрессивные стили по умолчанию (которые приходится «отключать» сотнями строк USS) и слабый отладчик в runtime. [UI Toolkit frustrations](https://discussions.unity.com/t/ui-toolkit-frustrations/1685389)
- В uGUI — «Full Animator support», «Thousands of Asset Store packages», визуальное редактирование прямо в Scene view; в UI Toolkit — «No Timeline support», «No mask component» (по крайней мере в стандартном виде, есть только шейдерные прямоугольные маски), «Limited shader effects», плюс отдельная кривая обучения (иная модель, чем GameObject-based uGUI). [Angry Shark Studio: UI Toolkit vs UGUI 2025](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)

### Вывод для проекта (без готового ответа за автора)

Единого консенсуса «что лучше для 2D-мобильной головоломки» в источниках нет — позиция самой Unity («основной вариант для runtime — uGUI») расходится с практическими тестами отдельных студий, где UI Toolkit выигрывает по метрикам. Для конкретно этого проекта (текстовый UXML/USS, который правит агент, вместо бинарных сцен) решающим фактором была не производительность, а удобство редактирования текстом — это указано в постановке задачи как исходная причина выбора UI Toolkit, а не вывод из данного исследования.

---

## Источники

- [Unity Manual: Comparison of UI systems in Unity (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UI-system-compare.html)
- [Unity Manual: Creating your first runtime UI / Create a list view runtime UI (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-HowTo-CreateRuntimeUI.html)
- [Unity Manual: Load UXML and USS in C# scripts](https://docs.unity3d.com/Manual/UIE-manage-asset-reference.html)
- [Unity Manual: Introduction to visual elements and the visual tree](https://docs.unity3d.com/Manual/UIE-VisualTree.html)
- [Unity Manual: Panel Renderer component (6000.6)](https://docs.unity3d.com/6000.6/Documentation/Manual/ui-systems/panel-renderer-component.html)
- [Unity Manual: Theme Style Sheet (TSS) (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-tss.html)
- [Unity Manual: Panel Settings properties reference (6000.0)](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Runtime-Panel-Settings.html)
- [Unity Scripting API: PanelScaleMode (2021.2)](https://docs.unity3d.com/2021.2/Documentation/ScriptReference/UIElements.PanelScaleMode.html)
- [Unity Scripting API: PanelSettings (6000.3)](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/UIElements.PanelSettings.html)
- [Unity Manual: USS data types (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-USS-PropertyTypes.html)
- [Unity Manual: USS properties reference](https://docs.unity3d.com/Manual/UIE-USS-Properties-Reference.html)
- [Unity Manual: Position element with the layout engine](https://docs.unity3d.com/Manual/UIE-LayoutEngine.html)
- [Unity Manual: USS transform (6000.4)](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-Transform.html)
- [Unity Manual: Best practices for USS](https://docs.unity3d.com/Manual/UIE-USS-WritingStyleSheets.html)
- [Unity Manual: Data binding (6000.1)](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-data-binding.html)
- [Unity Manual: Create a runtime binding in C# scripts (6000.1)](https://docs.unity3d.com/6000.1/Documentation/Manual/UIE-runtime-binding-types.html)
- [Unity Manual: Pointer events (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Pointer-Events.html)
- [Unity Manual: Click events (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Click-Events.html)
- [Unity Manual: Create a drag-and-drop UI inside a custom Editor window (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-create-drag-and-drop-ui.html)
- [Unity Manual: Runtime UI event system and input handling (6000.4)](https://docs.unity3d.com/6000.4/Documentation/Manual/UIE-Runtime-Event-System.html)
- [Unity Manual: FAQ for input and event systems with UI Toolkit](https://docs.unity3d.com/Manual/UIE-faq-event-and-input-system.html)
- [Unity Manual: World Space UI (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/world-space-ui.html)
- [Unity Manual: Create a World Space UI (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/create-world-space-ui.html)
- [Unity Manual: ListView UXML element (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-uxml-element-ListView.html)
- [Unity Manual: Control textures of the dynamic atlas (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-control-textures-of-the-dynamic-atlas.html)
- [Unity Manual: Optimizing performance — best practice guide (6000.4)](https://docs.unity3d.com/6000.4/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/optimizing-performance.html)
- [GitHub: artstorm/ui-toolkit-safe-area](https://github.com/artstorm/ui-toolkit-safe-area)
- [Unity Discussions: Get actual size of a VisualElement considering PanelSettings PanelScaleMode.ScaleWithScreenSize](https://discussions.unity.com/t/get-actual-size-of-a-visualelement-considering-panelsettings-panelscalemode-scalewithscreensize/906698)
- [Unity Discussions: UI Toolkit frustrations](https://discussions.unity.com/t/ui-toolkit-frustrations/1685389)
- [Unity Discussions: Performance of UI Toolkit](https://discussions.unity.com/t/performance-of-ui-toolkit/1563732)
- [Unity Discussions: UIToolkit rendering is extremely slow on older Android devices](https://discussions.unity.com/t/uitoolkit-rendering-is-extremely-slow-on-older-android-devices/1561024)
- [Unity Discussions: What's wrong with UI Toolkit?](https://discussions.unity.com/t/whats-wrong-with-ui-toolkit/1693143)
- [Unity Discussions: UI Toolkit Runtime Bindings performance](https://discussions.unity.com/t/ui-toolkit-runtime-bindings-performance/1593988)
- [Angry Shark Studio: Unity UI Toolkit vs UGUI: 2025 Developer Guide](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)

Не проверено / надёжных источников не найдено:
- Прямые треды Reddit r/Unity3D с развёрнутым мнением «за/против» UI Toolkit для мобильных 2D-игр — поиск не дал релевантных результатов.
- Официальная статья Unity «UI Toolkit at runtime: Get the breakdown» — обнаружена через поиск, но WebFetch по URL вернул HTTP 403 (доступ заблокирован), поэтому её содержимое в этот файл не включено как первоисточник; использованы только независимо процитированные фрагменты, где формулировки подтверждены другими открытыми страницами.
- Статья Medium «Unity UI Toolkit: Safe Area» (idimus) с полным кодом кастомного `SafeArea : VisualElement` — WebFetch вернул HTTP 403, содержимое не подтверждено напрямую; в разделе 6 использованы только формулировки, подтверждённые README пакета `artstorm/ui-toolkit-safe-area` и общими фактами про `Screen.safeArea`.
- Точное значение Unity-версии, с которой появился usage hint `LargePixelCoverage` и когда именно был исправлен баг медленного рендеринга на Redmi Note 4 — по данным треда это «Unity 6.3 beta» и последующие версии, но независимого подтверждения в release notes не проверялось.
