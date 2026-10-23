// Kino → SSpoisk — native Windows GUI installer, compiled as Windows EXE.
// Requires only .NET Framework 4.7.2+ (included in supported Windows 10/11).
// KinoExtension ZIP is embedded into the EXE as a managed resource.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KinoSSpoisk {
    internal sealed class Browser {
        public string Name;
        public string Exe;
        public string Url;
        public string[] Locations;

        public Browser(string name, string exe, string url, params string[] locations) {
            Name = name; Exe = exe; Url = url; Locations = locations;
        }
        public override string ToString() { return Name; }
    }

    internal static class Installer {
        internal static readonly string[] Files = {
            "manifest.json", "icon.png", "links.js", "popup.html", "popup.js", "content.js", "content.css"
        };
        internal static readonly string Target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KinoSSpoisk", "Extension");

        // The embedded ZIP is the same user-downloadable archive (root folder Kino-SSpoisk).
        internal static ZipArchive OpenArchive() {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KinoExtension");
            if (stream == null) throw new InvalidDataException("В установщике отсутствуют файлы расширения.");
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }

        internal static void CheckArchive() {
            using (ZipArchive zip = OpenArchive()) {
                foreach (string name in Files) {
                    ZipArchiveEntry entry = zip.GetEntry("Kino-SSpoisk/" + name);
                    if (entry == null) throw new InvalidDataException("Нет файла: " + name);
                    using (Stream input = entry.Open()) {
                        byte[] buffer = new byte[8192];
                        while (input.Read(buffer, 0, buffer.Length) > 0) { }
                    }
                }
            }
        }

        internal static void Install() {
            // Extract only the 7 known files. Do not extract arbitrary ZIP entry names.
            Directory.CreateDirectory(Target);
            using (ZipArchive zip = OpenArchive()) {
                foreach (string name in Files) {
                    ZipArchiveEntry entry = zip.GetEntry("Kino-SSpoisk/" + name);
                    if (entry == null) throw new InvalidDataException("Не найден файл " + name);
                    string destination = Path.Combine(Target, name);
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write)) {
                        input.CopyTo(output);
                    }
                }
            }
        }

        internal static string LocateBrowser(Browser browser) {
            foreach (RegistryKey root in new [] { Registry.CurrentUser, Registry.LocalMachine }) {
                try {
                    using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + browser.Exe)) {
                        if (key != null) {
                            string path = key.GetValue("") as string;
                            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
                        }
                    }
                } catch { /* Registry key may be inaccessible. */ }
            }
            string[] roots = {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            foreach (string root in roots) {
                if (string.IsNullOrEmpty(root)) continue;
                foreach (string location in browser.Locations) {
                    string candidate = Path.Combine(root, location);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return null;
        }

        internal static int GuessBrowser(List<Browser> browsers) {
            string progId = "";
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice")) {
                    if (key != null) progId = (key.GetValue("ProgId") as string ?? "").ToLowerInvariant();
                }
            } catch { }
            string[] hints = {"chrome", "brave", "yandex", "edge", "opera", "firefox"};
            for (int index = 0; index < browsers.Count; index++) {
                if (progId.Contains(hints[index])) return index;
            }
            for (int index = 0; index < browsers.Count; index++) {
                if (LocateBrowser(browsers[index]) != null) return index;
            }
            return 0;
        }

        internal static void ShowFolder(bool selectManifest) {
            string args = selectManifest
                ? "/select,\"" + Path.Combine(Target, "manifest.json") + "\""
                : "\"" + Target + "\"";
            Process.Start("explorer.exe", args);
        }
    }

    internal sealed class Wizard : Form {
        private readonly List<Browser> browsers = new List<Browser> {
            new Browser("Google Chrome", "chrome.exe", "chrome://extensions", @"Google\Chrome\Application\chrome.exe"),
            new Browser("Brave", "brave.exe", "brave://extensions", @"BraveSoftware\Brave-Browser\Application\brave.exe"),
            new Browser("Яндекс Браузер", "browser.exe", "browser://extensions", @"Yandex\YandexBrowser\Application\browser.exe"),
            new Browser("Microsoft Edge", "msedge.exe", "edge://extensions", @"Microsoft\Edge\Application\msedge.exe"),
            new Browser("Opera", "launcher.exe", "opera://extensions", @"Opera\launcher.exe", @"Opera Software\Opera Stable\launcher.exe"),
            new Browser("Firefox", "firefox.exe", "about:debugging#/runtime/this-firefox", @"Mozilla Firefox\firefox.exe")
        };

        private readonly Label heading = new Label();
        private readonly Label details = new Label();
        private readonly ComboBox browserList = new ComboBox();
        private readonly Button helper = new Button();
        private readonly Button back = new Button();
        private readonly Button next = new Button();
        private Browser selected;
        private int step;

        public Wizard() {
            Text = "🍿 Установка Kino → SSpoisk";
            ClientSize = new Size(570, 380);
            MinimumSize = MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);

            heading.SetBounds(22, 16, 520, 42);
            heading.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            Controls.Add(heading);

            details.SetBounds(22, 70, 520, 180);
            Controls.Add(details);

            browserList.SetBounds(22, 255, 335, 28);
            browserList.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (Browser browser in browsers) browserList.Items.Add(browser);
            browserList.SelectedIndex = Installer.GuessBrowser(browsers);
            Controls.Add(browserList);

            helper.SetBounds(22, 253, 260, 34);
            helper.Click += (sender, args) => {
                try { Installer.ShowFolder(selected != null && selected.Name == "Firefox"); }
                catch (Exception error) { MessageBox.Show(error.Message, "Не удалось открыть папку"); }
            };
            Controls.Add(helper);

            back.Text = "Назад";
            back.SetBounds(330, 325, 100, 32);
            back.Click += (sender, args) => { if (step > 0) step--; DrawStep(); };
            Controls.Add(back);

            next.SetBounds(440, 325, 105, 32);
            next.Click += NextClicked;
            Controls.Add(next);
            AcceptButton = next;
            DrawStep();
        }

        private void DrawStep() {
            back.Enabled = step > 0;
            browserList.Visible = step == 0;
            helper.Visible = step > 0;
            next.Text = step == 2 ? "Готово ✓" : "Далее →";
            if (step == 0) {
                heading.Text = "Добро пожаловать!";
                details.Text = "Установим кнопку для фильмов и сериалов за несколько шагов.\n\n" +
                    "Мастер сам сохранит расширение в постоянной папке. Вам останется выполнить две простые операции в браузере.\n\n" +
                    "Выберите браузер:";
            } else if (step == 1) {
                heading.Text = "Шаг 1 из 2 — настройки браузера";
                details.Text = selected.Name == "Firefox"
                    ? "✅ Файлы сохранены! В открытом Firefox перейдите в раздел «Этот Firefox» и нажмите «Загрузить временное дополнение».\n\nВажно: Firefox удалит дополнение после перезапуска. Для постоянного нужна подпись Mozilla."
                    : "✅ Файлы сохранены! В открывшейся вкладке «Расширения» включите «Режим разработчика» (Developer mode). Обычно переключатель находится справа сверху.\n\nЗатем вернитесь к этому окну и нажмите «Далее».";
                helper.Text = "Показать папку с файлами";
            } else {
                heading.Text = "Шаг 2 из 2 — добавьте расширение";
                details.Text = selected.Name == "Firefox"
                    ? "В Firefox выберите файл manifest.json из открытой папки.\n\nПапка расширения уже сохранена на компьютер. Её адрес скопирован и его можно вставить в окно выбора файла.\n\n" + Installer.Target
                    : "В браузере нажмите «Загрузить распакованное расширение» (Load unpacked) и выберите папку Extension.\n\nПапка открыта в Проводнике, её адрес скопирован в буфер обмена. Вставьте его в окно выбора папки.\n\n" + Installer.Target;
                helper.Text = "Показать папку в Проводнике";
            }
        }

        private void OpenBrowser() {
            string exe = Installer.LocateBrowser(selected);
            if (exe != null) {
                try {
                    Process.Start(exe, "\"" + selected.Url + "\"");
                    return;
                } catch { }
            }
            Clipboard.SetText(selected.Url);
            MessageBox.Show("Не получилось открыть " + selected.Name + " автоматически.\n\n" +
                "Запустите его и вставьте адрес из буфера обмена в адресную строку:\n" +
                selected.Url, "Откройте страницу расширений");
            Clipboard.SetText(Installer.Target);
        }

        private void NextClicked(object sender, EventArgs e) {
            if (step == 0) {
                selected = (Browser)browserList.SelectedItem;
                try {
                    Installer.Install();
                    Clipboard.SetText(Installer.Target);
                    OpenBrowser();
                } catch (Exception error) {
                    MessageBox.Show("Установка не завершена:\n" + error.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                step = 1;
            } else if (step == 1) {
                try { Installer.ShowFolder(selected.Name == "Firefox"); Clipboard.SetText(Installer.Target); }
                catch (Exception error) { MessageBox.Show(error.Message, "Не удалось открыть папку"); }
                step = 2;
            } else {
                MessageBox.Show("Откройте фильм или сериал на Кинопоиске — справа сверху будет кнопка «▶ Смотреть онлайн».\n\n" +
                    "Значок расширения можно закрепить рядом с адресной строкой браузера.", "Готово!");
                Close();
                return;
            }
            DrawStep();
        }
    }

    internal static class Program {
        [STAThread]
        private static int Main(string[] args) {
            // Non-interactive test for GitHub Actions Windows runner.
            if (args.Length == 1 && args[0] == "--selftest") {
                try { Installer.CheckArchive(); return 0; }
                catch { return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Wizard());
            return 0;
        }
    }
}
