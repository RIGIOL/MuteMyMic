# Mute my Mic

Маленькая программа для Windows, которая **выключает микрофон во всей системе** по горячей клавише или кнопке мыши — сразу для Google Meet, Discord, Teams, Zoom, Telegram, OBS и любых других программ.

*English below.*

![icon](assets/app.ico)

## Возможности

- 🎙️ Выключает/включает микрофоны в Windows (то же самое, что «Отключить звук» в параметрах звука). Можно выбрать, какие микрофоны выключать.
- ⌨️ Горячие клавиши **или кнопки мыши** (средняя или боковая) — работают, в каком бы окне вы ни находились: «вкл./выкл.» (по умолчанию **Ctrl + Alt + F12**), а также отдельные «только выключить» и «только включить».
- 🧩 Команды для Stream Deck, AutoHotkey и программ клавиатур: `MuteMyMic.exe --toggle`, `--mute`, `--unmute`.
- 🔴 Значок перечёркнутого микрофона поверх всех окон, пока микрофон выключен: выбор монитора, угла и размера; пока открыты настройки, значок можно перетащить мышью в любое место. Можно отключить.
- 🔔 Короткий звук при выключении и включении с регулировкой громкости (отключается).
- 🔒 По желанию: выключать микрофон при блокировке компьютера (Win + L) и включать обратно после разблокировки.
- ▶️ По желанию: выключать микрофон при запуске программы вручную. Компьютер же всегда стартует с включёнными микрофонами — перед выключением ПК программа включает их обратно.
- 🔄 Следит за изменениями извне: если выключить или включить микрофон в параметрах Windows или другой программе, значок и иконка в трее это покажут.
- 🔒 Полностью офлайн: никаких сетевых запросов, телеметрии и проверок обновлений; права администратора не нужны.
- 🚀 Опция «Запускать вместе с Windows».
- 🌍 13 языков: English, Русский, Українська, Deutsch, Français, Español, Português, Italiano, Polski, Türkçe, 中文, 日本語, 한국어. Язык выбирается при первом запуске и меняется в настройках.
- 🖱️ Иконка в трее: левый клик — вкл/выкл микрофон, правый — меню.
- Микрофон, подключённый, пока вы в режиме «выключен», тоже выключается. При выходе из программы микрофоны включаются обратно.
- Один файл `.exe` (~110 КБ), ничего устанавливать не нужно.

## Установка

1. Скачайте `MuteMyMic.exe` со страницы [Releases](../../releases).
2. Положите его в любую постоянную папку (например, `Документы\Mute my Mic`) и запустите.
3. При первом запуске выберите язык.
4. Иконка появится в трее возле часов. Если её не видно — нажмите стрелку `^` и перетащите иконку на панель задач.
5. Правый клик по иконке → **Настройки**.

> Windows SmartScreen может предупредить, что программа «неизвестного издателя» — это нормально для неподписанных программ. Нажмите «Подробнее» → «Выполнить в любом случае».

## Сборка из исходников

Ничего, кроме Windows 10/11, не нужно — используется компилятор C#, встроенный в .NET Framework 4.8:

```bat
build.bat
```

Готовый файл: `bin\MuteMyMic.exe`.

## Ограничения

- Если активно окно программы, запущенной **от имени администратора**, Windows не передаёт горячую клавишу обычным программам. Выход — запустить Mute my Mic тоже от администратора.
- Значок не виден поверх игр в эксклюзивном полноэкранном режиме (в оконном/безрамочном — виден).
- Пока программа запущена, выбранная клавиша или кнопка мыши работает только для микрофона и не выполняет своё обычное действие в других программах.
- Настройки хранятся в `%APPDATA%\MuteMyMic\settings.ini`, журнал ошибок — в `%APPDATA%\MuteMyMic\error.log` (если что-то пошло не так, приложите его к сообщению об ошибке).

---

## English

Mute my Mic is a tiny Windows tray app that **mutes your microphone system-wide** with a global hotkey or mouse button, so it works in Google Meet, Discord, Teams, Zoom and everything else at once.

- Mutes/unmutes recording devices via the Windows Core Audio API; choose which microphones it controls.
- Global hotkeys **or mouse buttons** (middle / side): toggle (default **Ctrl + Alt + F12**), mute-only and unmute-only.
- Command line for Stream Deck, AutoHotkey or keyboard software: `MuteMyMic.exe --toggle`, `--mute`, `--unmute`.
- Always-on-top crossed-out mic indicator while muted — choose the monitor, corner and size, or drag it anywhere while Settings is open. Can be turned off.
- Optional click sound on mute/unmute, with volume control.
- Optional: mute when the PC is locked (Win + L), unmute again after unlocking.
- Optional: start muted when launched by hand. The PC itself always starts with microphones on — they are switched back on before shutdown.
- Follows mute changes made in Windows settings or other apps.
- Fully offline: no network requests, telemetry or update checks; no admin rights needed.
- Optional "Start with Windows".
- 13 languages, picked on first run and changeable in Settings.
- Tray icon: left click toggles, right click opens the menu.
- Mics plugged in while muted get muted too; microphones are switched back on when the app exits.
- Single ~110 KB `.exe`, no installer.

**Build:** run `build.bat` (uses the C# compiler that ships with .NET Framework 4.8 on every Windows 10/11).

## License

[MIT](LICENSE)
