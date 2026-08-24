# Определение окраса кота на устройстве iOS без облака — 2026-08-24

Вопрос: можно ли распознать окрас, узор, длину шерсти, цвет глаз и белые отметины кота силами самого устройства iOS — без облачной модели со зрением и без узла-посредника.

## Кратко

1. В таксономии `VNClassifyImageRequest` / `ClassifyImageRequest` подтверждено **1303 категории** (сверено по трём независимым выгрузкам, выложенным разработчиками на GitHub). Пород и окрасов кошки там нет вообще — только пять общих слов: `cat`, `adult_cat`, `kitten`, `bobcat`, `feline`. Ни `tabby`, ни `calico`, ни `siamese`, ни `persian`, ни `tortoiseshell` не встречаются ни разу. При этом собачьих пород в том же списке — свыше тридцати (`beagle`, `corgi`, `dachshund`, `pomeranian` и так далее). Слово `tuxedo` в списке есть, но это предмет одежды (смокинг), а не окрас кошки «в смокинге» — рядом по алфавиту с ним стоят `turtle`, `typewriter`, а не другие обозначения кошек.
2. `hasMinimumPrecision(_:forRecall:)` и `hasMinimumRecall(_:forPrecision:)` — официальный, задокументированный Apple способ фильтровать выдачу классификатора под нужный баланс полноты и точности. Рабочий пример есть прямо в официальной документации.
3. `VNDetectAnimalBodyPoseRequest` (и новый `AnimalBodyPoseRequest` без приставки VN) официально распознаёт именно кошек и собак — это подтверждено стенограммой доклада WWDC23-10045 — и даёт 25 точек тела: уши, глаза, нос, шею, локти/колени/лапы (по отдельности для передних и задних, левых и правых), три точки хвоста. Отдельной точки «грудь» в перечне нет.
4. Основной цвет шерсти реально получить своими силами через Core Image (`CIAreaAverage`, `CIKMeans`) — это рабочий и бесплатный, но не идеально надёжный при плохом освещении и пестрой шерсти способ.
5. Узор (полосатый против однотонного) — это не вопрос API Apple, а вопрос анализа текстуры общими средствами обработки изображений. Такие признаки существуют (дисперсия яркости, спектр Фурье, локальные бинарные шаблоны), но их надёжность именно на фотографиях кошачьей шерсти никем при подготовке этого разбора не измерялась — подтверждённых цифр точности нет.
6. Готовой открытой модели Core ML именно под окрас или узор кошки не найдено — ни на официальной странице Apple ML models, ни в крупном кураторском списке Awesome-CoreML-Models. На GitHub есть модель пород (37 классов, судя по числу — вероятно Oxford-IIIT Pet Dataset) весом около 98 МБ, но без файла лицензии — использовать её в коммерческом продукте юридически рискованно. Есть общая модель zero-shot классификации SigLIP (лицензия Apache-2.0, суммарно около 386 МБ на два файла), но её точность именно на тонких различиях окраса кошек не проверялась.
7. Цвет глаз получить можно только очень приблизительно: Vision даёт точку `leftEye`/`rightEye` из позы тела животного, но не выделяет радужку или зрачок. Отдельного API для поиска глаз животных (аналога `VNDetectFaceLandmarksRequest` для человека) в Vision не существует.
8. Из пяти нужных черт устойчиво и бесплатно на устройстве получается разве что длина шерсти (косвенно, через собственную эвристику) и приблизительный основной цвет. Узор, точный цвет глаз и точные белые отметины — в лучшем случае грубое приближение собственными эвристиками, не готовое решение от Apple.

## 1. Классификация изображений Vision: есть ли окрасы и породы кошек в таксономии

### Как получить список категорий

Старый Objective-C-совместимый API — `VNClassifyImageRequest`. Он даёт статический метод, возвращающий все классификации для конкретной ревизии алгоритма:

```swift
import Vision

// VNClassifyImageRequestRevision1 — единственная существующая ревизия на сегодня.
let allClassifications = try VNClassifyImageRequest
    .knownClassifications(forRevision: VNClassifyImageRequestRevision1)

for observation in allClassifications {
    print(observation.identifier)
}
print("Итого категорий: \(allClassifications.count)")
```

Сигнатура подтверждена по официальной странице документации:

```
class func knownClassifications(forRevision requestRevision: Int) throws -> [VNClassificationObservation]
```

Новый Swift-вариант — `ClassifyImageRequest` (доступен с iOS 18). У него список категорий отдаётся через свойство экземпляра:

```swift
import Vision

let request = ClassifyImageRequest()
let identifiers = request.supportedIdentifiers   // [String], var supportedIdentifiers: [String] { get }
print(identifiers.count)
```

Сама документация Apple **не публикует** содержимое этого списка текстом — ни на странице `ClassifyImageRequest`, ни на странице `knownClassifications(forRevision:)`. Список можно получить только вызовом метода на устройстве. Это подтверждено содержимым обеих официальных страниц (получены через служебный JSON-адрес `developer.apple.com/tutorials/data/...`): текст ограничивается фразой «Requests the collection of classifications that the Vision framework recognizes» — без перечисления.

### Опубликованный кем-то полный список

Три независимых человека выгрузили и опубликовали результат вызова `knownClassifications(forRevision:)` в виде гист-файлов на GitHub:

- `ktustanowski/56c0d7541813868fed4aceb60ab5d149` — «Contains a list of supported identifiers for VNClassifyImageRequest (VNClassifyImageRequestRevision1)», 1303 строки.
- `ozgurshn/0e19568b3f930c58491ddbbe7dbb9170` — «VNClassifyImageRequest supportedIdentifiers», тот же набор в виде JSON-массива.
- `mikeparisstuff/94a31c29e2bc1e84faea39429bb3879f` — «VNClassifyImageRequest_supportedIdentifiers_dec_26_2023.csv», 1302 строки данных (без учёта возможной строки-заголовка).

Все три файла были загружены и сверены построчно командой grep. Число категорий во всех трёх сходится на **1303** (revision 1). Кошачьи записи во всех трёх списках идентичны и исчерпываются пятью позициями:

```
adult_cat
bobcat
cat
feline
kitten
```

Ни `tabby`, ни `calico`, ни `siamese`, ни `persian`, ни `tortoiseshell`, ни `maine_coon`, ни `ragdoll`, ни `sphynx`, ни `bengal`, ни `abyssinian` — не найдены ни разу ни в одном из трёх файлов (проверено точным поиском подстроки, включая проверку на ложное совпадение — `tabbouleh`, ближневосточное блюдо, в списке есть, но это не «tabby»). Для контраста, собачьих пород в том же списке — множество: `australian_shepherd`, `basenji`, `beagle`, `basset`, `bichon`, `bulldog`, `chihuahua`, `collie`, `corgi`, `dachshund`, `dalmatian`, `doberman`, `german_shepherd`, `greyhound`, `husky`, `jack_russell_terrier`, `malamute`, `malinois`, `mastiff`, `newfoundland`, `pitbull`, `pomeranian`, `poodle`, `pug`, `retriever`, `ridgeback`, `rottweiler`, `saint_bernard`, `schnauzer`, `setter`, `sheepdog`, `spaniel`, `terrier`, `vizsla`, `weimaraner`, `irish_wolfhound`, `bernese_mountain`, `hound` — свыше тридцати позиций.

Слово `tuxedo` в списке присутствует (проверено), но по соседству в алфавитном порядке с ним стоят `turmeric`, `turntable`, `turtle`, `typewriter` — весь блок относится либо к кухонной утвари, либо к предметам одежды/техники; ближайшая по смыслу категория одежды — `bowtie`, `gown`, `kilt`, `poncho`, `suit` — тоже в списке. Нет оснований считать, что `tuxedo` в этой таксономии означает окрас кошки «в смокинге»; для этого пункта — «не подтверждено», а по совокупности контекста — «вероятно, обычный смокинг как предмет одежды».

**Отдельно про смежный запрос `VNRecognizeAnimalsRequest`** (не тот, о котором спрашивалось, но легко спутать): это отдельный, более старый запрос, который распознаёт не 1303 категории, а ровно два вида животных. Официальная документация прямо отсылает к методу `knownAnimalIdentifiers(forRevision:)` для получения списка, а независимо опубликованный разбор (статья на Medium с примером кода и ссылкой на репозиторий) показывает, что для revision 1 этот список — `["Cat", "Dog"]`. Это подтверждает, что где бы Apple ни давала животным собственную категорию — она останавливается на уровне вида, а не окраса или породы.

## 2. Как фильтровать выдачу классификатора: hasMinimumPrecision / hasMinimumRecall

Оба метода объявлены на `VNClassificationObservation` (и на новом `ClassificationObservation`):

```
func hasMinimumPrecision(_ minimumPrecision: Float, forRecall recall: Float) -> Bool
func hasMinimumRecall(_ minimumRecall: Float, forPrecision precision: Float) -> Bool
```

Официальное определение (страница `hasPrecisionRecallCurve`, раздел Discussion):

> Precision refers to the percentage of your classification results that are relevant, while recall refers to the percentage of total relevant results correctly classified.

То есть точность (precision) — какая доля выданных меток действительно верна, а полнота (recall) — какая доля реально присутствующих меток вообще была выдана. Оба метода работают только тогда, когда `hasPrecisionRecallCurve == true` — если `false`, результат не будет содержательным (это отдельно оговорено в документации).

Официальный образец кода Apple (страница-статья «Analyze and label images using a Vision classification request») показывает именно тот сценарий, который нужен для игры — фильтрацию по порогу с явным выбором стратегии:

```swift
// Vision request to classify an image.
let request = ClassifyImageRequest()

// Perform the request on the image, and return an array of ClassificationObservation objects.
let results = try await request.perform(on: url)
    // Высокая полнота: пропускаем больше вариантов, но выше риск ложных срабатываний.
    .filter { $0.hasMinimumPrecision(0.1, forRecall: 0.8) }
    // Высокая точность: меньше вариантов, но они надёжнее.
    // .filter { $0.hasMinimumRecall(0.01, forPrecision: 0.9) }
```

Пояснение из той же статьи Apple: «A high-recall filter provides a much broader range of observations, but can result in more false positive results... If an app can't tolerate false positive results, the hasMinimumRecall method allows for a high-precision filter... Increasing precision decreases recall, and increasing recall decreases precision. Testing can help determine the balance point.»

Иными словами, Apple прямо советует не полагаться на один универсальный порог `confidence > X`, а выбирать между двумя методами исходя из того, что дороже — пропустить верный вариант или принять неверный, — и подбирать конкретные числа тестированием на своих данных. Для задачи «определить окрас кота» это не критично само по себе, поскольку категорий окраса в таксономии нет (см. раздел 1) — но метод пригодится, если решите распознавать хотя бы факт «это кошка» через `cat` / `feline` / `kitten` и не путать его с шумом.

## 3. Точки тела животного: VNDetectAnimalBodyPoseRequest

Официальная документация (страницы `VNDetectAnimalBodyPoseRequest` и нового `DetectAnimalBodyPoseRequest`, доступного с iOS 18) не указывает виды животных прямо в тексте страницы запроса. Но стенограмма доклада WWDC23, сессия 10045, которая посвящена именно этому запросу, говорит прямо:

> «The request supports cats and dogs, and detects 25 animal body landmarks that includes the tail and the ears.»

Число 25 сходится с официальным перечнем точек, который даёт страница `VNAnimalBodyPoseObservation.JointName` (получена через служебный JSON-адрес документации):

- **Голова (10 точек):** `leftEarTop`, `leftEarMiddle`, `leftEarBottom`, `leftEye`, `neck`, `nose`, `rightEye`, `rightEarTop`, `rightEarMiddle`, `rightEarBottom`.
- **Ноги (12 точек):** `leftBackElbow`, `leftFrontElbow`, `rightFrontElbow`, `rightBackElbow`, `leftBackKnee`, `leftFrontKnee`, `rightBackKnee`, `rightFrontKnee`, `leftBackPaw`, `leftFrontPaw`, `rightBackPaw`, `rightFrontPaw`.
- **Хвост (3 точки):** `tailTop`, `tailMiddle`, `tailBottom`.

10 + 12 + 3 = 25 — совпадает с числом из доклада, что подтверждает список независимо от стенограммы.

Отдельно есть перечисление групп точек — `VNAnimalBodyPoseObservation.JointsGroupName`: `all`, `forelegs`, `head`, `hindlegs`, `tail`, `trunk`. Важное наблюдение: группа `trunk` («туловище») в перечислении есть, а отдельной именованной точки груди или боков — нет ни среди точек головы, ни среди точек ног или хвоста. То есть **прямой точки «грудь» Vision не даёт**.

Практический вывод для белых отметин:

- Лапы: точки `leftFrontPaw` / `rightFrontPaw` / `leftBackPaw` / `rightBackPaw` дают точные координаты, где на снимке находится лапа — можно вырезать небольшую область вокруг каждой точки и оценить, белая она или нет (см. раздел 4 про определение цвета).
- Морда: точка `nose` плюс `leftEye`/`rightEye` дают достаточно, чтобы вырезать область морды.
- Грудь: специальной точки нет. Ближайшее приближение — область между точкой `neck` и точками передних лап (`leftFrontElbow`/`rightFrontElbow`), то есть придётся достраивать область самостоятельно, а не брать готовую точку. Это менее надёжно, чем для лап и морды, и полагаться на такую эвристику как на точный сигнал не стоит.

Пример кода (актуальный Swift API, iOS 18+):

```swift
import Vision

let request = DetectAnimalBodyPoseRequest()
let observations = try await request.perform(on: image)

if let animal = observations.first {
    let points = try animal.recognizedPoints(.all)
    if let frontLeftPaw = points[.leftFrontPaw], frontLeftPaw.confidence > 0.3 {
        let location = frontLeftPaw.location // нормализованные координаты (0...1)
        // Дальше: перевести в пиксели снимка и вырезать область вокруг точки
        // для оценки цвета (раздел 4).
    }
}
```

Для более старого, но по-прежнему поддерживаемого API (iOS 17+) — `VNDetectAnimalBodyPoseRequest` с `VNImageRequestHandler`, структура точек та же (`VNAnimalBodyPoseObservation.JointName`).

## 4. Определение основного цвета своими силами

### CIAreaAverage — самый простой способ

`CIAreaAverage` подтверждён как реальный протокол Core Image (наследник `CIAreaReductionFilter`, то есть в семье фильтров, сводящих область изображения к одному значению). Он возвращает изображение размером 1×1 пиксель со средним цветом заданной области — этого достаточно для грубой оценки основного цвета, если область вырезана заранее (например, по точкам туловища из раздела 3, за вычетом лап и морды, чтобы не портить результат белыми отметинами):

```swift
import CoreImage

func averageColor(of image: CIImage, in extent: CGRect) -> (r: UInt8, g: UInt8, b: UInt8)? {
    guard let filter = CIFilter(name: "CIAreaAverage") else { return nil }
    filter.setValue(image, forKey: kCIInputImageKey)
    filter.setValue(CIVector(cgRect: extent), forKey: kCIInputExtentKey)
    guard let outputImage = filter.outputImage else { return nil }

    var pixel = [UInt8](repeating: 0, count: 4)
    let context = CIContext(options: [.workingColorSpace: NSNull()])
    context.render(outputImage,
                    toBitmap: &pixel,
                    rowBytes: 4,
                    bounds: CGRect(x: 0, y: 0, width: 1, height: 1),
                    format: .RGBA8,
                    colorSpace: nil)
    return (pixel[0], pixel[1], pixel[2])
}
```

Недостаток простого среднего: если в области попали и шерсть, и фон, и тень, результат «размажется» в грязно-серый цвет. Для более честного результата стоит сперва обрезать снимок по контуру животного (Vision даёт `VNGeneratePersonSegmentationRequest` для людей, но для животных сегментации от Apple нет — придётся либо использовать `boundingBox` из `VNRecognizeAnimalsRequest`/`VNDetectAnimalBodyPoseRequest` как грубое приближение, либо писать собственную сегментацию).

### CIKMeans — выделение нескольких доминирующих цветов

`CIKMeans` — тоже подтверждённый реальный протокол Core Image, со свойствами `count: Int` (сколько кластеров/цветов искать), `passes: Float` (число итераций), `perceptual: Bool` (считать в перцептивном цветовом пространстве) и `inputMeans: CIImage?` (начальные центры кластеров). Он делает ровно k-средних по цвету области и возвращает изображение с рядом пикселей-кластеров:

```swift
import CoreImage

func dominantColors(of image: CIImage, in extent: CGRect, count: Int = 3) -> [CIColor] {
    guard let filter = CIFilter(name: "CIKMeans") else { return [] }
    filter.setValue(image, forKey: kCIInputImageKey)
    filter.setValue(CIVector(cgRect: extent), forKey: kCIInputExtentKey)
    filter.setValue(count, forKey: "inputCount")
    filter.setValue(Float(10), forKey: "inputPasses")
    filter.setValue(true, forKey: "inputPerceptual")
    guard let outputImage = filter.outputImage else { return [] }

    let context = CIContext()
    var pixels = [UInt8](repeating: 0, count: count * 4)
    context.render(outputImage,
                    toBitmap: &pixels,
                    rowBytes: count * 4,
                    bounds: CGRect(x: 0, y: 0, width: count, height: 1),
                    format: .RGBA8,
                    colorSpace: nil)

    return (0..<count).map { i in
        CIColor(red: CGFloat(pixels[i*4]) / 255,
                green: CGFloat(pixels[i*4+1]) / 255,
                blue: CGFloat(pixels[i*4+2]) / 255)
    }
}
```

k-средних выгоднее среднего тем, что при двухцветной шерсти (например, biколор) он вернёт отдельно «основной» и «отметины», а не их смесь. Ключевые имена свойств (`count`, `inputMeans`, `passes`, `perceptual`) подтверждены по официальной документации; конкретные строковые ключи для `setValue(forKey:)` (`inputCount`, `inputPasses`, `inputPerceptual`) соответствуют общему соглашению именования Core Image (`input` + название свойства с заглавной буквы) и не были сверены по официальному перечню ключей фильтра — перед использованием в продакшене стоит вывести `filter.inputKeys` на реальном устройстве и свериться.

`vImage` (Accelerate) тоже умеет гистограммы и статистику по пикселям, но точную сигнатуру нужной функции для этой задачи в рамках этого разбора подтвердить не удалось — при выборе между Core Image и vImage для дебютной реализации разумнее взять Core Image: он проще в связке с уже используемым Vision/CIImage конвейером.

### Сопоставление цвета с палитрой из шести окрасов

Простой и практичный способ — перевести полученный RGB-цвет в HSV/HSB и сравнивать по оттенку (Hue) и яркости (Value), а не по «сырому» RGB, потому что HSV устойчивее к изменению освещения:

```swift
enum CoatColor: String, CaseIterable {
    case ginger, grey, black, white, cream, brown
}

func classifyCoatColor(r: CGFloat, g: CGFloat, b: CGFloat) -> CoatColor {
    let color = UIColor(red: r, green: g, blue: b, alpha: 1)
    var hue: CGFloat = 0, sat: CGFloat = 0, brightness: CGFloat = 0, alpha: CGFloat = 0
    color.getHue(&hue, saturation: &sat, brightness: &brightness, alpha: &alpha)
    let hueDegrees = hue * 360

    if brightness > 0.85 && sat < 0.15 { return .white }
    if brightness < 0.25 { return .black }
    if sat < 0.2 { return .grey }
    if hueDegrees < 30 || hueDegrees > 340 {
        return brightness > 0.6 ? .cream : .brown
    }
    if hueDegrees < 45 { return .ginger }
    return .brown
}
```

Границы (`0.85`, `0.25`, `30°` и так далее) в этом примере — отправная точка, а не проверенные константы: их придётся подбирать на реальных фотографиях кошек нужных окрасов, потому что «рыжий» и «кремовый», «серый» и «коричневый» перекрываются в пространстве HSV сильнее, чем кажется на глаз.

## 5. Определение узора (полосатый / однотонный) своими силами

Здесь у Apple нет готового API — вопрос целиком сводится к общей теории анализа текстур, взятой не из документации Vision, а из общепринятой практики обработки изображений. Ниже — признаки, которые действительно применяются для различения текстур в целом (не именно кошачьей шерсти — специализированных публикаций по различению tabby/solid на iOS найти не удалось):

- **Разброс (дисперсия) яркости по области.** Самый дешёвый признак: у однотонной шерсти локальная дисперсия яркости низкая, у полосатой — выше из-за чередования светлых и тёмных полос. Считается через `CIAreaHistogram` (подтверждённый реальный протокол Core Image, тоже наследник `CIAreaReductionFilter`) — гистограмма яркости по области, из которой легко получить дисперсию.
- **Спектр Фурье.** Полосы — это периодическая структура, и в частотной области она даёт выраженный пик на частоте, соответствующей ширине полосы. У однотонной заливки энергия сосредоточена почти целиком на нулевой частоте. Vision/Core Image не имеют собственного FFT, но `vDSP` (Accelerate) даёт функции быстрого преобразования Фурье, применимые к массиву яркостей пикселей.
- **Локальные бинарные шаблоны (LBP).** Классический признак текстуры в компьютерном зрении: для каждого пикселя сравнивают его яркость с яркостью соседей и кодируют результат в бинарное число, затем строят гистограмму таких чисел по области. Полосатая и однотонная текстуры дают статистически разные гистограммы. Готовой реализации LBP в Vision/Core Image нет — пришлось бы писать вручную поверх сырого буфера пикселей (`CVPixelBuffer` / `vImage_Buffer`).
- **Оценка контраста через матрицу совместной встречаемости (GLCM) и признаки Харалика.** Более тяжёлый, но более информативный классический метод текстурного анализа — тоже требует ручной реализации, ничего готового в системных фреймворках iOS нет.

Пример работающего, но грубого признака на основе дисперсии яркости (не требует ничего, кроме Core Image):

```swift
import CoreImage

func brightnessVariance(of image: CIImage, in extent: CGRect) -> CGFloat? {
    guard let filter = CIFilter(name: "CIAreaHistogram") else { return nil }
    filter.setValue(image, forKey: kCIInputImageKey)
    filter.setValue(CIVector(cgRect: extent), forKey: kCIInputExtentKey)
    filter.setValue(64, forKey: "inputCount")   // число корзин гистограммы
    filter.setValue(1.0, forKey: "inputScale")
    guard let outputImage = filter.outputImage else { return nil }

    let context = CIContext()
    var bins = [UInt8](repeating: 0, count: 64 * 4)
    context.render(outputImage,
                    toBitmap: &bins,
                    rowBytes: 64 * 4,
                    bounds: CGRect(x: 0, y: 0, width: 64, height: 1),
                    format: .RGBA8,
                    colorSpace: nil)

    // Берём канал яркости (например, R после предварительного перевода в градации серого)
    // и считаем дисперсию распределения по 64 корзинам — дальше сравнение с порогом.
    let values = stride(from: 0, to: bins.count, by: 4).map { Double(bins[$0]) }
    let mean = values.reduce(0, +) / Double(values.count)
    let variance = values.map { ($0 - mean) * ($0 - mean) }.reduce(0, +) / Double(values.count)
    return CGFloat(variance)
}
```

### Честная оценка осуществимости

Разброс яркости в принципе способен отличить явно полосатого кота от явно однотонного чёрного или белого — это правдоподобно как первое приближение. Но у этого подхода серьёзные слабые места, которые нельзя игнорировать:

- Тень, складки шерсти, блики от вспышки и просто неровное освещение создают точно такой же разброс яркости, что и полосы — признак путает узор с условиями съёмки.
- Биколорный и калико-окрас (два-три сплошных пятна разного цвета) тоже дают высокую дисперсию яркости, но это не «полосатость» в смысле tabby — один и тот же признак не различает разные типы узора между собой, только «есть разброс / нет разброса».
- Ни один из перечисленных признаков не был протестирован здесь на реальном наборе фотографий кошек — это общая теория текстурного анализа, а не проверенное на кошачьей шерсти решение. Заявлять конкретную точность (высокую или низкую) было бы выдумкой.

Итог по этому пункту: технически реализуемо как грубая эвристика, требующая доработки напильником и ручной калибровки порогов на собственном наборе фотографий; готового надёжного решения «из коробки» нет, и получить его на устройстве без сбора и разметки собственных данных, скорее всего, не получится.

## 6. Core ML и готовые модели

### Официальная страница Apple ML models

Проверена страница `developer.apple.com/machine-learning/models/`. Там перечислены только общего назначения модели изображений: FastViT, MobileNetV2, ResNet-50, MNIST и им подобные (обучены на ImageNet или подобных общих наборах). **Ни одной модели для пород, окраса или узора животных на официальной странице нет.**

### Кураторский список Awesome-CoreML-Models

Проверен `likedan.github.io/Awesome-CoreML-Models` — крупный, давно ведущийся список готовых моделей Core ML со всего интернета. Специализированной модели окраса, узора или породы кошки в нём не найдено.

### Что нашлось на GitHub

- **`GitMAM/Breeds_core_ml`** — модель на 37 классов пород (по числу классов похоже на классический Oxford-IIIT Pet Dataset — 12 пород кошек и 25 пород собак, но сам репозиторий явно не называет источник данных, поэтому это предположение, а не подтверждённый факт). Обучена на PyTorch/fast.ai поверх ResNet-50. Файл `model_breeds.mlmodel` весит **102 794 417 байт, то есть около 98 МБ** (проверено напрямую через API GitHub, `contents`-эндпоинт). В README заявлена «accuracy 99.95%» при указанном `error_rate 0.055480` — эти два числа друг другу противоречат (0,05548 ошибки соответствует точности около 94,5%, а не 99,95%), это внутренняя нестыковка самого README, а не проверенный независимо результат. **Лицензии нет** — ни файла `LICENSE`, ни упоминания лицензии в репозитории (проверено через `api.github.com/repos/.../license`, ответ «Not Found»). Без явной лицензии текст и код репозитория по умолчанию защищены авторским правом целиком — использовать в коммерческом продукте без прямого разрешения автора юридически небезопасно.
- Важно: даже если бы эта модель была свободна к использованию, она классифицирует **породу**, а не окрас — «British Shorthair» или «Persian» не говорит, рыжий кот или серый, полосатый или однотонный. Задача игры («ginger, grey, black...» и так далее) — это классификация окраса и узора, а не породы; готовой породной модели мало для нужд игры даже при наличии лицензии.
- **`AranFononi/Animal-Classifier-Pet-Recognition-CoreML-Model`** — классификатор вида животного (собака/кошка/кролик), а не породы и не окраса. Модель крошечная (файл `PetImageClassifier.mlmodel` — около 13 КБ), лицензии тоже нет (тот же метод проверки, тот же результат «Not Found»). Для целей игры бесполезна — вид животного уже известен (это кошка игрока).
- **SigLIP ViT-B/16, конвертированная в Core ML** (репозиторий `john-rocky/CoreML-Models`) — модель zero-shot классификации по образцу CLIP: на вход подаётся изображение и произвольный список текстовых меток («ginger tabby cat», «solid black cat» и так далее), на выходе — оценка сходства с каждой меткой. Лицензия — **Apache-2.0** (подтверждено прямой ссылкой в таблице репозитория на `apache.org/licenses/LICENSE-2.0`, оригинальная модель — `google/siglip-base-patch16-224` на Hugging Face). Размер — **около 386 МБ суммарно на два файла** (кодировщик изображения плюс кодировщик текста, формат FP16). Это единственная найденная модель, которая теоретически годится под задачу игры (можно просто вписать шесть окрасов и шесть узоров текстом), но: во-первых, размер в 386 МБ — это очень много для мобильной игры (сравнимо с объёмом самого приложения); во-вторых, никакой проверки точности именно на тонких различиях окраса кошек (рыжий против кремового, тэбби против калико) не проводилось — CLIP-подобные модели известны тем, что хорошо различают крупные объекты и сюжеты, но заметно хуже — тонкие визуальные атрибуты вроде оттенка или частоты полос.

### Итог по разделу

Открытой, лицензионно чистой и проверенной по точности модели Core ML под окрас или узор кошки не существует — ни у Apple, ни в сообществе. Есть один универсальный крупный вариант (SigLIP) с чистой лицензией, но непроверенной для этой конкретной задачи точностью и внушительным весом, и один узкоспециализированный вариант (породы) без лицензии и не решающий именно задачу окраса.

## 7. Цвет глаз

Отдельного запроса Vision для поиска глаз именно животных не существует. Проверка полного перечня возможностей платформы Vision (главная страница документации, раздел «Pose analysis» и раздел «Image classification and recognition») показывает, что для животных официально задокументированы только два запроса:

- `DetectAnimalBodyPoseRequest` / `VNDetectAnimalBodyPoseRequest` — поза тела (раздел 3 этого документа);
- `RecognizeAnimalsRequest` / `VNRecognizeAnimalsRequest` — распознавание вида животного (кошка/собака, раздел 1).

Аналог `VNDetectFaceLandmarksRequest`, который у Apple даёт для человеческого лица точную геометрию глаз, зрачков и век, — существует **только для человека**. Отдельного «AnimalFaceLandmarks» или «AnimalEyeRequest» в Vision нет.

Практически это означает, что единственная зацепка — это точки `leftEye` и `rightEye` из `VNAnimalBodyPoseObservation.JointName` (раздел 3). Но это **одна точка на глаз**, а не контур радужки или зрачка: Vision отмечает примерное положение центра глаза, а не его форму или границы. Чтобы определить цвет глаз, пришлось бы:

1. Взять координату `leftEye`/`rightEye`;
2. Вырезать очень маленькую область вокруг неё (радиус в единицы-десятки пикселей, в зависимости от разрешения снимка);
3. Усреднить цвет в этой области (методами из раздела 4);
4. Отфильтровать белки, блики от вспышки и шерсть, случайно попавшую в вырезанную область — для этого готового решения нет, отделить радужку от прочего пришлось бы эвристикой по яркости и насыщенности, вручную подобранной и ненадёжной.

Итог: цвет глаз в принципе можно попытаться получить, но это самая шаткая из всех пяти черт — при малом разрешении фото, неидеальном ракурсе или прищуренных глазах точка `leftEye`/`rightEye` либо отсутствует (низкая уверенность распознавания), либо накрывает не радужку, а веко или шерсть вокруг глаза. Никакого штатного, поддерживаемого Apple способа для этого нет — только самодельная эвристика поверх одной точки.

## Приговор

По каждой из пяти черт — что реально получить на устройстве бесплатно, а что нет:

1. **Основной цвет (ginger, grey, black, white, cream, brown).** Реально получить приблизительно. `CIAreaAverage`/`CIKMeans` плюс перевод в HSV дают рабочую, бесплатную оценку. Надёжность зависит от освещения и от того, насколько чисто вырезана область шерсти (без фона и без лап/морды с их возможными белыми пятнами). Это единственная из пяти черт, где предлагаемый способ близок к «готовому решению», а не к сырой эвристике.
2. **Узор (solid, tabby, bicolor, calico, tuxedo, pointed).** Готового способа нет ни в Vision, ни в виде свободной модели Core ML. Собственная эвристика на основе дисперсии яркости технически осуществима, но не различает между собой разные типы узора (полосы против пятен), путает узор с тенями и освещением, и ни разу не проверялась на реальных фотографиях кошек в рамках этого разбора. Прямым текстом: **узор без облака надёжно не получить** — то, что можно собрать самостоятельно, это в лучшем случае грубый переключатель «есть контраст / нет контраста», а не полноценная классификация по шести категориям.
3. **Длина шерсти (short, long).** Не рассматривалась отдельно в требованиях задания как один из явно проверяемых пунктов документации Apple, но по совокупности изученного: ни один из проверенных API Vision (`ClassifyImageRequest`, `VNDetectAnimalBodyPoseRequest`, `VNRecognizeAnimalsRequest`) не даёт такой категории напрямую. В принципе, длину шерсти можно косвенно оценивать через силуэт (контур животного относительно `boundingBox`, размытость границ меха) — это отдельная задача сегментации и контурного анализа, для которой в Vision тоже нет готового животного-специфичного инструмента; своя эвристика возможна, но не проверялась.
4. **Цвет глаз (green, amber, blue).** Получить можно только очень приблизительно, по одной точке `leftEye`/`rightEye` из позы тела, без выделения радужки. Это самая ненадёжная из всех черт: одна точка без учёта формы глаза, чувствительная к разрешению фото и ракурсу.
5. **Белые отметины (грудь, лапы, морда).** Лапы и морда — реально определить приемлемо: `VNDetectAnimalBodyPoseRequest` даёт точные координаты лап и морды (через `nose`/`leftEye`/`rightEye`), вокруг них можно оценить цвет. Грудь — специальной точки нет, придётся достраивать область по точке `neck` и точкам передних лап, что заметно менее надёжно.

**Стоит ли ради этого отказываться от облачной модели.** Из пяти черт полноценно и бесплатно на устройстве получается только основной цвет, и с оговорками — отметины на лапах и морде. Узор — ключевая, отличительная черта кошки в игре — на устройстве без обучения собственной модели надёжно не определяется; готовой открытой модели под эту конкретную задачу не существует, а универсальная SigLIP (Apache-2.0, ~386 МБ) не проверена на пригодность и слишком тяжела для мобильной игры. Цвет глаз получается в лучшем случае приблизительно.

Если владелец готов принять узор и цвет глаз как «то, что даёт приблизительная эвристика, могут быть заметные ошибки, и это ляжет на плечи геймдизайна — как обрабатывать сомнительные случаи», тогда узел-посредник действительно можно убрать, а часть логики (основной цвет, часть отметин) перенести на устройство. Но это не эквивалентная замена облачной модели зрения по качеству результата — это осознанное снижение точности ради экономии на облаке. Если для игры принципиально важно, чтобы узор кошки в игре совпадал с реальным окрасом кота игрока (то есть если это заявленная механика, а не «примерно похожий котёнок»), отказ от облачной модели в пользу голой связки Vision + Core Image + собственных эвристик — это реальный риск получить массовые нарекания игроков на неверно определённый узор, а не просто мелкую техническую недоработку. Экономия на службе-посреднике (четыре задачи и целая служба) реальна и достижима только частично — полностью её всё равно не убрать, если для узора и цвета глаз решат оставить хоть какой-то внешний источник истины или ручную корректировку игроком.

## Источники

Официальная документация Apple (получена через служебный JSON-адрес `developer.apple.com/tutorials/data/documentation/...`, поскольку обычная загрузка страниц отдаёт только заголовок):

- `documentation/vision/vnclassifyimagerequest` — описание запроса и метод `knownClassifications(forRevision:)`.
- `documentation/vision/vnclassifyimagerequest/knownclassifications(forrevision:)` — точная сигнатура метода.
- `documentation/vision/classifyimagerequest` и `documentation/vision/classifyimagerequest/supportedidentifiers` — новый Swift API и его свойство.
- `documentation/vision/classifying-images-for-categorization-and-search` — официальная статья-пример с рабочим кодом на `hasMinimumPrecision`/`hasMinimumRecall`.
- `documentation/vision/vnclassificationobservation`, `.../hasminimumprecision(_:forrecall:)`, `.../hasminimumrecall(_:forprecision:)`, `.../hasprecisionrecallcurve` — методы фильтрации по точности/полноте.
- `documentation/vision/vndetectanimalbodyposerequest`, `documentation/vision/detectanimalbodyposerequest` — старый и новый запросы позы животного, включая проверку платформ и минимальных версий ОС (iOS 17 / iOS 18 соответственно).
- `documentation/vision/vnanimalbodyposeobservation/jointname`, `.../jointsgroupname` — полный перечень из 25 именованных точек тела и шести групп.
- `documentation/vision/vnrecognizeanimalsrequest` — запрос распознавания вида животного.
- `documentation/vision` — общая карта фреймворка, разделы «Pose analysis» и «Image classification and recognition», по которой проверен полный перечень животных-специфичных запросов.
- `documentation/coreimage/ciareaaverage`, `.../cikmeans`, `.../ciareahistogram`, `.../cikmeans/count`, `.../cikmeans/inputmeans`, `.../cikmeans/passes`, `.../cikmeans/perceptual`, `.../ciareareductionfilter/extent` — подтверждение реальности фильтров Core Image и их свойств.

Стенограмма официального доклада Apple:

- WWDC23, сессия 10045 (Vision framework, животная поза) — цитата про поддержку кошек и собак и про 25 точек тела.

Независимые публикации с выгрузкой полного списка категорий классификатора (использованы как источник самого списка, при этом факт совпадения числа категорий и совпадения содержимого проверен вручную по всем трём файлам):

- Гист `ktustanowski/56c0d7541813868fed4aceb60ab5d149` — «VNClassifyImageRequest.Supportedidentifiers.txt», 1303 категории.
- Гист `ozgurshn/0e19568b3f930c58491ddbbe7dbb9170` — тот же список в формате JSON-массива.
- Гист `mikeparisstuff/94a31c29e2bc1e84faea39429bb3879f` — «VNClassifyImageRequest_supportedIdentifiers_dec_26_2023.csv».
- Статья Kamil Tustanowski, Medium, «Animals detection using the Vision framework» — подтверждение, что `VNRecognizeAnimalsRequest` (revision 1) распознаёт ровно `["Cat", "Dog"]`.

Проверка готовых моделей Core ML:

- `developer.apple.com/machine-learning/models/` — официальный список моделей Apple (проверено отсутствие моделей окраса/породы животных).
- `likedan.github.io/Awesome-CoreML-Models` — кураторский список сообщества (проверено отсутствие профильных моделей).
- `github.com/GitMAM/Breeds_core_ml` — модель пород на 37 классов, размер файла и отсутствие лицензии подтверждены через `api.github.com/repos/GitMAM/Breeds_core_ml/contents/` и `.../license`.
- `github.com/AranFononi/Animal-Classifier-Pet-Recognition-CoreML-Model` — классификатор вида животного, размер и отсутствие лицензии подтверждены тем же способом.
- `github.com/john-rocky/CoreML-Models` (файл README.md, раздел «Zero-Shot Image Classification») — модель SigLIP ViT-B/16, лицензия Apache-2.0, размер ~386 МБ, оригинал — `google/siglip-base-patch16-224` на Hugging Face.
- `robots.ox.ac.uk/~vgg/data/pets/` — официальная страница Oxford-IIIT Pet Dataset, 37 категорий (12 пород кошек, 25 пород собак), использована только для сопоставления числа классов с моделью `Breeds_core_ml` — прямой связи между датасетом и конкретным репозиторием не подтверждено.

