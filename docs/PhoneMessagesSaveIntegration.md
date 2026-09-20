# Phone Messages Save/Load Integration v1

Проект: `E:\GO! Live`, Unity `6000.6.0f1`. Только runtime owner и интеграция с существующим сохранением. UI, seed-переписка, Phone Engine, Shop и сцены не изменены.

## Production .cs

- **Новый** `Assets/Game/Scripts/Phone/PhoneMessagesBehaviour.cs`: один scene-owned экземпляр `PhoneMessages`, доступный через `Messages`. Нет UI, callbacks, начальных контактов, навигации, событий или бизнес-логики в компоненте. Инициализация не зависит от порядка Awake и не повторяется при enable/disable.
- **Изменён** `Assets/Game/Scripts/Persistence/GameSaveData.cs`: поле `PhoneMessagesSnapshot Messages`.
- **Изменён** `Assets/Game/Scripts/Persistence/GameSaveController.cs`: обязательная serialized-ссылка `_phoneMessages`, capture/restore, preflight и save version **2**. Snapshot Messages остаётся версии **1**. Старые Skeleton saves версии 1 отклоняются; миграций нет.

`PhoneMessages.cs` и `PhoneMessagesSnapshot.cs` не изменены. Новых менеджеров, репозиториев, сервисов, singleton и пакетов нет.

## Владение и порядок операций

В сцене размещается один `PhoneMessagesBehaviour`; SaveController и будущие consumers должны ссылаться именно на него. Нет автоматического создания владельца в UI/SaveController, поиска объектов или `DontDestroyOnLoad`. Lifetime — объект игровой сцены/сессии. `DisallowMultipleComponent` запрещает повтор компонента на том же объекте; глобальный singleton для запрета дубликатов в разных объектах не вводился.

**Save:** существующий Capture вызывает `owner.Messages.CaptureSnapshot()`. JSON содержит контакты, порядок истории, направления, контент, игровые timestamps и IsRead; счётчики unread восстанавливаются из истории. Существующий атомарный путь записи файла сохранён.

**Load:** parse → проверка общей версии/структуры → проверка Messages → остальные существующие проверки → Apply остальных данных → `owner.Messages.Restore(data.Messages)`. Владелец и его экземпляр ядра не заменяются. Повторная загрузка заменяет историю, а не добавляет её повторно.

Preflight вызывает тот же `Restore` на временном domain-экземпляре без подписчиков. Это краткоживущая проверка, не второй runtime owner. Нет дублирования правил snapshot. Неправильный MessageId, дубликаты, версия, timestamp и другая ошибка domain-валидации отклоняют загрузку до изменения живых состояний. Входной файл не перезаписывается.

В Unity 6000.6 `JsonUtility.FromJson` создаёт inline-объект с default-значениями даже для отсутствующего/null Messages. Чтобы это не очистило историю незаметно, Load использует существующий `JsonUtility.FromJsonOverwrite` с исходно невалидным Messages snapshot (Version 0, Conversations null). Отсутствующий, null или пустой объект Messages не проходит preflight. Корректный пустой snapshot версии 1 с массивом `[]` разрешён.

Граница сериализатора: Unity нормализует JSON `"Conversations": null` в пустой массив. Это проверено отдельным тестом и следует текущей семантике JsonUtility; строгий самостоятельный JSON schema validator не добавлялся. В C# snapshot с `Conversations = null` доменная проверка отклоняет. Сам Capture всегда записывает настоящий массив.

SaveController ранее не имел общей транзакции rollback для произвольных исключений во время Apply остальных игровых систем. Этот этап её не добавляет. Гарантия здесь: invalid Messages snapshot отклоняется **до** Apply, а domain Restore не применяет историю частично. Исключения внешних подписчиков сохраняют существующий контракт ядра после commit; интеграция не обещает откат произвольных побочных эффектов.

## Назначение в Unity Inspector

Вне Play Mode, в используемой игровой сцене (в текущих `GL` и `GL_002` объект называется `[GameSystems]`):

1. На существующий **`[GameSystems]`** добавить **ровно один `PhoneMessagesBehaviour`**. Новый GameObject не нужен; в PhoneRig компонент не добавлять.
2. В существующем **`GameSaveController` → Game State → Phone Messages** назначить этот же компонент с `[GameSystems]`.
3. Сохранить используемую сцену. Если обе сцены запускаются отдельно, выполнить это в каждой из них; не создавать несколько владельцев в одной игровой сессии.

У самого владельца нет настраиваемых полей. Без ссылки Save/Load возвращает отказ с ошибкой конфигурации, не создаёт временную пустую историю и не перезаписывает save. Никакие scene/prefab changes этим этапом автоматически не сохранены.

## Проверки

- До изменений: **43/43** EditMode PASS.
- Новый capture-тест сначала воспроизвёл проблему: реальный save-файл не содержал записанную историю.
- Итог: **63/63 PASS**, 0 failed, 0 skipped: 20 Save integration + 37 Messages Core + 6 PhoneSession.
- Проверены фактические `TrySave` / `TryLoad` существующего контроллера с временными JSON-файлами: unread, outgoing/текст/время, несколько и пустые conversations, защита ID после load, повторный load в тот же экземпляр, пустая история, невалидные snapshots, отсутствие owner, старый формат и сохранение остальных проверяемых состояний.
- Отдельно проверены реальные особенности JsonUtility для отсутствующих/null inline-полей и массивов.
- Unity runtime/Editor compilation: **0 compile errors**. В логах остаются ранее существовавшие CS0618 для FindObjectsByType; новый owner ошибок/предупреждений не добавляет.
- EditMode fixture создаёт временные компоненты и явно задаёт их состояние через reflection. Это тест Save/Load, **не проверка Awake/Start/Play Mode**. Настоящий Play Mode и player build не запускались.
- Контроль хешей подтвердил: существующие сцены, prefab, Phone Engine, core и UI не изменены. Кроме двух целевых persistence-файлов, Unity обновила в `.csproj` только ссылки на новые owner и test .cs.

Результаты: `E:\Reports\PhoneMessagesSave-20260920\editmode-tests.xml`, `editmode-tests.log`, `baseline-tests.xml`, `red-tests.xml`, `scope-check.json`; там же исходный рабочий diff и копии двух persistence-файлов до изменений.

Добавлен `Assets/Game/Tests/Editor/PhoneMessagesSaveTests.cs` и его `.meta`; owner также имеет Unity-generated `.meta`. Существующие тесты не удалены. Из-за поведения сериализатора тест JSON null-массива выделен из проверок invalid domain snapshot в самостоятельную проверку нормализации.

Следующие этапы не начаты: Messages UI binding, landlord seed, ответы игрока, Shop, Orders, Delivery. Коммит/push не выполнялись.
