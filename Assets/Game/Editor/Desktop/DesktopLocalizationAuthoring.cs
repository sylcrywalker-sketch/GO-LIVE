using System.Collections.Generic;
using System.Linq;
using GoLive.Localization;
using UnityEditor;

namespace GoLive.Editor.Desktop
{
    internal static class DesktopLocalizationAuthoring
    {
        internal static void Apply(LocalizationContext context)
        {
            var contextData=new SerializedObject(context);
            var catalog=contextData.FindProperty("_catalog").objectReferenceValue;
            var serialized=new SerializedObject(catalog);var entries=serialized.FindProperty("_entries");
            var known=new Dictionary<string,int>();
            for(int i=0;i<entries.arraySize;i++) known.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("_key").stringValue,i);
            foreach(var line in Lines.Concat(ShellPolishLocalization.Lines).Concat(CommunityPolishLocalization.Lines)
                .Concat(BroadcastPolishLocalization.Lines).Concat(PcPolishLocalization.Lines).Concat(PeripheralReadinessLocalization.Lines)
                .Concat(StreamCoreLocalization.Lines).Concat(ViewerCoreLocalization.Lines))
            {
                string[] fields=line.Split('|');
                if(fields.Length!=3) throw new System.InvalidOperationException("Invalid desktop localization line: "+line);
                if(!known.TryGetValue(fields[0],out int index)){index=entries.arraySize++;known.Add(fields[0],index);}
                var entry=entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_key").stringValue=fields[0];
                entry.FindPropertyRelative("_russian").stringValue=fields[1];
                entry.FindPropertyRelative("_english").stringValue=fields[2];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);
        }
        private static readonly string[] Lines=
        {
            "pc.object.case|Компьютер|Computer",
            "pc.workbench.enter|Сборка ПК|PC Build Mode",
            "pc.object.monitor|Монитор|Monitor",
            "pc.power.on|Включить ПК|Turn PC on",
            "pc.power.off|Выключить ПК|Turn PC off",
            "pc.monitor.on|Включить монитор|Turn monitor on",
            "pc.monitor.off|Выключить монитор|Turn monitor off",
            "pc.session.sit|Сесть за ПК|Sit at PC",
            "pc.session.hands_full|Сначала освободите руки|Put down the item first",
            "pc.session.focus_hint|ЛКМ — открыть рабочий стол     F — монитор     Esc — встать|Click — open desktop     F — monitor     Esc — stand up",
            "pc.session.off_hint|F — монитор     Esc — встать\nПК включается кнопкой на корпусе|F — monitor     Esc — stand up\nTurn the PC on at its case",
            "pc.status.power_budget|Питание: {0} / {1} Вт|Power: {0} / {1} W",
            "pc.slot.compatible|Совместимо|Compatible",
            "pc.slot.incompatible|Не подходит|Not compatible",
            "pc.slot.required|Требуется: {0}|Required: {0}",
            "pc.parts.title|Компоненты|Components",
            "desktop.app.mycomputer|Мой компьютер|My Computer",
            "desktop.app.mycomputer.description|Диски и свободное место|Drives and available space",
            "desktop.app.streamly|Streamly|Streamly",
            "desktop.app.streamly.description|Всё для вашего первого эфира.|Everything for your first stream.",
            "desktop.app.trich|Trich|Trich",
            "desktop.app.trich.description|Ваш канал, зрители и новые истории.|Your channel, viewers and new stories.",
            "desktop.app.outline|Outline|Outline",
            "desktop.app.outline.description|Почта, с которой всё начинается.|The inbox where it all begins.",
            "desktop.app.donation|Donation|Donation",
            "desktop.app.donation.description|Поддержка от вашей аудитории.|A little support from your audience.",
            "desktop.app.hub|Hub|Hub",
            "desktop.app.hub.description|Приложения для вашего компьютера|Apps for your computer",
            "desktop.app.web|Web|Web",
            "desktop.app.web.description|Ваше окно в сеть GO!|Your window to the GO! network",
            "desktop.start|GO! Пуск|GO! Start",
            "desktop.leave|Esc · Назад|Esc · Back",
            "desktop.boot|Запускаем домашний компьютер…|Starting your home computer…",
            "desktop.minimize|—|—",
            "desktop.close|×|×",
            "desktop.saved|Сохранено|Saved",
            "desktop.save|Сохранить|Save",
            "desktop.save_profile|Сохранить профиль|Save profile",
            "desktop.copy|Копировать код|Copy code",
            "desktop.copied|Код скопирован|Code copied",
            "desktop.paste|Вставить|Paste",
            "desktop.install|Установить|Install",
            "desktop.installed|Приложение установлено|App installed",
            "desktop.open|Открыть|Open",
            "desktop.previous|←|←",
            "desktop.next|→|→",
            "desktop.error.not_installed|Установите приложение в Hub.|Install this app from Hub.",
            "desktop.error.already_installed|Приложение уже установлено.|This app is already installed.",
            "desktop.error.unknown_app|Приложение недоступно.|This app is unavailable.",
            "desktop.error.drive_unavailable|Нет доступного накопителя.|No storage drive is available.",
            "desktop.error.insufficient_storage|Недостаточно места на диске.|There is not enough disk space.",
            "desktop.computer.title|Этот компьютер|This computer",
            "desktop.computer.subtitle|Ваши накопители. Установленные приложения занимают место на своём диске.|Your storage drives. Installed apps use space on their own disk.",
            "desktop.disk.name|Локальный диск ({0}:)|Local disk ({0}:)",
            "desktop.disk.capacity|Свободно {0:0.0} ГБ из {1:0.0} ГБ|{0:0.0} GB free of {1:0.0} GB",
            "desktop.disk.empty|Накопители не подключены. Проверьте сборку ПК.|No drives connected. Check your PC build.",
            "desktop.hub.title|Хорошие приложения. Большие планы.|Small apps. Big plans.",
            "desktop.hub.subtitle|Начните с Outline, создайте канал в Trich и выходите в эфир со Streamly.|Start with Outline, create a Trich channel and go live with Streamly.",
            "desktop.outline.create_title|Почта для вашего нового начала|An inbox for your new beginning",
            "desktop.outline.create_subtitle|Один адрес для писем и регистрации канала. Пароль не нужен.|One address for messages and your channel. No password needed.",
            "desktop.outline.username|Придумайте имя для адреса|Choose your address name",
            "desktop.outline.username_hint|например, nightowl|e.g. nightowl",
            "desktop.outline.create|Создать адрес|Create address",
            "desktop.outline.no_password|От 3 до 24 латинских букв, цифр, точек, дефисов или подчёркиваний.\nПисьма только входящие — отвечать пока не требуется.|Use 3–24 Latin letters, numbers, dots, dashes or underscores.\nAn incoming-only inbox. No replies needed yet.",
            "desktop.outline.inbox|Входящие|Inbox",
            "desktop.outline.select|Выберите письмо|Select a message",
            "desktop.outline.empty|Здесь будут ваши письма. После первого эфира загляните за итогами.|Your messages will arrive here. Check back after your first stream.",
            "desktop.outline.already_created|Адрес уже создан.|Your address already exists.",
            "desktop.outline.invalid_username|Проверьте имя: 3–24 символа, латинские буквы и цифры.|Check your name: 3–24 characters, Latin letters and numbers.",
            "desktop.outline.account_required|Сначала создайте адрес Outline.|Create an Outline address first.",
            "desktop.outline.invalid_message|Не удалось прочитать письмо.|This message could not be read.",
            "desktop.outline.history_full|Почтовый архив заполнен.|The mail archive is full.",
            "desktop.outline.message_missing|Письмо больше недоступно.|This message is no longer available.",
            "desktop.trich.join|Первый шаг к своему каналу|Your channel starts here",
            "desktop.trich.join_subtitle|Зарегистрируйтесь с адресом Outline. Зрители скоро найдут вас.|Register with your Outline address. Your audience is out there.",
            "desktop.trich.email|Адрес Outline|Outline address",
            "desktop.trich.email_hint|ваше.имя@outline.local|your.name@outline.local",
            "desktop.trich.register|Создать канал|Create channel",
            "desktop.trich.open_outline|Открыть Outline|Open Outline",
            "desktop.trich.channel|Ваш канал|Your channel",
            "desktop.trich.name|Название канала|Channel name",
            "desktop.trich.name_hint|Как вас будут звать зрители?|What should viewers call you?",
            "desktop.trich.description|О канале|About your channel",
            "desktop.trich.description_hint|Во что играете? О чём общаетесь?|What do you play? What do you talk about?",
            "desktop.trich.code|Код канала|Channel code",
            "desktop.trich.statistics|Эфиров: {0}\nПодписчиков: {1}\nПик зрителей: {2}\nВ эфире: {3:0} мин\nПоддержка: ${4:0.00}|Streams: {0}\nFollowers: {1}\nPeak viewers: {2}\nOn air: {3:0} min\nSupport: ${4:0.00}",
            "desktop.trich.your_channel|Ваш будущий канал|Your future channel",
            "desktop.trich.already_registered|У вас уже есть канал.|You already have a channel.",
            "desktop.trich.outline_required|Сначала создайте почту в Outline.|Create your email in Outline first.",
            "desktop.trich.email_mismatch|Используйте адрес вашего аккаунта Outline.|Use the address of your Outline account.",
            "desktop.trich.account_required|Сначала зарегистрируйте канал Trich.|Register a Trich channel first.",
            "desktop.trich.invalid_profile|Имя: 1–32 символа. Описание: до 240 символов. Без угловых скобок.|Name: 1–32 characters. Description: up to 240. No angle brackets.",
            "desktop.trich.invalid_summary|Не удалось записать итог эфира.|The stream summary could not be recorded.",
            "desktop.trich.total_limit|Достигнут предел статистики канала.|The channel statistics limit has been reached.",
            "desktop.stream.setup|Ваш эфир начинается здесь|Your stream starts here",
            "desktop.stream.code|Код из профиля Trich|Code from your Trich profile",
            "desktop.stream.code_hint|Вставьте код канала|Paste your channel code",
            "desktop.stream.connect|Подключить|Connect",
            "desktop.stream.connected|● Канал подключён|● Channel connected",
            "desktop.stream.not_connected|Канал не подключён|No channel connected",
            "desktop.stream.quality|Качество эфира|Stream quality",
            "desktop.stream.quality.low|Низкое|Low",
            "desktop.stream.quality.medium|Среднее|Medium",
            "desktop.stream.quality.high|Высокое|High",
            "desktop.stream.requirements|Интернет: {0:0.#} Мбит/с на отдачу\nНизкое — 1 · Среднее — 3 · Высокое — 6|Upload: {0:0.#} Mbps\nLow — 1 · Medium — 3 · High — 6",
            "desktop.stream.preview|ПРЕДПРОСМОТР|PREVIEW",
            "desktop.stream.ready|Можно начинать эфир.|Ready to go live.",
            "desktop.stream.start|Начать эфир|Go live",
            "desktop.stream.stop|Закончить эфир|End stream",
            "desktop.stream.state.offline|Не в эфире|Offline",
            "desktop.stream.state.starting|Подключаемся…|Connecting…",
            "desktop.stream.state.live|● В ЭФИРЕ|● LIVE",
            "desktop.stream.state.stopping|Завершаем эфир…|Ending stream…",
            "desktop.stream.channel_required|Сначала создайте канал в Trich.|Create a channel in Trich first.",
            "desktop.stream.invalid_code|Код не совпадает. Скопируйте его из профиля Trich.|That code does not match. Copy it from your Trich profile.",
            "desktop.stream.invalid_quality|Выберите доступное качество.|Select an available quality.",
            "desktop.stream.busy|Дождитесь завершения текущего эфира.|Wait for the current stream to finish.",
            "desktop.stream.pc_off|Включите компьютер.|Turn the computer on.",
            "desktop.stream.desktop_required|Для эфира нужен рабочий компьютер с накопителем.|Streaming needs a working computer with storage.",
            "desktop.stream.gpu_required|Для высокого качества нужна видеокарта.|High quality needs a dedicated graphics card.",
            "desktop.stream.upload_low|Скорости отдачи недостаточно. Снизьте качество.|Upload speed is too low. Choose a lower quality.",
            "desktop.stream.not_live|Сейчас нет активного эфира.|There is no active stream.",
            "desktop.stream.total_limit|Достигнут предел статистики эфиров.|The stream statistics limit has been reached.",
            "desktop.overlay.chat|ЧАТ ЭФИРА|STREAM CHAT",
            "desktop.overlay.stats|Зрителей  {0}\nВремя  {1:00}:{2:00}\nПодписки  +{3}\nПоддержка  ${4:0.00}|Viewers  {0}\nTime  {1:00}:{2:00}\nFollowers  +{3}\nSupport  ${4:0.00}",
            "desktop.donation.title|Маленькое спасибо. Большая поддержка.|A small thank you. A big difference.",
            "desktop.donation.subtitle|Настройте имя и уведомления. Поддержка зрителей появится в истории.|Set your name and alerts. Viewer support will appear in your history.",
            "desktop.donation.name|Имя получателя|Recipient name",
            "desktop.donation.name_hint|Ваше имя или название канала|Your name or channel name",
            "desktop.donation.alerts|Показывать уведомления во время эфира|Show alerts during your stream",
            "desktop.donation.total|Всего: ${0:0.00}|Total: ${0:0.00}",
            "desktop.donation.history|Последняя поддержка|Recent support",
            "desktop.donation.receipt|{0}  ·  ${1:0.00}|{0}  ·  ${1:0.00}",
            "desktop.donation.empty|Пока пусто. Первый зритель ещё придёт.|Nothing yet. Your first supporter is out there.",
            "desktop.donation.invalid_name|Имя должно содержать от 1 до 32 символов.|Use a name between 1 and 32 characters.",
            "desktop.donation.invalid_receipt|Не удалось принять поддержку.|This contribution could not be accepted.",
            "desktop.donation.total_limit|Достигнут предел суммы поддержки.|The contribution total limit has been reached.",
            "desktop.donation.history_full|История поддержки заполнена.|The contribution history is full.",
            "desktop.web.home|Домой|Home",
            "desktop.web.address|Адрес страницы|Page address",
            "desktop.web.go|Перейти|Go",
            "desktop.web.home_body|Добро пожаловать домой.\n\nНайдите нужные приложения в Hub или загляните на свой канал Trich. Ваш первый эфир — ближе, чем кажется.|Welcome home.\n\nFind your apps in Hub or visit your Trich channel. Your first stream is closer than you think.",
            "desktop.web.unavailable|Эта страница пока недоступна в сети GO!.\n\nОткройте home.go, hub.go или trich.go.|This page is not available on the GO! network yet.\n\nTry home.go, hub.go or trich.go.",
            "desktop.web.hub_link|Hub · Найти приложения|Hub · Find your apps",
            "desktop.web.trich_link|Trich · Мой канал|Trich · My channel",
            "desktop.mail.stream.subject|Ваш эфир завершён|Your stream has ended",
            "desktop.mail.stream.body|{0}, спасибо за эфир!\n\nПродолжительность: {1} сек\nПик зрителей: {2}\nНовых подписчиков: {3}\nПоддержка: ${4}\n\nДо встречи в следующем эфире.\nКоманда Trich|Thanks for streaming, {0}!\n\nDuration: {1} sec\nPeak viewers: {2}\nNew followers: {3}\nSupport: ${4}\n\nSee you next stream.\nThe Trich team"
        };
    }
}
