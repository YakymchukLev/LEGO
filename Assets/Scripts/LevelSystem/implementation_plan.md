# План реалізації системи рівнів для головоломки LEGO (300+ рівнів)

Ця система дозволяє зберігати та налаштовувати 300+ унікальних рівнів гри (з різними формами поля, перешкодами, воротами та фігурами LEGO) у форматі легких `ScriptableObject`, використовуючи лише **одну спільну сцену гри** та візуальний редактор рівнів.

---

## Запропонована архітектура

Система складатиметься з 3 основних шарів:
1. **Data Layer (Дані та структури):** `ScriptableObject` для рівнів, конфігурації фігур, клітинок сітки та бази ассетів.
2. **Runtime Layer (Ігрова логіка):** `GridManager` / `LevelLoader` для побудови сітки, розміщення деталей, обробки таймера та умов перемоги/поразки.
3. **Editor Layer (Візуальний редактор):** Інструмент Unity Editor Window для малювання форми сітки, розміщення перешкод, воріт та деталей LEGO мишкою.

---

## Запропоновані зміни

### 1. Data Layer (Моделі даних)

#### [NEW] [CellData.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/CellData.cs)
- Перелічення `CellType`: `Empty` (порожньо/поза полем), `Walkable` (жовтий тайл), `Obstacle` (дерев'яна перешкода), `ExitGate` (ворота виходу).
- Перелічення `ExitDirection`: `Up`, `Down`, `Left`, `Right`.
- Перелічення `MoveRestriction`: `Free`, `HorizontalOnly` (`↔`), `VerticalOnly` (`↕`), `Locked`.
- Структура `CellData` (позиція X/Y, тип, напрямок виходу, колір воріт).

#### [NEW] [LegoShapeDefinition.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/LegoShapeDefinition.cs)
- `ScriptableObject` для визначення форми LEGO (матриця/список локальних клітинок: `1x1`, `2x2`, `2x4`, `L-Shape`, `T-Shape`, `4x4` тощо).
- Дозволяє додавати будь-які нові форми блоків без зміни коду.

#### [NEW] [LegoPieceData.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/LegoPieceData.cs)
- Структура конкретного блоку на рівні: посилання на `LegoShapeDefinition`, початкова позиція (X, Y), колір, поворот (0, 90, 180, 270), обмеження руху.

#### [NEW] [LevelData.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/LevelData.cs)
- Головний `ScriptableObject` рівня:
  - Номер рівня, назва.
  - Ліміт часу (секунди) або ходів.
  - Розміри сітки (`width`, `height`).
  - Масив клітинок `CellData[]` (визначає унікальну форму та ворота).
  - Список блоків `List<LegoPieceData>`.

#### [NEW] [BlockPalette.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Data/BlockPalette.cs)
- `ScriptableObject` реєстру матеріалів/кольорів та префабів блоків/тайлів/воріт.

---

### 2. Runtime Layer (Спавн та ігрова логіка)

#### [NEW] [GridCellView.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/GridCellView.cs)
- Візуальне представлення клітинки на сцені: звичайний тайл, дерев'яна перешкода або рамка воріт зі стрілкою відповідного кольору.

#### [NEW] [LegoPieceView.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/LegoPieceView.cs)
- Компонент блоку на сцені: візуалізація LEGO-шипів, кольору, стрілочок обмеження руху (`↔`/`↕`), обробка перетягування пальцем/мишкою по сітці з колізіями.

#### [NEW] [LevelLoader.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/LevelLoader.cs)
- Завантажує поточний `LevelData`:
  - Очищає попереднє поле.
  - Центрує камеру відповідно до розміру сітки.
  - Генерує тайли, перешкоди та ворота.
  - Спавнить деталі LEGO на їхні початкові позиції.
  - Керує таймером рівня та відстежує, коли всі потрібні деталі вийшли через ворота (Win Condition).

---

### 3. Editor Layer (Візуальний редактор рівнів)

#### [NEW] [LevelEditorWindow.cs](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/LevelEditorWindow.cs)
- Меню `Tools -> LEGO Level Editor` у Unity.
- Інтерактивна сітка:
  - Режим "Пензля": вибір тайлу (Walkable / Obstacle / Exit Gate / Empty) та малювання кліком мишки.
  - Налаштування воріт: вибір кольору та напрямку стрілки.
  - Розміщення фігур LEGO: вибір форми, кольору, обмеження руху та клік для встановлення на сітку.
  - Кнопки: **"Створити новий рівень"**, **"Зберегти в LevelData"**, **"Завантажити рівень для редагування"**, **"Тестувати в Play Mode"**.

---

## План верифікації

### 1. Перевірка компіляції та структур
- Переконатися у відсутності помилок компіляції C# у проекті.
- Перевірити коректність серіалізації `LevelData` у форматі Unity `ScriptableObject`.

### 2. Перевірка генерації та редактора
- Створити тестовий рівень через `ScriptableObject` (аналог скріншота з рівня 11).
- Перевірити коректність ініціалізації сітки, встановлення форми та спавну блоків через `LevelLoader`.
