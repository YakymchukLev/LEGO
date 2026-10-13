# Документація: Ігровий шар (Runtime Layer)

Папка: `Assets/Scripts/LevelSystem/Runtime/` (та `Assets/Scripts/MenuButtonController.cs`)  
Простір імен: `LegoPuzzle.Runtime`

Цей шар містить усю виконувану логіку гри під час сесії: ігровий процес, фізику та введення для блоків LEGO, адаптивні камери, систему бустерів, глобальну економіку (монети, кристали, серця), міні-гру зі слот-машиною та соковиті UI-ефекти (Juice).

---

## Зміст
1. [Базовий геймплей (Core Gameplay)](#1-базовий-геймплей-core-gameplay)
2. [Система бустерів (Booster System)](#2-система-бустерів-booster-system)
3. [Глобальні менеджери та економіка (Singletons)](#3-глобальні-менеджери-та-економіка-singletons)
4. [Інтерфейс головного меню (Menu UI)](#4-інтерфейс-головного-меню-menu-ui)
5. [Міні-гра «Слот-машина / Лото» (Slots)](#5-міні-гра-слот-машина--лото-slots)
6. [Візуальні ефекти та соковитість інтерфейсу (Juice & VFX)](#6-візуальні-ефекти-та-соковитість-інтерфейсу-juice--vfx)

---

## 1. Базовий геймплей (Core Gameplay)

### `LevelLoader.cs`
* **Файл:** [`LevelLoader.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/LevelLoader.cs)
* **Призначення:** Центральний оркестратор сцени [`Game.unity`](file:///c:/FORK/LEGO/Assets/Scenes/Game.unity).
* **Відповідальність:**
  * Завантажує рівні зі списку `allLevels` або тестовий `testLevelData`.
  * Будує 3D-сітку підлоги, стін, кутів та воріт через префаби [`GridCellView`](#gridcellviewcs).
  * Спавнить 3D-деталі [`LegoPieceView`](#legopieceviewcs) відповідно до конфігурації [`LevelData`](../Data/README.md#1-leveldatacs).
  * Розраховує межі активного поля та здійснює плавне кадрування камери (Smooth Auto-Framing) під заданий кут нахилу (`cameraPitchAngle`), співвідношення сторін екрана та масштаб.
  * Керує таймером рівня (з можливістю заморожування) та умовами перемоги/поразки.
  * Зберігає прогрес у `PlayerPrefs` і керує переходом між рівнями (`LoadNextLevel()`).

### `LegoPieceView.cs`
* **Файл:** [`LegoPieceView.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/LegoPieceView.cs)
* **Призначення:** Контролер фізичного та візуального представлення окремої деталі LEGO на сцені.
* **Відповідальність:**
  * Обробляє введення гравця: події `OnMouseDown`, `OnMouseDrag`, `OnMouseUp` (або Touch) через рейкаст у площину сітки.
  * Перевіряє правила руху: не дозволяє виходити за межі поля, перетинати стіни чи інші деталі.
  * Враховує слайдери (`MoveRestriction`: вільний рух, тільки по горизонталі чи тільки по вертикалі).
  * Забезпечує желеподібну анімацію пружинення (Squash & Stretch) при зіткненнях і зупинках.
  * Якщо деталь дотягнуто до воріт свого кольору — запускає анімацію виходу за край, відтворює звук, тригерить тактильну вібрацію та сповіщає `LevelLoader`.

### `GridCellView.cs`
* **Файл:** [`GridCellView.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/GridCellView.cs)
* **Призначення:** Візуалізація окремої клітинки ігрового поля (підлога, стіна, кут, ворота).
* **Відповідальність:**
  * Підтримує правильне позиціонування мешу стін та куточків.
  * Для воріт (`ExitGate` / `HalfExitGate`) генерує або позиціонує динамічні кольорові стрілки-покажчики виходу з корекцією кута огляду камери.

### `LevelUIController.cs`
* **Файл:** [`LevelUIController.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/LevelUIController.cs)
* **Призначення:** Керує верхнім та діалоговим інтерфейсом у геймплейній сцені.
* **Відповідальність:**
  * Відображає номер поточного рівня та таймер.
  * Показує та ховає панелі перемоги (`WinPanel`) і поразки (`LosePanel`).
  * Зв'язує кнопки рестарту, наступного рівня та виходу в меню з викликами у `LevelLoader`.

### `LevelVideoBackground.cs`
* **Файл:** [`LevelVideoBackground.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/LevelVideoBackground.cs)
* **Призначення:** Відповідає за живий/анімований задній фон (Quad з відео/шейдером), що плавно слідує за камерою та масштабується під екран.

---

## 2. Система бустерів (Booster System)

### `HammerBooster.cs`
* **Файл:** [`HammerBooster.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/HammerBooster.cs)
* **Призначення:** Бустер «Молоток». Дозволяє вибрати будь-яку деталь на полі й знищити її, звільняючи прохід іншим блокам.
* **Пов'язані скрипти:**
  * [`HammerSmashSequence.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/HammerSmashSequence.cs) — процедурна 3D-анімація замаху та удару молотка з ефектом струсу камери.
  * [`HammerCrackEffect.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/HammerCrackEffect.cs) — створення сітки тріщин і розлітання уламків цеглинок у точці удару.

### `TimeFreezeBooster.cs`
* **Файл:** [`TimeFreezeBooster.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/TimeFreezeBooster.cs)
* **Призначення:** Бустер «Заморожування часу». Зупиняє таймер зворотного відліку на рівні на фіксований час (наприклад, 10 секунд) з візуальним морозним ефектом та іконкою сніжинки.

### `HintBooster.cs`
* **Файл:** [`HintBooster.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/HintBooster.cs)
* **Призначення:** Бустер «Підказка» (Лампочка).
* **Відповідальність:**
  * Бере поточний стан сітки і викликає алгоритм BFS-солвера.
  * Знаходить оптимальний наступний крок.
  * Кличе [`HintIndicatorEffect.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/HintIndicatorEffect.cs) для малювання пульсуючої стрілки, яка вказує гравцю правильний хід.

### `BoosterBadgeUI.cs`
* **Файл:** [`BoosterBadgeUI.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/BoosterBadgeUI.cs)
* **Призначення:** Відображає круглий бейдж на кнопці кожного бустера: кількість наявних зарядів або зелену іконку «+», якщо заряди закінчилися.

### `BoosterShopPanel.cs`
* **Файл:** [`BoosterShopPanel.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/BoosterShopPanel.cs)
* **Призначення:** Модальне вікно швидкого поповнення бустерів у грі за золоті монети або перегляд реклами.

### `BoosterTutorialManager.cs`
* **Файл:** [`BoosterTutorialManager.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/BoosterTutorialManager.cs)
* **Призначення:** Показує перше знайомство та підсвітку бустерів при досягненні певних рівнів гри.

---

## 3. Глобальні менеджери та економіка (Singletons)

Усі менеджери реалізовані як постійні сінглтони (`DontDestroyOnLoad`), доступні з будь-якого скрипту через `Instance`.

### `CoinManager.cs`
* **Файл:** [`CoinManager.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/CoinManager.cs)
* **Призначення:** Менеджер золотих монет (основна валюта).
* **Методи:** `AddCoins(int amount)`, `SpendCoins(int amount)`, подія `OnCoinsChanged`. Зберігається у `PlayerPrefs`.

### `GemManager.cs`
* **Файл:** [`GemManager.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/GemManager.cs)
* **Призначення:** Менеджер кристалів/гемів (преміум-валюта). Нараховує +1 гем за кожну перемогу на рівні.

### `HeartManager.cs`
* **Файл:** [`HeartManager.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/HeartManager.cs)
* **Призначення:** Система енергії/життів гравця:
  * Максимум 5 життів.
  * Автоматичне відновлення (1 життя за 30 хвилин) з підтримкою виходу з гри (Unix timestamp).
  * Списання життя при програші за часом або рестарті активного рівня.

### `GameSettingsManager.cs`
* **Файл:** [`GameSettingsManager.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/GameSettingsManager.cs)
* **Призначення:** Централізоване сховище налаштувань:
  * Гучність та увімкнення/вимкнення звуків (`Sound`) і музики (`Music`).
  * Тактильна вібрація (`Haptic Feedback`) при переміщенні блоків та проходженні воріт.
  * Режим для людей з порушенням сприйняття кольорів (`Colorblind Mode`).
  * Метод повного скидання прогресу гри.

---

## 4. Інтерфейс головного меню (Menu UI)

### `MenuButtonController.cs`
* **Файл:** [`MenuButtonController.cs`](file:///c:/FORK/LEGO/Assets/Scripts/MenuButtonController.cs)
* **Призначення:** Головний контролер навігації меню. Обробляє натискання кнопок переходу в гру (`StartGame`), перевіряє наявність життів у гравця перед запуском та налаштовує нижню навігаційну панель.

### `MenuHeartsUI.cs`, `MenuCoinsUI.cs`, `MenuGemsUI.cs`
* **Файли:** [`MenuHeartsUI.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/MenuHeartsUI.cs), [`MenuCoinsUI.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/MenuCoinsUI.cs), [`MenuGemsUI.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/MenuGemsUI.cs)
* **Призначення:** Віджети верхньої панелі меню. Підписуються на відповідні менеджери валют і плавно оновлюють лічильники та таймер регенерації життів.

### `MenuSettingsPanel.cs`
* **Файл:** [`MenuSettingsPanel.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/MenuSettingsPanel.cs)
* **Призначення:** Спливаюче вікно налаштувань у головному меню з перемикачами звуку, музики, вібрації та режиму дальтонізму.

### `MenuShopVideoPanel.cs`
* **Файл:** [`MenuShopVideoPanel.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/MenuShopVideoPanel.cs)
* **Призначення:** Магазин меню з можливістю перегляду промо-відео та отримання безкоштовних монет або енергії.

---

## 5. Міні-гра «Слот-машина / Лото» (Slots)

### `SlotMachinePanel.cs`
* **Файл:** [`SlotMachinePanel.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/SlotMachinePanel.cs)
* **Призначення:** Повний UI та механіка міні-гри «Lotto Slot Machine». Керує трьома барабанами, кнопкою обертання (Spin), щоденним безкоштовним спіном та нарахуванням виграшів.

### `SlotReelView.cs`
* **Файл:** [`SlotReelView.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/SlotReelView.cs)
* **Призначення:** Фізичне безперервне обертання окремого барабана з ефектом затухання швидкості, вирівнюванням на виграшному символі та звуком клацання.

### `SlotItemData.cs`, `SlotsButton.cs`, `SlotTriggerButton.cs`
* **Файли:** [`SlotItemData.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/SlotItemData.cs), [`SlotsButton.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/SlotsButton.cs), [`SlotTriggerButton.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/SlotTriggerButton.cs)
* **Призначення:** Конфігурація нагород слота (монети, бустери, геми, серця) та кнопки виклику панелі слотів у меню.

---

## 6. Візуальні ефекти та соковитість інтерфейсу (Juice & VFX)

* **[`SafeArea.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/SafeArea.cs)** — автоматично підганяє RectTransform канвасу під вирізи та "чубчики" смартфонів (Notch / Dynamic Island).
* **[`UIButtonPressEffect.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/UIButtonPressEffect.cs)** — желеподібна анімація пружинення кнопок при натисканні (Squash & Stretch).
* **[`UIButtonShinePulse.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/UIButtonShinePulse.cs)** — циклічний світловий відблиск і пульсація привабливості на головних кнопках (Play, Claim).
* **[`UIButtonAudioClick.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/UIButtonAudioClick.cs)** — гарантоване відтворення звуку кліку при натисканні будь-якої кнопки.
* **[`UIConfettiEffect.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/UIConfettiEffect.cs)** — святковий феєрверк із конфеті при перемозі на рівні.
* **[`UILegoBrickDebris.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/UILegoBrickDebris.cs)** — розлітання дрібних 2D/3D цеглинок LEGO при знищенні блоків молотком.
* **[`UILostPanelProgress.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/UILostPanelProgress.cs)** — анімована смужка прогресу у вікні поразки (показує, скільки блоків встиг вивести гравець).
* **[`UIPanelSlideIn.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/UIPanelSlideIn.cs)** — плавна поява вікон із виїздом та відскоком (Bounce).
* **[`TutorialHandEffect.cs`](file:///c:/FORK/LEGO/Assets/Scripts/LevelSystem/Runtime/TutorialHandEffect.cs)** — анімована рука-підказка, яка показує жест пальця по екрану для навчання новачків на першому рівні.
