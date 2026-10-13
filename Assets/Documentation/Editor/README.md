# Документація: Шар редактора та інструментів (Editor Layer)

Папка: `Assets/Scripts/LevelSystem/Editor/`  
Простір імен: `LegoPuzzle.Editor`  
Усі скрипти обгорнуті в `#if UNITY_EDITOR` і працюють виключно в середовищі розробника Unity.

---

## 1. Процедурна генерація та ШІ-Солвер

### `LegoLevelGenerator.cs`
* **Файл:** [`LegoLevelGenerator.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/LegoLevelGenerator.cs)
* **Призначення:** Статичний математичний рушій для створення та валідації рівнів.
* **Ключові компоненти:**
  * **`BuildLevelWithReverseSolver(...)`**: Генерація рівня «навпаки» (Reverse Assembly). Деталі вводяться на поле через майбутні ворота, після чого поле інтенсивно заплутується (`Scramble`) за правилами руху слайдерів та колізій.
  * **`SolveLevelBFS(...)` (Fast 64-Bit BFS / A\* Solver)**: Високошвидкісний алгоритм проходження рівня. Кодує координати деталей у 64-бітні бітові маски ([`StateKey`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/LegoLevelGenerator.cs#L1603)) та перевіряє до 60 000 станів/сек.
  * **Гарантія якості**: Забезпечує 100% прохідність рівня, відкидає легкі шорткати та перевіряє відсутність ходів за 1 крок на старті (0 Immediate Exits).
  * **Архетипи поля**: Арена, Центральний моноліт, Перешийок кімнат (Hourglass), Лабіринт, Фортеця, Серпантин тощо.

### `LevelGeneratorWindow.cs`
* **Файл:** [`LevelGeneratorWindow.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/LevelGeneratorWindow.cs)
* **Виклик у Unity:** `Tools` ➔ `LEGO Level Generator` ➔ `Відкрити вікно генератора`
* **Вкладки:**
  1. **Single Level Studio** — генерація одного рівня з візуальним переглядом, детальним логом BFS-розв'язку та ручним збереженням у `.asset`.
  2. **Batch Generator** — пакетне створення цілих наборів рівнів (наприклад, з 1 по 20 або з 10 по 50) відповідно до кривої складності.
  3. **Difficulty Curve Inspector** — огляд та перевірка математичних параметрів складності (розмір сітки, кількість деталей, слайдери, таймери) для будь-якого номера рівня.

---

## 2. Візуальні редактори та Інспектори

### `LevelEditorWindow.cs`
* **Файл:** [`LevelEditorWindow.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/LevelEditorWindow.cs)
* **Виклик у Unity:** `Tools` ➔ `LEGO Level Editor`
* **Призначення:** Інтерактивний редактор рівнів для ручного проектування.
* **Можливості:**
  * Візуальне малювання клітинок сітки (підлога, стіни, перешкоди, ворота виходу із зазначенням кольору та стрілки).
  * Розміщення та поворот деталей LEGO будь-яких форм.
  * Налаштування обмежень руху (Free, Horizontal, Vertical).
  * Швидке збереження та завантаження асетів `LevelData`.

### `LevelGridRenderer.cs`
* **Файл:** [`LevelGridRenderer.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/LevelGridRenderer.cs)
* **Призначення:** Спеціалізований модуль відмальовки сітки, клітинок, деталей та воріт всередині вікон редактора (GUI / Handles).

### `LegoShapeDefinitionEditor.cs`
* **Файл:** [`LegoShapeDefinitionEditor.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/LegoShapeDefinitionEditor.cs)
* **Призначення:** Кастомний інспектор для асетів [`LegoShapeDefinition`](../Data/README.md#4-legoshapedefinitioncs).
* Дозволяє мишкою клікати по сітці 5x5 і візуально "виліплювати" форму нової деталі без введення числових координат вручну.

### `LevelLoaderEditor.cs`
* **Файл:** [`LevelLoaderEditor.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/LevelLoaderEditor.cs)
* **Призначення:** Кастомний інспектор для компонента [`LevelLoader`](../Runtime/README.md#levelloadercs).
* **Кнопки швидких дій:**
  * **«🔄 Оновити кадрування камери»**: перераховує зум та позицію камери під активні межі рівня прямо в редакторі.
  * **«🔍 Заповнити всі LevelData»**: автоматично знаходить усі асети рівнів у проекті, сортує їх за номером `levelIndex` та записує в список `allLevels`.

---

## 3. Майстри налаштування сцен (Scene Setup Utilities)

Це меню-хелпери, які за один клік створюють потрібну ієрархію Canvas, підключають компоненти та налаштовують UI.

* **[`BoosterShopSceneSetup.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/BoosterShopSceneSetup.cs)** (`LEGO/Setup Booster Shop Panel in Scene`): створює модальну панель магазину бустерів під активним Canvas.
* **[`MenuSettingsSceneSetup.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/MenuSettingsSceneSetup.cs)** (`LEGO/Setup Settings Panel in Menu`): будує панель налаштувань у меню, розставляє звуки кліків кнопок та желеподібні ефекти.
* **[`MenuCoinsSceneSetup.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/MenuCoinsSceneSetup.cs)** & **[`MenuGemsSceneSetup.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/MenuGemsSceneSetup.cs)**: автоматично налаштовують плашки монет та гемів у верхній навігації.
* **[`SlotMachineSceneSetup.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/SlotMachineSceneSetup.cs)** (`LEGO/Create Lotto Slot Machine Panel in Menu Scene`): створює повну панель слот-машини (3 барабани, кнопка Spin, бейджі нагород).
* **[`SafeAreaEditorHelper.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/SafeAreaEditorHelper.cs)** (`LEGO/Apply SafeArea to Active Canvas`): знаходить кореневі контейнери та навішує адаптер `SafeArea`.
* **[`FolderColorizer.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Editor/FolderColorizer.cs)** (`Assets/Set Folder Color/...`): розширює контекстне меню проекту Unity, дозволяючи фарбувати папки різними кольорами для швидкої візуальної навігації.
