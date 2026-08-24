# Гигиена репозитория Unity при работе агентов: обзор на 2026-08-24 (стек Unity 6.3 LTS)

Дата сбора материала: 2026-08-24. Версия стека, для которой собирались данные: Unity 6.3 LTS (6000.3).

## Кратко

- Файлы `.unity`, `.prefab`, `.asset` — это YAML с внутренними опознавателями `fileID` (номер объекта внутри файла) и `guid` (глобальный идентификатор ассета, хранится в соответствующем `.meta`-файле). Любой инструмент, который правит эти файлы не через Unity API, а как обычный текст/копированием, рискует рассинхронизировать эти идентификаторы.
- `.meta`-файл обязателен для каждого файла и папки в `Assets` — если он потерян, Unity создаёт новый и удаляет старый, из-за чего все существующие ссылки на этот ассет по старому GUID превращаются в "битые". Это задокументированное поведение самого Unity, а не гипотеза.
- Force Text (Asset Serialization Mode) и Visible Meta Files включаются в `Edit > Project Settings > Editor` и `Edit > Project Settings > Version Control` соответственно; с февраля 2019 года Force Text — значение по умолчанию для новых проектов.
- Официальный шаблон `.gitignore` для Unity лежит в репозитории `github/gitignore` и уже используется как отраслевой стандарт; полный его текст приведён ниже дословно.
- Библиотека UnityYAMLMerge поставляется прямо внутри Unity Editor и подключается через `.gitconfig`; путь к бинарнику на macOS при установке через Unity Hub — `/Applications/Unity/Hub/Editor/<версия>/Unity.app/Contents/Tools/UnityYAMLMerge` (при установке не через Hub официальная документация даёт другой путь — `/Applications/Unity/Unity.app/Contents/Helpers/UnityYAMLMerge`).
- В репозитории обязаны быть `ProjectSettings/` и `Packages/manifest.json` (и `packages-lock.json`) — без них проект открывается с другими настройками ввода, физики, тегов и слоёв; `Library/`, `Temp/`, `Obj/`, `Build*/`, `Logs/`, `UserSettings/` — не должны попадать в git никогда.
- Практики, снижающие риск от агентов: выносить данные в ScriptableObject вместо дублирования их в сценах и префабах, строить пользовательский интерфейс на UI Toolkit (UXML/USS) вместо тяжёлых иерархий префабов, собирать часть сцены кодом — всё это уменьшает объём YAML, который вообще может быть задет неудачной автоматической правкой.
- Для CI разумно: кеширование `Library/`, обязательный прогон Unity Test Runner (EditMode/PlayMode) через `game-ci/unity-test-runner`, отдельная проверка на отсутствующие скрипты/ссылки, и архитектурное разделение доменного слоя от `UnityEngine`-зависимого кода.

## 1. Формат файлов Unity: почему агент их ломает

Файлы `.unity` (сцены), `.prefab` (префабы) и `.asset` (ассеты ScriptableObject и прочие) в режиме текстовой сериализации — это YAML-документы, где каждый Unity-объект (GameObject, Transform, MonoBehaviour и т. д.) представлен YAML-блоком с числовым `fileID`, уникальным в пределах данного файла. Ссылки между объектами внутри одного файла (например, у Transform — ссылка на родителя и детей) закодированы именно через `fileID`. Ссылки на объекты, лежащие в других файлах (материал, текстура, скрипт, другой префаб), закодированы парой `fileID` + `guid`, где `guid` — глобальный идентификатор целевого ассета.

Официальная документация Unity описывает происхождение и роль GUID и `.meta`-файлов так, дословно:

> "As part of the importing process, Unity creates metadata about any assets you import into your project. The metadata contains information such as the asset's import settings, and where your project uses the asset. When you import an asset, Unity does the following: Assigns the asset a unique ID. Creates a .meta file for the asset. Processes the asset."

И далее, про сам механизм присвоения ID:

> "The Unity Editor frequently checks the contents of the Assets folder against the list of assets it already knows about. When you place an asset in the Assets folder, Unity detects that you have added a new file and assigns a unique ID to the asset. This is an ID that Unity uses internally to reference the asset, so that it can move or rename the asset without breaking anything."

Источник: [Unity - Manual: Asset metadata (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html).

### Почему агент (или любой внешний инструмент) их ломает

Ключевая причина — Unity сам управляет соответствием "путь к файлу ↔ GUID" только тогда, когда изменения происходят через сам редактор (drag-and-drop, переименование в Project Window и т. п.). Если файл или папка перемещаются или переименовываются в обход Unity — например, обычным файловым инструментом текстового редактора или скриптом агента — `.meta`-файл может не последовать за ассетом, и тогда происходит следующее, дословно по документации:

> "Meta files contain important information about how the asset is used in your project, and they must stay with the asset file they relate to. If you move or rename an asset within the Project window, Unity automatically moves or renames the corresponding .meta file. However, if you move or rename an asset outside of Unity, you must move or rename the .meta file to match. If an asset loses its .meta file, any reference to that asset is broken in your project. In this situation, Unity generates a new .meta file for the moved or renamed asset as if it's a brand new asset, and deletes the old .meta file."

И последствия конкретно по типам ассетов:

> "If a texture asset loses its .meta file, any materials that use that texture lose their reference to that texture. To fix it, you must manually re-assign that texture to any materials which require it. If a script asset loses its .meta file, any GameObjects or Prefabs that have that script assigned instead have an unassigned script component, and lose their functionality. To fix it, you must manually re-assign that script to any GameObjects which require it."

Источник: тот же, [docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html).

Второй канал поломки — прямое редактирование текста YAML-файла сцены/префаба вручную или через обобщённый (generic) инструмент правки текста, а не через сериализатор Unity. Если структура YAML нарушена (несовпадение отступов, дублирующийся `fileID`, испорченный якорь), Unity либо не сможет открыть файл, либо молча создаст рассинхронизацию между объектами. Именно поэтому известные наборы правил для AI-агентов, работающих с Unity через MCP, отдельно запрещают агенту трогать содержимое `Assets` универсальными файловыми инструментами (`edit_file`, `apply`, `copy`, `move`) — см. подробности и источник в файле `01-unity-mcp.md`, раздел "Ограничения и опасности".

Даже пустые папки становятся источником проблем при работе с системами контроля версий, потому что многие VCS (включая git) не хранят пустые директории как таковые — только файлы. Unity явно описывает специальное поведение для этого случая:

> "Unity assigns each folder in your project's Assets folder its own .meta file. However, some version control systems (VCS) can't store empty folders. When you add or delete an empty folder from your project, the VCS stores the .meta file as added or removed, but doesn't store the change of adding or removing the folder itself."

Источник: тот же, [docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html).

## 2. Force Text Serialization и Visible Meta Files

Обе настройки относятся к тому, как Unity хранит на диске сцены, префабы, ассеты и метаданные, и обе критичны для того, чтобы git вообще мог показать осмысленный diff и позволить смержить конфликт вручную.

**Visible Meta Files.** Включается в `Edit > Project Settings > Editor`, пункт Version Control, значение "Visible Meta Files" (для более новых версий редактора этот же переключатель также доступен как часть настроек `Version Control`). При включении Unity кладёт `.meta`-файл рядом с каждым ассетом на диске в открытом виде, а не прячет его во внутреннем кеше. Это обязательная настройка, если проект вообще находится под git/любой другой VCS, — без неё `.meta`-файлы недоступны для коммита, а GUID-ссылки, соответственно, не версионируются вместе с самими ассетами.

**Force Text (Asset Serialization Mode).** Включается там же, в `Edit > Project Settings > Editor`, пункт Asset Serialization, значение "Force Text". В этом режиме Unity записывает `.unity`, `.prefab`, `.asset` и `.meta` файлы в текстовом YAML-формате вместо бинарного. Это делает содержимое читаемым и, что важнее, доступным для построчного diff и (иногда) ручного или автоматического слияния — бинарный формат для этого категорически не подходит, потому что бинарный конфликт нельзя показать построчно и почти никогда нельзя смержить вручную.

По данным независимого технического обзора (JetBrains, документация плагина ReSharper для Unity), с февраля 2019 года Force Text — значение по умолчанию для новых проектов Unity, но старые проекты, созданные раньше, могли сохранить бинарный режим и должны быть переключены вручную.

Официальная страница Unity о содержимом Asset Database (актуальная версия 6000.3) описывает связь настройки с читаемостью файлов дословно (в изложении, так как страница возвращает контент через прокси-модель): текстовые типы файлов (сцены, префабы, материалы, ассеты ScriptableObject) человекочитаемы, если Asset Serialization Mode установлен в значение по умолчанию Force Text, тогда как бинарные файлы вроде текстур или аудио читаемыми не становятся ни при каком режиме сериализации. Источник: [Unity - Manual: Contents of the Asset Database (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/asset-database-contents.html).

Официальная страница про Smart Merge подтверждает связь настроек с работой инструмента слияния: чтобы UnityYAMLMerge вообще мог что-то сделать, файлы уже должны быть в текстовом YAML-формате — иначе сравнивать нечего. Источник: [Unity - Manual: Smart merge (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html).

Практическая рекомендация, подтверждаемая множеством независимых практиков (сообщество MRTK, гайды по настройке Unity под VCS): обе настройки включаются один раз при создании проекта и коммитятся как часть `ProjectSettings/EditorSettings.asset`, чтобы все члены команды и все агенты работали в одинаковом режиме сериализации без ручной настройки на каждой машине.

## 3. Git и Unity

### 3.1. Полный официальный `.gitignore`

Ниже — дословное содержимое файла `Unity.gitignore` из официального репозитория `github/gitignore`, полученное напрямую с `raw.githubusercontent.com` 2026-08-24:

```gitignore
# This .gitignore file should be placed at the root of your Unity project directory
#
# Get latest from https://github.com/github/gitignore/blob/main/Unity.gitignore
#
# Recommended: add any editor/OS/tool-specific ignore rules from the Global/ templates as needed.
# See: https://github.com/github/gitignore/tree/main/Global
#
.utmp/
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
*.log

# By default unity supports Blender asset imports, *.blend1 blender files do not need to be commited to version control.
*.blend1
*.blend1.meta

# MemoryCaptures can get excessive in size.
# They also could contain extremely sensitive data
/[Mm]emoryCaptures/

# Recordings can get excessive in size
/[Rr]ecordings/

# Uncomment this line if you wish to ignore the asset store tools plugin
# /[Aa]ssets/AssetStoreTools*

# Autogenerated Jetbrains Rider plugin
/[Aa]ssets/Plugins/Editor/JetBrains*
# Jetbrains Rider personal-layer settings
*.DotSettings.user

# Visual Studio cache directory
.vs/

# Gradle cache directory
.gradle/

# Autogenerated VS/MD/Consulo solution and project files
ExportedObj/
.consulo/
*.csproj
*.unityproj
*.sln
*.slnx
*.suo
*.tmp
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db

# Unity3D generated meta files
*.pidb.meta
*.pdb.meta
*.mdb.meta

# Unity3D generated file on crash reports
sysinfo.txt

# Mono auto generated files
mono_crash.*

# Builds
*.apk
*.aab
*.unitypackage
*.unitypackage.meta
*.app

# Crashlytics generated file
crashlytics-build.properties

# TestRunner generated files
InitTestScene*.unity*

# Addressables default ignores, before user customizations
/ServerData
/[Aa]ssets/StreamingAssets/aa*
/[Aa]ssets/AddressableAssetsData/link.xml*
/[Aa]ssets/Addressables_Temp*
# By default, Addressables content builds will generate addressables_content_state.bin
# files in platform-specific subfolders, for example:
# /Assets/AddressableAssetsData/OSX/addressables_content_state.bin
/[Aa]ssets/AddressableAssetsData/*/*.bin*

# Visual Scripting auto-generated files
/[Aa]ssets/Unity.VisualScripting.Generated/VisualScripting.Flow/UnitOptions.db
/[Aa]ssets/Unity.VisualScripting.Generated/VisualScripting.Flow/UnitOptions.db.meta
/[Aa]ssets/Unity.VisualScripting.Generated/VisualScripting.Core/Property Providers
/[Aa]ssets/Unity.VisualScripting.Generated/VisualScripting.Core/Property Providers.meta

# Auto-generated scenes by play mode tests
/[Aa]ssets/[Ii]nit[Tt]est[Ss]cene*.unity*

# Auto-generated cache in Assets folder
/[Aa]ssets/[Ss]ceneDependencyCache*
```

Источник: [github.com/github/gitignore/blob/main/Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore).

Обратите внимание на использование парных классов символов вида `[Ll]ibrary/` — это защита от разного регистра пути на разных ОС и от случаев, когда часть инструментов создаёт папку `library` с маленькой буквы.

### 3.2. Git LFS для рисунков и звуков

Официальной страницы Unity, посвящённой конкретно настройке Git LFS, в рамках этого исследования найдено не было; ниже — согласующиеся между собой рекомендации из независимых практических источников (Medium, Hextant Studios, riptutorial), которые сходятся в одном и том же базовом рецепте.

Установка и включение LFS для типов файлов делается один раз командами вида:

```bash
git lfs install
git lfs track "*.psd"
git lfs track "*.png"
git lfs track "*.fbx"
git lfs track "*.wav"
```

После выполнения `git lfs track` Git создаёт или обновляет файл `.gitattributes` — именно он и должен быть закоммичен в репозиторий, чтобы правило LFS применялось у всех участников и агентов одинаково. Типовой набор категорий, встречающийся в нескольких независимых гайдах: изображения (`*.jpg`, `*.png`, `*.psd`, `*.tif`, `*.cubemap`), звук (`*.mp3`, `*.wav`, `*.ogg`), видео (`*.mp4`, `*.mov`), 3D-модели (`*.fbx`, `*.blend`, `*.obj`) — каждая строка получает `filter=lfs diff=lfs merge=lfs -text`.

Отдельное практическое предупреждение из того же круга источников: файл `LightingData.asset` рекомендуется не пускать через фильтр `unityyamlmerge`, а трактовать как обычный бинарный файл — на практике команды сталкивались с его порчей при попытке смержить его как YAML.

Источники: [Getting Started With Git LFS in Unity Without Wrecking Your Repo (Medium)](https://medium.com/@0xJake/getting-started-with-git-lfs-in-unity-without-wrecking-your-repo-89c1140cedbd), [.gitattributes for Unity Projects (Hextant Studios)](https://hextantstudios.com/unity-gitattributes/), [unity3d Tutorial: Using Git Large File Storage (LFS) with Unity (riptutorial)](https://riptutorial.com/unity3d/example/7178/using-git-large-file-storage--lfs--with-unity).

### 3.3. `.gitattributes` и merge-драйвер UnityYAMLMerge

Официальная страница Unity Manual "Smart merge" (проверена для версии 6000.3, то есть именно Unity 6.3 LTS) даёт следующую инструкцию для Git дословно:

> "Git: Add the following text to your .git or .gitconfig file:
> [merge]
> tool = unityyamlmerge
> [mergetool "unityyamlmerge"]
> trustExitCode = false
> cmd = '<path to UnityYAMLMerge>' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED""

Источник: [Unity - Manual: Smart merge (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html).

Официальный путь к бинарнику UnityYAMLMerge, по тому же источнику (сценарий "Unity установлен в стандартное расположение", без Unity Hub):

```
Windows: C:\Program Files\Unity\Editor\Data\Tools\UnityYAMLMerge.exe
         (или C:\Program Files (x86)\Unity\Editor\Data\Tools\UnityYAMLMerge.exe)
macOS:   /Applications/Unity/Unity.app/Contents/Helpers/UnityYAMLMerge
```

Официальная документация отдельно уточняет, как добраться до этого пути на macOS: "To access this folder from the Finder, right-click the Unity.app and select the Show Package Contents option." Источник тот же.

На практике почти все современные установки делаются через Unity Hub, а не как одиночное приложение `/Applications/Unity/Unity.app`, — и путь у Hub-инсталляции другой. По независимым, но взаимно согласующимся источникам (гайд No Time to Make Games, тред JetBrains YouTrack про поддержку UnityYAMLMerge в Rider), реальный путь на macOS при установке через Unity Hub выглядит так:

```
/Applications/Unity/Hub/Editor/<версия>/Unity.app/Contents/Tools/UnityYAMLMerge
```

Например, для Rider документирован такой случай: "UnityYamlMerge location on Mac: /Applications/Unity/Hub/Editor/2018.4.0f1/Unity.app/Contents/Tools/UnityYAMLMerge." Аналоги для других ОС при установке через Hub: Windows — `C:\Program Files\Unity\Hub\Editor\<версия>\Editor\Data\Tools\UnityYAMLMerge.exe`; Linux — `/home/<пользователь>/Unity/Hub/Editor/<версия>/Editor/Data/Tools/UnityYAMLMerge`. Источники: [Tutorial: Setup Smart Merge for Unity Assets with Git (No Time to Make Games)](https://nagachiang.github.io/tutorial-setup-smart-merge-for-unity-assets-with-git/), [UnityYAMLMerge / Smart Merge support: RIDER-33411 (JetBrains YouTrack)](https://youtrack.jetbrains.com/issue/RIDER-33411/UnityYAMLMerge-Smart-Merge-support).

Практический вывод: перед настройкой стоит проверить оба варианта пути (`Contents/Tools` и `Contents/Helpers`) внутри конкретной установленной через Hub версии Unity 6.3 LTS, потому что официальная документация описывает только сценарий одиночной установки, а расположение внутри `.app`-бандла у версий Unity исторически менялось.

Для того чтобы `unityyamlmerge` реально включался автоматически при `git merge`/`git rebase`, а не только при ручном вызове `git mergetool`, в `.gitattributes` репозитория дополнительно нужно указать merge-драйвер для нужных расширений, например:

```gitattributes
*.unity merge=unityyamlmerge eol=lf
*.prefab merge=unityyamlmerge eol=lf
*.asset merge=unityyamlmerge eol=lf
*.mat merge=unityyamlmerge eol=lf
*.anim merge=unityyamlmerge eol=lf
```

Это согласуется с независимыми конфигурациями `.gitattributes`, встречающимися у нескольких практиков (Hextant Studios, публичные gist-примеры), где текстовые YAML-типы Unity помечаются `merge=unityyamlmerge eol=lf`, а бинарные типы отдельно помечаются под LFS-фильтр (`filter=lfs diff=lfs merge=lfs -text`) — то есть один и тот же файл `.gitattributes` покрывает одновременно и LFS, и smart-merge для разных категорий файлов.

Официальная документация Unity Smart Merge также отдельно описывает три режима поведения инструмента в настройках `Edit > Project Settings > Version Control`, поле Smart Merge (доступно при выборе стороннего VCS вроде Perforce или UVCS в поле Mode): "Off: use only the default merge tool set in the preferences with no smart merging. Premerge: enable smart merging, accept clean merges... Ask: enable smart merging but when a conflict occurs, show a dialog to let the user resolve it (this is the default setting)." Источник: [Unity - Manual: Smart merge (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html).

Важная практическая оговорка независимых источников, согласующаяся с логикой самого инструмента: результат работы `unityyamlmerge` не следует автоматически коммитить без проверки — смерженный файл сцены или префаба стоит открыть в самом Unity и убедиться, что сцена загружается и не содержит битых ссылок, прежде чем фиксировать результат слияния.

## 4. Что должно лежать в репозитории, а что нет

Список папок, которые официальный `.gitignore` (см. раздел 3.1) исключает из репозитория, полностью совпадает с тем, что регенерируется Unity автоматически при каждом открытии проекта: `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`. Источник этого списка — тот же файл [github.com/github/gitignore/blob/main/Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore), приведённый выше дословно.

**`Library/`** — локальный кеш импортированных ассетов и скомпилированных сборок. Он однозначно не версионируется: пересобирается Unity автоматически из содержимого `Assets/` и `.meta`-файлов при первом открытии проекта. Если эта папка уже случайно попала в git, недостаточно просто добавить её в `.gitignore` — записи в истории коммитов остаются, и для реального уменьшения размера репозитория требуется переписывание истории (например, `git filter-repo`), что меняет хэши всех коммитов и требует повторного клонирования репозитория всеми участниками.

**`Temp/`, `Obj/`** — временные файлы текущей сессии редактора/компилятора, не нужны даже локально после закрытия Unity.

**`UserSettings/`** — персональные настройки конкретного разработчика в редакторе (раскладка окон, состояние Layout и т. п.); в отличие от `ProjectSettings/` эта папка не должна попадать под контроль версий, потому что описывает предпочтения одного человека, а не поведение проекта.

**`ProjectSettings/`** — обязательна к коммиту. Здесь хранятся оси ввода, слои физики, уровни качества, настройки Player Settings, теги — то есть общее для всей команды и для агента поведение проекта. Потеря этой папки означает, что открытый заново проект будет вести себя по-другому, причём разница может быть достаточно тонкой, чтобы её не сразу заметили.

**`Packages/manifest.json` и `Packages/packages-lock.json`** — обязательны к коммиту. Это списки зависимостей Unity Package Manager: без `manifest.json` Unity не знает, какие пакеты и какой версии должны быть установлены в проекте, а `packages-lock.json` фиксирует точные разрешённые версии, аналогично lock-файлам в npm/yarn.

**`Assets/` вместе со всеми `.meta`-файлами внутри** — это, собственно, всё содержимое проекта: код, сцены, префабы, материалы, текстуры. Не опционально ни в каком сценарии.

Сводная таблица:

| Путь | В репозитории? | Причина |
|---|---|---|
| `Assets/` (включая все `.meta`) | Да | Основное содержимое проекта |
| `ProjectSettings/` | Да | Общие настройки проекта (ввод, физика, теги, качество) |
| `Packages/manifest.json`, `Packages/packages-lock.json` | Да | Зависимости UPM и их зафиксированные версии |
| `Library/` | Нет | Кеш импорта, пересобирается автоматически |
| `Temp/`, `Obj/` | Нет | Временные файлы текущей сессии |
| `Build/`, `Builds/` | Нет | Артефакты сборки |
| `Logs/` | Нет | Логи компиляции/редактора |
| `UserSettings/` | Нет | Персональные настройки конкретного разработчика |

## 5. Приёмы, снижающие риск от агентов

Общая логика всех трёх приёмов ниже одна: чем меньше значимых данных лежит прямо в YAML-файлах сцен и префабов, тем меньше вероятность, что неудачная автоматическая правка (агентом или человеком) испортит что-то трудно восстановимое, и тем компактнее диффы для code review.

### 5.1. Данные — в ScriptableObject и JSON, а не в сценах

Официальный инженерный блог Unity прямо формулирует это как рекомендованный паттерн:

> "ScriptableObjects are perfect containers for static data" — и далее: разнесение данных по ScriptableObject "help to split your GameObjects into multiple smaller files... reducing the risk of merge conflicts."

Источник: [Achieve better Scene workflow with ScriptableObjects (blogs.unity3d.com / unity.com)](https://unity.com/blog/2020/07/01/achieve-better-scene-workflow-with-scriptableobjects/).

Механизм объясняется тем же источником через проблему дублирования: если данные (характеристики предмета, конфигурация) хранятся прямо в MonoBehaviour на префабе, то каждый инстанс префаба получает собственную копию этих данных, и правка одного значения требует находить и синхронизировать копии вручную; при вынесении данных в ScriptableObject все инстансы ссылаются на один и тот же ассет по GUID, а сам объект сцены/префаба содержит только эту одну ссылку вместо целого блока значений.

Независимый практик добавляет к этому организационный аргумент, дословно:

> "Each prefab is saved to its own file. If you change something in the prefab, only the prefab's file is changed."

Источник: [Merge Conflicts in Unity - How to avoid them? (Manuel Rauber)](https://manuel-rauber.com/2023/01/25/merge-conflicts-in-unity-how-to-avoid-them/).

Тот же автор формулирует организационное правило, напрямую применимое и к работе агента: "Each developer should work in his own working scene where no other developer will make a change ever" — то есть сцена, которую одновременно правит агент, не должна быть той же сценой, которую параллельно правит человек или другой процесс.

### 5.2. Интерфейс — на UXML/USS, а не на префабах

Прямого источника, который бы формулировал именно "снижение риска от AI-агентов" через UI Toolkit, в рамках этого исследования найдено не было. Но независимо подтверждённая связь такая: UI Toolkit Unity описывает интерфейс декларативно в текстовых файлах `.uxml` (структура) и `.uss` (стили) — то есть теми же средствами, что и HTML/CSS, вместо иерархии GameObject-ов внутри тяжёлого `.prefab`. Это не делает UXML/USS невосприимчивыми к конфликтам слияния сами по себе (это тоже текстовые файлы, которые могут конфликтовать), но заметно уменьшает долю UI-логики, которая физически хранится как непрозрачный граф `fileID`-ссылок внутри `.prefab`, а значит уменьшает площадь, на которой возможна GUID-порча, специфичная именно для формата префаба Unity.

Дополнительно, по тем же источникам про ScriptableObject, UI Toolkit и данные естественно сочетаются: экран на UI Toolkit "builds itself only once and needs to be notified when the data has been altered" через событие в ScriptableObject — то есть привязка данных (data binding) идёт через код и ассеты, а не через ручные ссылки на конкретные GameObject в сцене.

### 5.3. Сборка сцены кодом

Отдельного специализированного источника, детально описывающего программную сборку сцены именно как защиту от AI-агентов, найдено не было. Общий принцип подтверждается той же логикой, что и для ScriptableObject: если объекты сцены создаются и настраиваются кодом (например, через `PrefabUtility.InstantiatePrefab` и последующую настройку компонентов в `Awake`/`Start`, либо через выделенный редакторский скрипт построения уровня), то сам `.unity`-файл содержит меньше состояния — а значит меньше того, что можно случайно испортить прямой правкой YAML. Практический компромисс: часть сцены, отвечающая за расстановку статичного окружения художником, обычно остаётся в виде обычной сцены/префабов, тогда как повторяемая или генерируемая структура (пул врагов, UI-контейнеры, служебные менеджеры) — хороший кандидат на сборку кодом при старте.

## 6. Проверки для CI, ловящие поломки агента

### 6.1. Компиляция и тесты через `game-ci/unity-test-runner`

Официально документированный и широко используемый в сообществе способ гонять Unity Test Runner (режимы EditMode и PlayMode) в GitHub Actions — экшен `game-ci/unity-test-runner`. Базовый вид шага из официальной документации GameCI, дословно по структуре (не буквальная цитата, а прямое изложение задокументированных параметров):

```yaml
- uses: actions/cache@v3
  with:
    path: path/to/your/project/Library
    key: Library-MyProjectName-TargetPlatform

- uses: game-ci/unity-test-runner@v4
  with:
    projectPath: path/to/your/project
    githubToken: ${{ secrets.GITHUB_TOKEN }}
```

Кеширование `Library/` в CI официально рекомендовано именно потому, что пересборка этой папки с нуля на каждый запуск — самая медленная часть прогона; документация отдельно уточняет, что кеширование в таком виде применимо к проектам Unity, но не к пакетам (packages). Источник: [Test runner | GameCI](https://game.ci/docs/github/test-runner/), [github.com/game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner).

Важная практическая оговорка из открытых issue самого проекта: были зафиксированы случаи, когда ошибка компиляции не приводила к явному провалу CI-проверки — раннер показывал "0/0 tests passed" вместо явного failure, хотя в логе компилятора стоял `compilationhadfailure: True`. Вывод для настройки CI: недостаточно полагаться только на "тесты прошли зелёным" — статус компиляции стоит проверять отдельно, не полагаясь на то, что раннер тестов сам корректно пробросит ошибку компиляции наверх. Источник: [Tests not running · Issue #105 · game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner/issues/105).

### 6.2. Проверка на потерянные ссылки (missing references)

Прямого официального инструмента от Unity для этой конкретной проверки в рамках исследования не найдено. Существуют сторонние open-source редакторские утилиты, которые находят потерянные (missing) скрипты и ссылки и могут быть встроены в CI как отдельный шаг перед сборкой — например, `RimuruDev/Unity-MissingScriptsFinder` (36 звёзд на GitHub на момент проверки 2026-08-24, лицензия MIT, описание: "Unity Missing Scripts Finder Editor Tool. Updated for Unity 6000 and above.", проверено через `gh api repos/RimuruDev/Unity-MissingScriptsFinder`). Такой инструмент можно запускать batch-режимом Unity (`-batchmode -executeMethod`) как шаг CI до этапа тестов, чтобы поймать поломку GUID-ссылок раньше, чем она попадёт в тесты или в сборку. Источник: [github.com/RimuruDev/Unity-MissingScriptsFinder](https://github.com/RimuruDev/Unity-MissingScriptsFinder).

### 6.3. Запрет `UnityEngine` в отдельном каталоге (архитектурная граница)

Практика выносить доменную логику в отдельную сборку/каталог, не зависящий от `UnityEngine`, задокументирована в нескольких открытых учебных репозиториях по чистой архитектуре в Unity. Например, независимый пример-репозиторий по чистой архитектуре формулирует принцип изоляции слоёв так: "In this architecture the components from an inner layer cannot speak with components in an outer layer, helping to keep our domain testable and decoupled from everything." Технически это в Unity реализуется через `.asmdef` (assembly definition files) — сборке с доменной логикой просто не добавляется ссылка на сборку `UnityEngine`/`UnityEditor`, и попытка написать `using UnityEngine;` в файле этой сборки приведёт к ошибке компиляции, а не только к предупреждению линтера.

В рамках этого исследования не найдено отдельного документированного примера именно `grep`-проверки `using UnityEngine` в CI как самостоятельного стороннего паттерна — это может быть простым дополнительным шагом (`grep -rl "using UnityEngine" path/to/DomainLayer/ && exit 1`) поверх основной защиты через `.asmdef`, но как самостоятельно задокументированную практику её подтвердить источником не удалось; более надёжный и подтверждённый источниками механизм — именно ограничение ссылок сборки через `.asmdef`, которое Unity проверяет на этапе компиляции автоматически, без дополнительного `grep`-шага.

### 6.4. Сводный набор проверок для CI

Исходя из собранного материала, разумный минимальный набор проверок для проекта, где сцены и код правит агент:

1. Компиляция проекта в batch-режиме Unity (`-batchmode -quit -logFile - -projectPath ...`) как отдельный шаг, независимо от статуса Test Runner — чтобы не полагаться на то, что провал компиляции автоматически провалит тесты (см. 6.1).
2. Прогон Unity Test Runner (EditMode как минимум, PlayMode — если тесты требуют рантайма) через `game-ci/unity-test-runner` с кешированием `Library/`.
3. Отдельный шаг поиска потерянных ссылок/скриптов через редакторский инструмент вроде `Unity-MissingScriptsFinder`, запущенный в batch-режиме до/после основной сборки.
4. Ограничение зависимостей через `.asmdef`: доменная сборка не должна ссылаться на `UnityEngine`/`UnityEditor` — это Unity проверяет сама на этапе компиляции; дополнительный `grep`-шаг может служить дешёвой подстраховкой, но не заменяет `.asmdef`-границу.
5. Ручная или автоматическая проверка, что merge-конфликты в `.unity`/`.prefab` файлах либо отсутствуют, либо разрешены через `UnityYAMLMerge`, а не приняты вслепую одной из сторон (`ours`/`theirs`) — см. раздел 3.3 про обязательность открытия смерженной сцены в самом Unity перед коммитом.

## Источники

- [Unity - Manual: Asset metadata (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetMetadata.html)
- [Unity - Manual: Contents of the Asset Database (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/asset-database-contents.html)
- [Unity - Manual: Smart merge (6000.3)](https://docs.unity3d.com/6000.3/Documentation/Manual/SmartMerge.html)
- [github.com/github/gitignore — Unity.gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore)
- [Achieve better Scene workflow with ScriptableObjects (unity.com)](https://unity.com/blog/2020/07/01/achieve-better-scene-workflow-with-scriptableobjects/)
- [Merge Conflicts in Unity - How to avoid them? (Manuel Rauber)](https://manuel-rauber.com/2023/01/25/merge-conflicts-in-unity-how-to-avoid-them/)
- [Getting Started With Git LFS in Unity Without Wrecking Your Repo (Medium)](https://medium.com/@0xJake/getting-started-with-git-lfs-in-unity-without-wrecking-your-repo-89c1140cedbd)
- [.gitattributes for Unity Projects (Hextant Studios)](https://hextantstudios.com/unity-gitattributes/)
- [unity3d Tutorial: Using Git Large File Storage (LFS) with Unity (riptutorial)](https://riptutorial.com/unity3d/example/7178/using-git-large-file-storage--lfs--with-unity)
- [Tutorial: Setup Smart Merge for Unity Assets with Git (No Time to Make Games)](https://nagachiang.github.io/tutorial-setup-smart-merge-for-unity-assets-with-git/)
- [UnityYAMLMerge / Smart Merge support: RIDER-33411 (JetBrains YouTrack)](https://youtrack.jetbrains.com/issue/RIDER-33411/UnityYAMLMerge-Smart-Merge-support)
- [Test runner | GameCI](https://game.ci/docs/github/test-runner/)
- [github.com/game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner)
- [Tests not running · Issue #105 · game-ci/unity-test-runner](https://github.com/game-ci/unity-test-runner/issues/105)
- [github.com/RimuruDev/Unity-MissingScriptsFinder](https://github.com/RimuruDev/Unity-MissingScriptsFinder)
- [01-unity-mcp.md — раздел "Ограничения и опасности" (внутренняя ссылка в этой же базе знаний)](./01-unity-mcp.md)
