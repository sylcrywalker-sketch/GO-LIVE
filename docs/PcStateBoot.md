# PC State / Boot Backend — Stage 2A

Внутреннее состояние стартового ПК: какие детали установлены, включится ли он, дойдёт ли до рабочего стола, есть ли видеокарта и хватает ли блока питания. Desktop, браузер, Streamly, Critical Strike, интернет и периферия в этот этап не входят и строятся поверх этого слоя.

## 1. Ответственность

| Класс | Отвечает за |
| --- | --- |
| `PcAssembly` (plain C#) | Единственный владелец записи «слот → InstanceId». Правила совместимости (тип + разъём), снимок для сохранения. Не изменён по сути, кроме `PcSlotCheck.FixedInPlace`. |
| `PcCapabilities` (plain C#, значение) | Что ПК может, вычисляется из `PcAssembly` при каждом чтении: наличие деталей, бюджет мощности, `CanPowerOn`, `CanUseDesktop`, `GamingGraphicsAvailable`, `CanPlayCriticalStrike`, упорядоченные диагностики. Не кэшируется. |
| `PcDiagnostic` (plain C#, значение) | Код + серьёзность (`Blocker`/`Limitation`) + что именно затронуто (`PcFunction.PowerOn/Desktop/Gaming`) + ключи локализации + ватты (только `InsufficientPower`). Таблица значений — `PcDiagnostic.For`. |
| `PcPowerOnResult` (plain C#) | Ответ на нажатие кнопки питания: `Blocked`/`Started`, `ReachesDesktop`, текущие capabilities. Без состояния: в игре пока нечему «работать» после включения, поэтому On/Off не хранится и не сохраняется. |
| `PcComponentSpec` (ScriptableObject, только данные) | Тип, разъём, потребление; у блока питания — мощность. `Validate` отвергает невозможные данные. |
| `PcAssemblyBehaviour` (мост Unity) | Слоты сцены, новые детали новой игры (`preinstalled`), две транзакции руки ↔ слот, load. Владелец начальной сборки. |
| `PcComponentSlot` (презентация) | Поза установки, область указателя, подсветка; `fixedInPlace` — деталь пока нельзя вынуть (материнская плата). |
| `PcWorkbenchBehaviour` | Показывает статус и диагностики (сначала слова, потом ватты). Логики ПК не содержит. |

## 2. Правила

- `CanPowerOn` = материнская плата + процессор + ≥1 модуль памяти + блок питания + `TotalPowerDrawWatts ≤ PowerSupplyCapacityWatts`.
- Накопитель для включения не нужен; `CanUseDesktop` = `CanPowerOn` + накопитель.
- Без видеокарты ПК включается и доходит до рабочего стола; `GamingGraphicsAvailable` = `CanPowerOn` + видеокарта; `CanPlayCriticalStrike` = `CanUseDesktop` + `GamingGraphicsAvailable`.
- Мощность не влияет на физическую установку: деталь ставится в подходящий слот, а слабый блок питания даёт `InsufficientPower` и блокирует включение.
- Порядок диагностик фиксирован (`PcDiagnosticCode`): MissingMotherboard, MissingCpu, MissingMemory, MissingPowerSupply, InsufficientPower, MissingStorage, NoDedicatedGpu. Каждая — не больше одного раза. Отсутствующий блок питания — `MissingPowerSupply`, а не `InsufficientPower`.

## 3. Стартовый ПК (новая игра)

| Слот | Тип / разъём | Деталь (ItemId) | Вт |
| --- | --- | --- | --- |
| `motherboard-0` (fixed) | Motherboard / MotherboardTray | `starter-motherboard` | 25 |
| `cpu-0` | Cpu / CpuSocket | `starter-cpu` | 65 |
| `ram-0` | Ram / MemorySlot | `starter-ram` | 3 |
| `psu-0` | Psu / PowerSupplyBay | `used-psu` (существующий товар) | ёмкость 300 |
| `storage-0` | Storage / SataStorage | `starter-hdd` | 6 |
| `gpu-0` | Gpu / PcieX16 | пусто; `budget-gpu` из магазина | 75 |

Итого 99 Вт; с Budget GPU 174 Вт из 300. Детали — реальные `WorldItem` внутри префаба `StudentPC` на `InstallAnchor` своих слотов, с постоянными scene ID (`authoredInstanceId`), и перечислены в `PcAssemblyBehaviour.preinstalled`.

Жизненный цикл: `WorldItem.Awake` создаёт `ItemInstance` (World) → `PcAssemblyBehaviour.Awake` строит слоты → `PcAssemblyBehaviour.Start` (Unity вызывает все Awake загруженной сцены до любого Start) проверяет весь набор целиком тем же `IsValidSnapshot`, что и загрузка, плюс правила сцены (деталь на якоре своего слота, неучтённых предметов на якорях нет, scene ID есть и уникален), и только потом переводит предметы World → Installed и записывает сборку. Ошибка контента: лог, ПК выключен, ничего не сдвинуто. `Start` выполняется один раз; включение/выключение объекта ничего не дублирует. Сохранение/загрузка требуют `PcAssemblyBehaviour.IsReady`.

## 4. Сохранение — v6

Item system хранит InstanceId, DefinitionId и `Installed`; `PcAssembly` хранит SlotId → InstanceId. SlotId в `ItemSaveData` нет, позы у установленных деталей нет (её задаёт слот). Схема поднята до **v6**: сохранения v1–v5 не содержат стартовых деталей, и при загрузке сцены-предметы, отсутствующие в сохранении, стали бы `Removed` — ПК молча лишился бы процессора и памяти. Поэтому v1–v5 отвергаются до изменения живого состояния, миграции нет (pre-release).

## 5. Ограничения этапа

- Материнская плата закреплена (`fixedInPlace`): процессор и память физически стоят на ней, вложенные слоты не моделируются.
- Один слот памяти (`ram-0`), хотя на модели платы два DIMM; второй модуль покрыт доменными тестами.
- Кулер, вентилятор и кабели — статичная презентация.
- Модель б/у блока питания (102 мм) выше ATX-отсека корпуса (87 мм), поэтому `Visual` в `Item_UsedPowerSupply` равномерно масштабирован до 0.845 (86 мм, ATX-высота). FBX не менялся; иконка от равномерного масштаба не меняется.

## 6. Тесты

- `PcCapabilitiesTests` (plain C#): 13 обязательных случаев, мощность, порядок, спецификации, попытка включения.
- `PcPreinstalledPartsTests` (EditMode, настоящий префаб): каждая ошибка начального контента отвергается целиком.
- `PcBuildingContentTests`: таблица слотов, стартовые детали, уникальные ID, мощность, только ПК-детали имеют спецификацию, локализация RU/EN.
- `PcBuildingPlayModeTests`, `PcWorkbenchPlayModeTests` (реальная сцена GL): сценарии A–F, save/load, закреплённая плата.
