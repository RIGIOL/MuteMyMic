using System;
using System.Collections.Generic;
using System.Globalization;

namespace MuteMyMic
{
    // UI translations. Every entry lists the text in the same order as Codes.
    // A missing or empty translation falls back to English.
    static class L
    {
        public static readonly string[] Codes =
            { "en", "ru", "uk", "de", "fr", "es", "pt", "it", "pl", "tr", "zh", "ja", "ko" };

        public static readonly string[] NativeNames =
            { "English", "Русский", "Українська", "Deutsch", "Français", "Español", "Português",
              "Italiano", "Polski", "Türkçe", "中文（简体）", "日本語", "한국어" };

        static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>();
        static int current;

        public static string Current
        {
            get { return Codes[current]; }
        }

        public static int IndexOf(string code)
        {
            return Array.IndexOf(Codes, code);
        }

        public static void Set(string code)
        {
            int i = IndexOf(code);
            current = i >= 0 ? i : 0;
        }

        /// <summary>The Windows display language if we have it, otherwise English.</summary>
        public static string SystemLanguage()
        {
            string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return IndexOf(two) >= 0 ? two : "en";
        }

        public static string Get(string key)
        {
            string[] v;
            if (!Texts.TryGetValue(key, out v)) return key;
            string s = current < v.Length ? v[current] : null;
            return string.IsNullOrEmpty(s) ? v[0] : s;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        static void A(string key, params string[] v)
        {
            Texts[key] = v;
        }

        static L()
        {
            //   en, ru, uk, de, fr, es, pt, it, pl, tr, zh, ja, ko
            A("already_running",
                "Mute my Mic is already running — look for its icon in the tray (next to the clock).",
                "Программа Mute my Mic уже запущена — ищите значок в трее (возле часов).",
                "Mute my Mic уже запущено — шукайте значок у треї (біля годинника).",
                "Mute my Mic läuft bereits – das Symbol finden Sie im Infobereich (neben der Uhr).",
                "Mute my Mic est déjà lancé — cherchez son icône dans la zone de notification (près de l'horloge).",
                "Mute my Mic ya se está ejecutando: busque su icono en la bandeja del sistema (junto al reloj).",
                "O Mute my Mic já está em execução — procure o ícone na bandeja do sistema (perto do relógio).",
                "Mute my Mic è già in esecuzione: cerca l'icona nell'area di notifica (vicino all'orologio).",
                "Mute my Mic już działa — poszukaj ikony w zasobniku systemowym (obok zegara).",
                "Mute my Mic zaten çalışıyor — simgesini sistem tepsisinde (saatin yanında) bulabilirsiniz.",
                "Mute my Mic 已在运行——请在系统托盘（时钟旁边）查找它的图标。",
                "Mute my Mic はすでに実行中です。通知領域（時計の横）のアイコンを確認してください。",
                "Mute my Mic이 이미 실행 중입니다. 알림 영역(시계 옆)에서 아이콘을 확인하세요.");

            A("menu.mute", "Mute microphone", "Выключить микрофон", "Вимкнути мікрофон", "Mikrofon stummschalten",
                "Couper le micro", "Silenciar micrófono", "Silenciar microfone", "Disattiva microfono",
                "Wycisz mikrofon", "Mikrofonu kapat", "关闭麦克风", "マイクをミュート", "마이크 음소거");
            A("menu.unmute", "Unmute microphone", "Включить микрофон", "Увімкнути мікрофон", "Mikrofon einschalten",
                "Réactiver le micro", "Activar micrófono", "Ativar microfone", "Riattiva microfono",
                "Włącz mikrofon", "Mikrofonu aç", "打开麦克风", "マイクのミュートを解除", "마이크 음소거 해제");
            A("menu.settings", "Settings...", "Настройки...", "Налаштування...", "Einstellungen...", "Paramètres...",
                "Configuración...", "Configurações...", "Impostazioni...", "Ustawienia...", "Ayarlar...",
                "设置...", "設定...", "설정...");
            A("menu.exit", "Exit", "Выход", "Вихід", "Beenden", "Quitter", "Salir", "Sair", "Esci", "Zakończ",
                "Çıkış", "退出", "終了", "종료");

            A("tip.muted", "mic MUTED", "микрофон ВЫКЛЮЧЕН", "мікрофон ВИМКНЕНО", "Mikrofon STUMM", "micro COUPÉ",
                "micrófono SILENCIADO", "microfone SILENCIADO", "microfono DISATTIVATO", "mikrofon WYCISZONY",
                "mikrofon KAPALI", "麦克风已关闭", "マイク ミュート中", "마이크 음소거됨");
            A("tip.live", "mic on", "микрофон включён", "мікрофон увімкнено", "Mikrofon an", "micro actif",
                "micrófono activo", "microfone ativo", "microfono attivo", "mikrofon włączony", "mikrofon açık",
                "麦克风已打开", "マイク オン", "마이크 켜짐");

            A("err.hotkey_busy",
                "\"{0}\" is already used by another program. Choose a different one in Settings.",
                "«{0}» уже занята другой программой. Выберите другую в настройках.",
                "«{0}» уже зайнята іншою програмою. Виберіть іншу в налаштуваннях.",
                "„{0}“ wird bereits von einem anderen Programm verwendet. Wählen Sie in den Einstellungen eine andere.",
                "« {0} » est déjà utilisé par un autre programme. Choisissez-en un autre dans les paramètres.",
                "«{0}» ya lo usa otro programa. Elija otro en la configuración.",
                "\"{0}\" já está sendo usado por outro programa. Escolha outro nas configurações.",
                "\"{0}\" è già usato da un altro programma. Scegline un altro nelle impostazioni.",
                "„{0}” jest już używany przez inny program. Wybierz inny w ustawieniach.",
                "\"{0}\" başka bir program tarafından kullanılıyor. Ayarlardan başka bir tuş seçin.",
                "“{0}”已被其他程序占用。请在设置中选择其他按键。",
                "「{0}」は他のプログラムで使用されています。設定で別のキーを選んでください。",
                "\"{0}\"은(는) 다른 프로그램에서 이미 사용 중입니다. 설정에서 다른 키를 선택하세요.");
            A("err.no_mics", "No microphones found.", "Микрофоны не найдены.", "Мікрофони не знайдено.",
                "Keine Mikrofone gefunden.", "Aucun micro trouvé.", "No se encontraron micrófonos.",
                "Nenhum microfone encontrado.", "Nessun microfono trovato.", "Nie znaleziono mikrofonów.",
                "Mikrofon bulunamadı.", "未找到麦克风。", "マイクが見つかりません。", "마이크를 찾을 수 없습니다.");
            A("err.audio_busy",
                "Windows audio is not responding. Try again in a few seconds.",
                "Звук Windows не отвечает. Попробуйте ещё раз через несколько секунд.",
                "Звук Windows не відповідає. Спробуйте ще раз за кілька секунд.",
                "Windows-Audio reagiert nicht. Versuchen Sie es in ein paar Sekunden erneut.",
                "L'audio de Windows ne répond pas. Réessayez dans quelques secondes.",
                "El audio de Windows no responde. Inténtelo de nuevo en unos segundos.",
                "O áudio do Windows não está respondendo. Tente novamente em alguns segundos.",
                "L'audio di Windows non risponde. Riprova tra qualche secondo.",
                "Dźwięk systemu Windows nie odpowiada. Spróbuj ponownie za kilka sekund.",
                "Windows ses hizmeti yanıt vermiyor. Birkaç saniye sonra yeniden deneyin.",
                "Windows 音频没有响应。请几秒钟后再试。",
                "Windows のオーディオが応答していません。数秒後にもう一度お試しください。",
                "Windows 오디오가 응답하지 않습니다. 잠시 후 다시 시도하세요.");
            A("err.generic", "Error: {0}", "Ошибка: {0}", "Помилка: {0}", "Fehler: {0}", "Erreur : {0}",
                "Error: {0}", "Erro: {0}", "Errore: {0}", "Błąd: {0}", "Hata: {0}", "错误：{0}", "エラー: {0}", "오류: {0}");
            A("err.save", "Could not save settings: {0}", "Не удалось сохранить настройки: {0}",
                "Не вдалося зберегти налаштування: {0}", "Einstellungen konnten nicht gespeichert werden: {0}",
                "Impossible d'enregistrer les paramètres : {0}", "No se pudo guardar la configuración: {0}",
                "Não foi possível salvar as configurações: {0}", "Impossibile salvare le impostazioni: {0}",
                "Nie udało się zapisać ustawień: {0}", "Ayarlar kaydedilemedi: {0}", "无法保存设置：{0}",
                "設定を保存できませんでした: {0}", "설정을 저장할 수 없습니다: {0}");

            A("started",
                "Mute my Mic is running. Press {0} to mute or unmute the microphone.",
                "Mute my Mic работает. Нажмите {0}, чтобы выключить или включить микрофон.",
                "Mute my Mic працює. Натисніть {0}, щоб вимкнути або увімкнути мікрофон.",
                "Mute my Mic läuft. Drücken Sie {0}, um das Mikrofon stumm- oder einzuschalten.",
                "Mute my Mic est lancé. Appuyez sur {0} pour couper ou réactiver le micro.",
                "Mute my Mic está en marcha. Pulse {0} para silenciar o activar el micrófono.",
                "O Mute my Mic está em execução. Pressione {0} para silenciar ou ativar o microfone.",
                "Mute my Mic è attivo. Premi {0} per disattivare o riattivare il microfono.",
                "Mute my Mic działa. Naciśnij {0}, aby wyciszyć lub włączyć mikrofon.",
                "Mute my Mic çalışıyor. Mikrofonu kapatmak veya açmak için {0} tuşuna basın.",
                "Mute my Mic 正在运行。按 {0} 关闭或打开麦克风。",
                "Mute my Mic が起動しました。{0} でマイクのミュートを切り替えます。",
                "Mute my Mic이 실행 중입니다. {0}을(를) 눌러 마이크를 끄거나 켜세요.");

            A("set.title", "Settings", "Настройки", "Налаштування", "Einstellungen", "Paramètres", "Configuración",
                "Configurações", "Impostazioni", "Ustawienia", "Ayarlar", "设置", "設定", "설정");
            A("set.language", "Language:", "Язык:", "Мова:", "Sprache:", "Langue :", "Idioma:", "Idioma:",
                "Lingua:", "Język:", "Dil:", "语言：", "言語:", "언어:");
            A("set.grp_hotkeys", "Hotkeys", "Горячие клавиши", "Гарячі клавіші", "Tastenkürzel", "Raccourcis",
                "Teclas de acceso rápido", "Teclas de atalho", "Tasti rapidi", "Skróty", "Kısayol tuşları",
                "快捷键", "ホットキー", "단축키");
            A("set.hk_toggle", "Mute / unmute:", "Выкл. / вкл.:", "Вимк. / увімк.:", "Stumm / an:",
                "Couper / réactiver :", "Silenciar / activar:", "Silenciar / ativar:", "Disattiva / riattiva:",
                "Wycisz / włącz:", "Kapat / aç:", "关闭 / 打开：", "ミュート切替:", "음소거 전환:");
            A("set.hk_mute", "Mute only:", "Только выключить:", "Лише вимкнути:", "Nur stumm:",
                "Couper seulement :", "Solo silenciar:", "Só silenciar:", "Solo disattiva:", "Tylko wycisz:",
                "Yalnızca kapat:", "仅关闭：", "ミュートのみ:", "음소거만:");
            A("set.hk_unmute", "Unmute only:", "Только включить:", "Лише увімкнути:", "Nur an:",
                "Réactiver seulement :", "Solo activar:", "Só ativar:", "Solo riattiva:", "Tylko włącz:",
                "Yalnızca aç:", "仅打开：", "解除のみ:", "음소거 해제만:");
            A("warn.dup_key",
                "\"{0}\" is already used for another action.",
                "«{0}» уже назначена на другое действие.",
                "«{0}» уже призначено на іншу дію.",
                "„{0}“ ist bereits einer anderen Aktion zugewiesen.",
                "« {0} » est déjà utilisé pour une autre action.",
                "«{0}» ya está asignada a otra acción.",
                "\"{0}\" já está atribuída a outra ação.",
                "\"{0}\" è già assegnato a un'altra azione.",
                "„{0}” jest już przypisany do innej akcji.",
                "\"{0}\" zaten başka bir işleve atanmış.",
                "“{0}”已分配给其他操作。",
                "「{0}」は別の操作に割り当て済みです。",
                "\"{0}\"은(는) 이미 다른 동작에 지정되어 있습니다.");
            A("set.volume", "Volume:", "Громкость:", "Гучність:", "Lautstärke:", "Volume :", "Volumen:", "Volume:",
                "Volume:", "Głośność:", "Ses düzeyi:", "音量：", "音量:", "볼륨:");
            A("corner.custom", "Custom position", "Своё место", "Власне місце", "Eigene Position",
                "Position personnalisée", "Posición personalizada", "Posição personalizada", "Posizione personalizzata",
                "Własne położenie", "Özel konum", "自定义位置", "カスタム位置", "사용자 지정 위치");
            A("set.drag_hint",
                "While this window is open, you can drag the icon on the screen with the mouse.",
                "Пока это окно открыто, значок на экране можно перетащить мышью.",
                "Поки це вікно відкрите, значок на екрані можна перетягнути мишею.",
                "Solange dieses Fenster geöffnet ist, können Sie das Symbol mit der Maus verschieben.",
                "Tant que cette fenêtre est ouverte, vous pouvez déplacer l'icône avec la souris.",
                "Mientras esta ventana esté abierta, puede arrastrar el icono con el ratón.",
                "Enquanto esta janela estiver aberta, você pode arrastar o ícone com o mouse.",
                "Finché questa finestra è aperta, puoi trascinare l'icona con il mouse.",
                "Gdy to okno jest otwarte, możesz przeciągnąć ikonę myszą.",
                "Bu pencere açıkken simgeyi fareyle sürükleyebilirsiniz.",
                "此窗口打开时，可以用鼠标拖动屏幕上的图标。",
                "このウィンドウを開いている間は、アイコンをマウスでドラッグできます。",
                "이 창이 열려 있는 동안 마우스로 아이콘을 끌어 옮길 수 있습니다.");
            A("set.start_muted",
                "Mute the microphone when Mute my Mic is started by hand (when started with Windows, the microphone is always on)",
                "Выключать микрофон при запуске Mute my Mic вручную (при запуске вместе с Windows микрофон всегда включён)",
                "Вимикати мікрофон під час запуску Mute my Mic вручну (під час запуску разом з Windows мікрофон завжди увімкнено)",
                "Mikrofon stummschalten, wenn Mute my Mic von Hand gestartet wird (beim Start mit Windows ist das Mikrofon immer an)",
                "Couper le micro quand Mute my Mic est lancé manuellement (au démarrage de Windows, le micro est toujours actif)",
                "Silenciar el micrófono al abrir Mute my Mic manualmente (al iniciarse con Windows, el micrófono siempre está activo)",
                "Silenciar o microfone ao abrir o Mute my Mic manualmente (ao iniciar com o Windows, o microfone fica sempre ativo)",
                "Disattiva il microfono quando Mute my Mic viene avviato manualmente (all'avvio con Windows il microfono è sempre attivo)",
                "Wyciszaj mikrofon po ręcznym uruchomieniu Mute my Mic (przy starcie z Windows mikrofon jest zawsze włączony)",
                "Mute my Mic elle başlatıldığında mikrofonu kapat (Windows ile başladığında mikrofon her zaman açıktır)",
                "手动启动 Mute my Mic 时关闭麦克风（随 Windows 启动时麦克风始终打开）",
                "Mute my Mic を手動で起動したときにマイクをミュートする（Windows と同時に起動したときは常にマイク オン）",
                "Mute my Mic을 직접 실행할 때 마이크 끄기 (Windows와 함께 시작될 때는 마이크가 항상 켜짐)");
            A("set.version", "Version {0}", "Версия {0}", "Версія {0}", "Version {0}", "Version {0}", "Versión {0}",
                "Versão {0}", "Versione {0}", "Wersja {0}", "Sürüm {0}", "版本 {0}", "バージョン {0}", "버전 {0}");
            A("set.clear", "Clear", "Очистить", "Очистити", "Löschen", "Effacer", "Borrar", "Limpar", "Cancella",
                "Wyczyść", "Temizle", "清除", "クリア", "지우기");
            A("set.hotkey_hint",
                "Click the box, then press a key combination or a mouse button (middle or side).",
                "Кликните в поле и нажмите сочетание клавиш или кнопку мыши (среднюю или боковую).",
                "Клацніть у полі й натисніть сполучення клавіш або кнопку миші (середню чи бічну).",
                "Klicken Sie in das Feld und drücken Sie eine Tastenkombination oder eine Maustaste (Mitte oder Seite).",
                "Cliquez dans le champ, puis appuyez sur une combinaison de touches ou un bouton de souris (central ou latéral).",
                "Haga clic en el campo y pulse una combinación de teclas o un botón del ratón (central o lateral).",
                "Clique no campo e pressione uma combinação de teclas ou um botão do mouse (do meio ou lateral).",
                "Fai clic nel campo e premi una combinazione di tasti o un pulsante del mouse (centrale o laterale).",
                "Kliknij pole i naciśnij kombinację klawiszy lub przycisk myszy (środkowy lub boczny).",
                "Kutuya tıklayın, ardından bir tuş kombinasyonuna veya fare düğmesine (orta ya da yan) basın.",
                "点击输入框，然后按下组合键或鼠标按键（中键或侧键）。",
                "欄をクリックしてから、キーの組み合わせかマウスボタン（中央またはサイド）を押してください。",
                "상자를 클릭한 다음 키 조합이나 마우스 버튼(가운데 또는 측면)을 누르세요.");
            A("set.sound",
                "Play a sound when the microphone is muted or unmuted",
                "Звук при выключении и включении микрофона",
                "Звук під час вимкнення та увімкнення мікрофона",
                "Ton beim Stumm- und Einschalten des Mikrofons",
                "Jouer un son quand le micro est coupé ou réactivé",
                "Reproducir un sonido al silenciar o activar el micrófono",
                "Tocar um som ao silenciar ou ativar o microfone",
                "Riproduci un suono quando il microfono viene disattivato o riattivato",
                "Odtwarzaj dźwięk przy wyciszaniu i włączaniu mikrofonu",
                "Mikrofon kapatılıp açıldığında ses çal",
                "关闭或打开麦克风时播放提示音",
                "マイクのミュート切り替え時に音を鳴らす",
                "마이크를 끄거나 켤 때 소리 재생");

            A("set.grp_overlay", "On-screen icon", "Значок на экране", "Значок на екрані", "Bildschirmsymbol",
                "Icône à l'écran", "Icono en pantalla", "Ícone na tela", "Icona sullo schermo", "Ikona na ekranie",
                "Ekran simgesi", "屏幕图标", "画面上のアイコン", "화면 아이콘");
            A("set.show_overlay", "Show the icon while the microphone is muted",
                "Показывать значок, пока микрофон выключен",
                "Показувати значок, поки мікрофон вимкнено",
                "Symbol anzeigen, solange das Mikrofon stumm ist",
                "Afficher l'icône quand le micro est coupé",
                "Mostrar el icono mientras el micrófono está silenciado",
                "Mostrar o ícone enquanto o microfone estiver silenciado",
                "Mostra l'icona mentre il microfono è disattivato",
                "Pokazuj ikonę, gdy mikrofon jest wyciszony",
                "Mikrofon kapalıyken simgeyi göster",
                "麦克风关闭时显示图标",
                "ミュート中はアイコンを表示する",
                "마이크가 꺼져 있을 때 아이콘 표시");
            A("set.monitor", "Monitor:", "Монитор:", "Монітор:", "Bildschirm:", "Écran :", "Monitor:", "Monitor:",
                "Monitor:", "Monitor:", "Monitör:", "显示器：", "モニター:", "모니터:");
            A("set.monitor_primary", "Primary monitor", "Основной монитор", "Основний монітор", "Hauptbildschirm",
                "Écran principal", "Monitor principal", "Monitor principal", "Monitor principale", "Monitor główny",
                "Ana monitör", "主显示器", "メイン モニター", "주 모니터");
            A("set.monitor_n", "Monitor {0} ({1}×{2})", "Монитор {0} ({1}×{2})", "Монітор {0} ({1}×{2})",
                "Bildschirm {0} ({1}×{2})", "Écran {0} ({1}×{2})", "Monitor {0} ({1}×{2})", "Monitor {0} ({1}×{2})",
                "Monitor {0} ({1}×{2})", "Monitor {0} ({1}×{2})", "Monitör {0} ({1}×{2})", "显示器 {0}（{1}×{2}）",
                "モニター {0}（{1}×{2}）", "모니터 {0} ({1}×{2})");
            A("set.corner", "Corner:", "Угол экрана:", "Кут екрана:", "Ecke:", "Coin :", "Esquina:", "Canto:",
                "Angolo:", "Narożnik:", "Köşe:", "位置：", "位置:", "위치:");
            A("corner.tl", "Top left", "Левый верхний", "Лівий верхній", "Oben links", "En haut à gauche",
                "Arriba a la izquierda", "Superior esquerdo", "In alto a sinistra", "Lewy górny", "Sol üst",
                "左上", "左上", "왼쪽 위");
            A("corner.tr", "Top right", "Правый верхний", "Правий верхній", "Oben rechts", "En haut à droite",
                "Arriba a la derecha", "Superior direito", "In alto a destra", "Prawy górny", "Sağ üst",
                "右上", "右上", "오른쪽 위");
            A("corner.bl", "Bottom left", "Левый нижний", "Лівий нижній", "Unten links", "En bas à gauche",
                "Abajo a la izquierda", "Inferior esquerdo", "In basso a sinistra", "Lewy dolny", "Sol alt",
                "左下", "左下", "왼쪽 아래");
            A("corner.br", "Bottom right", "Правый нижний", "Правий нижній", "Unten rechts", "En bas à droite",
                "Abajo a la derecha", "Inferior direito", "In basso a destra", "Prawy dolny", "Sağ alt",
                "右下", "右下", "오른쪽 아래");
            A("set.size", "Size:", "Размер:", "Розмір:", "Größe:", "Taille :", "Tamaño:", "Tamanho:",
                "Dimensione:", "Rozmiar:", "Boyut:", "大小：", "サイズ:", "크기:");
            A("size.s", "Small", "Маленький", "Малий", "Klein", "Petite", "Pequeño", "Pequeno", "Piccola", "Mały",
                "Küçük", "小", "小", "작게");
            A("size.m", "Medium", "Средний", "Середній", "Mittel", "Moyenne", "Mediano", "Médio", "Media", "Średni",
                "Orta", "中", "中", "보통");
            A("size.l", "Large", "Большой", "Великий", "Groß", "Grande", "Grande", "Grande", "Grande", "Duży",
                "Büyük", "大", "大", "크게");

            A("set.grp_mics", "Microphones to mute", "Какие микрофоны выключать", "Які мікрофони вимикати",
                "Stummzuschaltende Mikrofone", "Micros à couper", "Micrófonos que silenciar",
                "Microfones a silenciar", "Microfoni da disattivare", "Mikrofony do wyciszenia",
                "Kapatılacak mikrofonlar", "要关闭的麦克风", "ミュートするマイク", "음소거할 마이크");
            A("set.mics_hint",
                "Unchecked microphones are left alone. Newly connected microphones are muted too.",
                "Микрофоны без галочки программа не трогает. Новые подключённые микрофоны тоже выключаются.",
                "Мікрофони без позначки програма не чіпає. Нові підключені мікрофони теж вимикаються.",
                "Nicht markierte Mikrofone bleiben unverändert. Neu angeschlossene Mikrofone werden ebenfalls stummgeschaltet.",
                "Les micros non cochés ne sont pas modifiés. Les micros branchés ensuite sont aussi coupés.",
                "Los micrófonos sin marcar no se tocan. Los que se conecten después también se silencian.",
                "Microfones desmarcados não são alterados. Microfones conectados depois também são silenciados.",
                "I microfoni non selezionati non vengono toccati. Anche quelli collegati in seguito vengono disattivati.",
                "Nieoznaczone mikrofony pozostają bez zmian. Nowo podłączone mikrofony też są wyciszane.",
                "İşaretlenmemiş mikrofonlara dokunulmaz. Sonradan bağlanan mikrofonlar da kapatılır.",
                "未勾选的麦克风不受影响。新连接的麦克风也会被关闭。",
                "チェックを外したマイクは操作しません。新しく接続したマイクもミュートされます。",
                "체크하지 않은 마이크는 건드리지 않습니다. 새로 연결한 마이크도 음소거됩니다.");
            A("set.grp_other", "Other", "Прочее", "Інше", "Sonstiges", "Autres", "Otros", "Outros", "Altro",
                "Inne", "Diğer", "其他", "その他", "기타");
            A("set.lock",
                "Mute the microphone when the computer is locked (Win + L) and turn it back on after unlocking",
                "Выключать микрофон при блокировке компьютера (Win + L) и включать обратно после разблокировки",
                "Вимикати мікрофон під час блокування комп'ютера (Win + L) і вмикати знову після розблокування",
                "Mikrofon beim Sperren des Computers (Win + L) stummschalten und nach dem Entsperren wieder einschalten",
                "Couper le micro au verrouillage de l'ordinateur (Win + L) et le réactiver au déverrouillage",
                "Silenciar el micrófono al bloquear el equipo (Win + L) y activarlo de nuevo al desbloquear",
                "Silenciar o microfone ao bloquear o computador (Win + L) e reativá-lo ao desbloquear",
                "Disattiva il microfono quando il computer viene bloccato (Win + L) e riattivalo allo sblocco",
                "Wyciszaj mikrofon po zablokowaniu komputera (Win + L) i włączaj go po odblokowaniu",
                "Bilgisayar kilitlendiğinde (Win + L) mikrofonu kapat, kilit açılınca yeniden aç",
                "锁定电脑时（Win + L）关闭麦克风，解锁后重新打开",
                "PC のロック時（Win + L）にマイクをミュートし、ロック解除後に元に戻す",
                "컴퓨터를 잠글 때(Win + L) 마이크를 끄고 잠금 해제 후 다시 켜기");
            A("set.autostart", "Start with Windows", "Запускать вместе с Windows", "Запускати разом з Windows",
                "Mit Windows starten", "Lancer au démarrage de Windows", "Iniciar con Windows",
                "Iniciar com o Windows", "Avvia con Windows", "Uruchamiaj razem z systemem Windows",
                "Windows ile başlat", "开机时自动启动", "Windows の起動時に開始", "Windows 시작 시 실행");
            A("btn.cancel", "Cancel", "Отмена", "Скасувати", "Abbrechen", "Annuler", "Cancelar", "Cancelar",
                "Annulla", "Anuluj", "İptal", "取消", "キャンセル", "취소");

            A("key.none", "(none)", "(не задана)", "(не задано)", "(keine)", "(aucun)", "(ninguna)", "(nenhuma)",
                "(nessuno)", "(brak)", "(yok)", "（无）", "（なし）", "(없음)");
            A("key.mouse_middle", "Middle mouse button", "Средняя кнопка мыши", "Середня кнопка миші",
                "Mittlere Maustaste", "Bouton central de la souris", "Botón central del ratón",
                "Botão do meio do mouse", "Pulsante centrale del mouse", "Środkowy przycisk myszy",
                "Farenin orta düğmesi", "鼠标中键", "マウスの中央ボタン", "마우스 가운데 버튼");
            A("key.mouse_x1", "Mouse side button (Back)", "Боковая кнопка мыши («Назад»)",
                "Бічна кнопка миші («Назад»)", "Maus-Seitentaste (Zurück)", "Bouton latéral de la souris (Précédent)",
                "Botón lateral del ratón (Atrás)", "Botão lateral do mouse (Voltar)",
                "Pulsante laterale del mouse (Indietro)", "Boczny przycisk myszy (Wstecz)",
                "Fare yan düğmesi (Geri)", "鼠标侧键（后退）", "マウスのサイドボタン（戻る）", "마우스 측면 버튼(뒤로)");
            A("key.mouse_x2", "Mouse side button (Forward)", "Боковая кнопка мыши («Вперёд»)",
                "Бічна кнопка миші («Вперед»)", "Maus-Seitentaste (Vorwärts)", "Bouton latéral de la souris (Suivant)",
                "Botón lateral del ratón (Adelante)", "Botão lateral do mouse (Avançar)",
                "Pulsante laterale del mouse (Avanti)", "Boczny przycisk myszy (Dalej)",
                "Fare yan düğmesi (İleri)", "鼠标侧键（前进）", "マウスのサイドボタン（進む）", "마우스 측면 버튼(앞으로)");

            A("warn.typing_key",
                "This key is used for typing. Add Ctrl, Alt or Shift, or choose a separate key (F1–F24, Pause, Scroll Lock, Insert) or a mouse button.",
                "Эта клавиша нужна для набора текста. Добавьте Ctrl, Alt или Shift либо выберите отдельную клавишу (F1–F24, Pause, Scroll Lock, Insert) или кнопку мыши.",
                "Ця клавіша потрібна для набору тексту. Додайте Ctrl, Alt або Shift чи виберіть окрему клавішу (F1–F24, Pause, Scroll Lock, Insert) або кнопку миші.",
                "Diese Taste wird zum Tippen gebraucht. Fügen Sie Ctrl, Alt oder Shift hinzu oder wählen Sie eine eigene Taste (F1–F24, Pause, Scroll Lock, Insert) oder eine Maustaste.",
                "Cette touche sert à la saisie. Ajoutez Ctrl, Alt ou Shift, ou choisissez une touche à part (F1–F24, Pause, Scroll Lock, Insert) ou un bouton de souris.",
                "Esta tecla se usa para escribir. Añada Ctrl, Alt o Shift, o elija una tecla aparte (F1–F24, Pause, Scroll Lock, Insert) o un botón del ratón.",
                "Esta tecla é usada para digitar. Adicione Ctrl, Alt ou Shift, ou escolha uma tecla separada (F1–F24, Pause, Scroll Lock, Insert) ou um botão do mouse.",
                "Questo tasto serve per scrivere. Aggiungi Ctrl, Alt o Shift, oppure scegli un tasto a parte (F1–F24, Pause, Scroll Lock, Insert) o un pulsante del mouse.",
                "Ten klawisz służy do pisania. Dodaj Ctrl, Alt lub Shift albo wybierz osobny klawisz (F1–F24, Pause, Scroll Lock, Insert) lub przycisk myszy.",
                "Bu tuş yazı yazmak için kullanılıyor. Ctrl, Alt veya Shift ekleyin ya da ayrı bir tuş (F1–F24, Pause, Scroll Lock, Insert) veya bir fare düğmesi seçin.",
                "此按键用于打字。请加上 Ctrl、Alt 或 Shift，或选择单独的按键（F1–F24、Pause、Scroll Lock、Insert）或鼠标按键。",
                "このキーは文字入力に使われます。Ctrl・Alt・Shift を組み合わせるか、単独のキー（F1～F24、Pause、Scroll Lock、Insert）またはマウスボタンを選んでください。",
                "이 키는 입력에 사용됩니다. Ctrl, Alt 또는 Shift를 함께 쓰거나 별도의 키(F1–F24, Pause, Scroll Lock, Insert) 또는 마우스 버튼을 선택하세요.");
            A("warn.steals_key",
                "While Mute my Mic is running, \"{0}\" will only mute the microphone and will stop doing its usual job in other programs. Use it anyway?",
                "Пока Mute my Mic запущена, «{0}» будет только выключать микрофон и перестанет выполнять своё обычное действие в других программах. Всё равно использовать?",
                "Поки Mute my Mic запущено, «{0}» лише вимикатиме мікрофон і перестане виконувати свою звичайну дію в інших програмах. Все одно використати?",
                "Solange Mute my Mic läuft, schaltet „{0}“ nur das Mikrofon stumm und erfüllt in anderen Programmen nicht mehr seine normale Funktion. Trotzdem verwenden?",
                "Tant que Mute my Mic est lancé, « {0} » servira uniquement à couper le micro et ne fera plus son action habituelle dans les autres programmes. L'utiliser quand même ?",
                "Mientras Mute my Mic esté en marcha, «{0}» solo silenciará el micrófono y dejará de hacer su función habitual en otros programas. ¿Usarla de todos modos?",
                "Enquanto o Mute my Mic estiver em execução, \"{0}\" só vai silenciar o microfone e deixará de fazer sua função normal em outros programas. Usar mesmo assim?",
                "Finché Mute my Mic è in esecuzione, \"{0}\" disattiverà solo il microfono e smetterà di svolgere la sua funzione abituale negli altri programmi. Usarlo comunque?",
                "Dopóki Mute my Mic działa, „{0}” będzie tylko wyciszać mikrofon i przestanie działać jak zwykle w innych programach. Użyć mimo to?",
                "Mute my Mic çalıştığı sürece \"{0}\" yalnızca mikrofonu kapatacak ve diğer programlardaki normal işlevini yapmayacak. Yine de kullanılsın mı?",
                "Mute my Mic 运行期间，“{0}”只会用来关闭麦克风，在其他程序中将不再执行原来的功能。仍要使用吗？",
                "Mute my Mic の実行中、「{0}」はマイクのミュート専用になり、他のプログラムでの本来の動作をしなくなります。それでも使用しますか？",
                "Mute my Mic이 실행 중인 동안 \"{0}\"은(는) 마이크 음소거에만 쓰이며 다른 프로그램에서 원래 기능을 하지 않습니다. 그래도 사용할까요?");
            A("ask.save", "Save the changes?", "Сохранить изменения?", "Зберегти зміни?", "Änderungen speichern?",
                "Enregistrer les modifications ?", "¿Guardar los cambios?", "Salvar as alterações?",
                "Salvare le modifiche?", "Zapisać zmiany?", "Değişiklikler kaydedilsin mi?", "保存更改吗？",
                "変更を保存しますか？", "변경 내용을 저장할까요?");

            A("first.welcome", "Welcome to Mute my Mic!", "Добро пожаловать в Mute my Mic!",
                "Ласкаво просимо до Mute my Mic!", "Willkommen bei Mute my Mic!", "Bienvenue dans Mute my Mic !",
                "¡Bienvenido a Mute my Mic!", "Bem-vindo ao Mute my Mic!", "Benvenuto in Mute my Mic!",
                "Witamy w Mute my Mic!", "Mute my Mic'e hoş geldiniz!", "欢迎使用 Mute my Mic！",
                "Mute my Mic へようこそ！", "Mute my Mic에 오신 것을 환영합니다!");
            A("first.choose_lang", "Choose a language:", "Выберите язык:", "Виберіть мову:", "Sprache wählen:",
                "Choisissez une langue :", "Elija un idioma:", "Escolha um idioma:", "Scegli una lingua:",
                "Wybierz język:", "Bir dil seçin:", "选择语言：", "言語を選択:", "언어 선택:");
            A("first.continue", "Continue", "Продолжить", "Продовжити", "Weiter", "Continuer", "Continuar",
                "Continuar", "Continua", "Dalej", "Devam", "继续", "続行", "계속");
            A("first.later", "You can change all of this later in Settings.",
                "Всё это можно поменять потом в настройках.",
                "Усе це можна змінити пізніше в налаштуваннях.",
                "Alles lässt sich später in den Einstellungen ändern.",
                "Vous pourrez tout modifier plus tard dans les paramètres.",
                "Puede cambiar todo esto más tarde en la configuración.",
                "Você pode mudar tudo isso depois nas configurações.",
                "Puoi cambiare tutto più tardi nelle impostazioni.",
                "Wszystko to można później zmienić w ustawieniach.",
                "Bunların hepsini daha sonra Ayarlar'dan değiştirebilirsiniz.",
                "以后可以在设置中更改这些选项。",
                "これらは後から設定で変更できます。",
                "나중에 설정에서 모두 변경할 수 있습니다.");
        }
    }
}
