# Документація: Шар даних (Data Layer)

Папка: `Assets/Scripts/LevelSystem/Data/`  
Простір імен: `LegoPuzzle.Data`

Цей шар реалізує **Data-Driven Architecture** на основі `ScriptableObject` та серіалізованих C# класів. Він зберігає конфігурацію рівнів, геометрію блоків, палітри кольорів та стан клітинок без прив'язки до сцени.

---

## 1. `LevelData.cs`
* **Файл:** [`LevelData.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/LevelData.cs)
* **Тип:** `ScriptableObject` (Створюється через `CreateAssetMenu(fileName = "NewLevelData", menuName = "LEGO Puzzle/Level Data")`)
* **Призначення:** Головний контейнер конфігурації одного рівня. Зберігається як окремий файл `.asset` у папці `Assets/Levels/`.

### Основні поля:
| Поле | Тип | Опис |
| :--- | :--- | :--- |
| `levelIndex` | `int` | Порядковий номер рівня (1, 2, 3...). Використовується для сортування та збереження прогресу. |
| `levelTitle` | `string` | Відображувана назва рівня. |
| `gridWidth`, `gridHeight` | `int` | Загальні розміри сітки в клітинках (за замовчуванням 8x8). |
| `timeLimitSeconds` | `float` | Ліміт часу на проходження в секундах (0 = без ліміту). |
| `movesLimit` | `int` | Ліміт ходів (0 = необмежено). |
| `cells` | `CellData[]` | Одновимірний серіалізований масив клітинок сітки довжиною `gridWidth * gridHeight`. |
| `pieces` | `List<LegoPieceData>` | Список усіх розміщених на рівні деталей LEGO. |

### Ключові методи:
* `GetCell(int x, int y)` / `SetCell(int x, int y, CellData data)` — безпечний доступ до клітинок за координатами `(x, y)` з перевіркою меж сітки.
* `EnsureGridCapacity()` — ініціалізує та коригує розмір масиву `cells`, зберігаючи наявні клітинки при зміні розміру сітки.

---

## 2. `LegoPieceData.cs`
* **Файл:** [`LegoPieceData.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/LegoPieceData.cs)
* **Тип:** `[Serializable] class` (Серіалізована структура даних всередині `LevelData`)
* **Призначення:** Описує конкретну деталь LEGO на ігровому полі.

### Основні поля:
| Поле | Тип | Опис |
| :--- | :--- | :--- |
| `pieceId` | `string` | Унікальний ідентифікатор деталі на рівні (наприклад, `"Piece_0"`). |
| `colorType` | `BlockColorType` | Колір деталі (Red, Yellow, Blue, Green, Purple, Pink, Cyan, Orange, Gray тощо). |
| `shape` | `LegoShapeDefinition` | Посилання на асет форми (`1x1`, `1x2`, `2x2`, `L` тощо). |
| `originPosition` | `Vector2Int` | Координата опорної клітинки (Pivot) деталі на сітці `(X, Y)`. |
| `rotationSteps` | `int` | Поворот деталі (0 = 0°, 1 = 90°, 2 = 180°, 3 = 270° за годинниковою стрілкою). |
| `moveRestriction` | `MoveRestriction` | Обмеження руху: `Free` (в усі 4 боки), `HorizontalOnly` (тільки ліворуч/праворуч), `VerticalOnly` (тільки вгору/вниз). |
| `isRequiredForWin` | `bool` | Чи обов'язково вивести цю деталь для перемоги на рівні. |

---

## 3. `CellData.cs`
* **Файл:** [`CellData.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/CellData.cs)
* **Тип:** `[Serializable] class`
* **Призначення:** Описує стан та поведінку кожної окремої клітинки ігрової сітки.

### Основні переліки (Enums):
* `CellType`:
  * `Empty` — неактивна/прозора зона за межами арени.
  * `Walkable` — ігрова поверхня (підлога), якою можуть вільно ковзати деталі.
  * `Obstacle` — повнорозмірна нерухома перешкода на полі.
  * `HalfObstacle` / `QuarterObstacle` — зовнішній периметр бортиків та кутових блоків арени.
  * `ExitGate` / `HalfExitGate` — ворота виходу, через які деталі покидають поле.
* `ExitDirection`: Напрямок стрілки воріт та виходу деталі (`Up`, `Down`, `Left`, `Right`).

### Основні поля:
| Поле | Тип | Опис |
| :--- | :--- | :--- |
| `gridPosition` | `Vector2Int` | Координати клітинки `(x, y)`. |
| `cellType` | `CellType` | Тип клітинки (підлога, перешкода, ворота). |
| `exitDirection` | `ExitDirection` | Напрямок виходу або внутрішня орієнтація бортика. |
| `gateColorType` | `BlockColorType` | Колір воріт. Тільки деталь відповідного кольору (або Universal) може вийти тут. |

---

## 4. `LegoShapeDefinition.cs`
* **Файл:** [`LegoShapeDefinition.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/LegoShapeDefinition.cs)
* **Тип:** `ScriptableObject` (Створюється через `CreateAssetMenu(fileName = "NewLegoShape", menuName = "LEGO Puzzle/Shape Definition")`)
* **Призначення:** Описує конфігурацію форми деталі (набір клітинок відносно точки `(0,0)`).

### Основні поля:
| Поле | Тип | Опис |
| :--- | :--- | :--- |
| `shapeName` | `string` | Читабельна назва форми (наприклад, `"1x2"`, `"L-Shape"`). |
| `occupiedCells` | `List<Vector2Int>` | Список відносних зміщень клітинок, які займає деталь при повороті 0°. |
| `customPrefab` | `GameObject` | (Опціонально) Окремий префаб 3D-моделі під цю специфічну форму. |

### Ключові методи:
* `GetRotatedOffsets(int rotationSteps)` — повертає координати зайнятих клітинок з урахуванням кута повороту (0°, 90°, 180°, 270°).
* `GetRotatedBounds(int rotationSteps)` — повертає фактичні габарити форми `(Width, Height)` у клітинках при заданому повороті.

---

## 5. `BlockPalette.cs`
* **Файл:** [`BlockPalette.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/BlockPalette.cs)
* **Тип:** `ScriptableObject`
* **Призначення:** Централізована палітра візуальних та звукових ресурсів гри.

### Основні поля:
* `colorEntries` — перелік зв'язок `BlockColorType` ➔ `Material` для фарбування 3D-моделей блоків.
* Матеріали для підлоги, стін, кутів, воріт різних кольорів.
* Посилання на базові 3D-моделі деталей (1x1, 1x2, 2x2 тощо).
* Стрілки воріт та партикли виходу.
* Звуки пересування блоків (`pieceMoveSound1`, `pieceMoveSound2`), звуки виходу та перемоги.

---

## 6. `ColorblindPalette.cs`
* **Файл:** [`ColorblindPalette.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/ColorblindPalette.cs)
* **Тип:** `ScriptableObject`
* **Призначення:** Альтернативна палітра для гравців з порушенням сприйняття кольорів (дальтонізмом).
* Замінює схожі відтінки на високонтрастні варіанти (наприклад, за шкалою Wong / Okabe-Ito), що гарантує легке розпізнавання пар «блок — ворота».
