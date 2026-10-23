// Native universal macOS installer, no third-party dependencies.
#import <Cocoa/Cocoa.h>

static BOOL ask(NSString *title, NSString *detail, NSString *button, BOOL cancellable) {
    NSAlert *alert = [[NSAlert alloc] init];
    alert.messageText = title;
    alert.informativeText = detail;
    alert.alertStyle = NSAlertStyleInformational;
    [alert addButtonWithTitle:button];
    if (cancellable) [alert addButtonWithTitle:@"Отмена"];
    return [alert runModal] == NSAlertFirstButtonReturn;
}

static void copyPath(NSString *path) {
    NSPasteboard *pasteboard = [NSPasteboard generalPasteboard];
    [pasteboard clearContents];
    [pasteboard setString:path forType:NSPasteboardTypeString];
}

static BOOL openBrowser(NSString *appName, NSString *url) {
    NSTask *task = [[NSTask alloc] init];
    task.executableURL = [NSURL fileURLWithPath:@"/usr/bin/open"];
    task.arguments = @[@"-a", appName, url];
    NSError *error = nil;
    if (![task launchAndReturnError:&error]) return NO;
    [task waitUntilExit];
    return task.terminationStatus == 0;
}

int main(int argc, const char *argv[]) {
    @autoreleasepool {
        [NSApplication sharedApplication];
        [NSApp setActivationPolicy:NSApplicationActivationPolicyRegular];
        [NSApp activateIgnoringOtherApps:YES];

        if (!ask(@"🍿 Kino → SSpoisk",
                 @"Установим кнопку для фильмов и сериалов за пару шагов.\n\nМастер сам сохранит расширение в постоянной папке, а затем покажет, куда нажать в браузере.",
                 @"Далее →", YES)) return 0;

        NSArray<NSDictionary *> *browsers = @[
            @{@"name": @"Brave", @"app": @"Brave Browser", @"url": @"brave://extensions", @"id": @"com.brave.Browser"},
            @{@"name": @"Google Chrome", @"app": @"Google Chrome", @"url": @"chrome://extensions", @"id": @"com.google.Chrome"},
            @{@"name": @"Яндекс Браузер", @"app": @"Yandex", @"url": @"browser://extensions", @"id": @"ru.yandex.desktop.yandex-browser"},
            @{@"name": @"Microsoft Edge", @"app": @"Microsoft Edge", @"url": @"edge://extensions", @"id": @"com.microsoft.edgemac"},
            @{@"name": @"Opera", @"app": @"Opera", @"url": @"opera://extensions", @"id": @"com.operasoftware.Opera"},
            @{@"name": @"Firefox", @"app": @"Firefox", @"url": @"about:debugging#/runtime/this-firefox", @"id": @"org.mozilla.firefox"}
        ];

        NSAlert *chooser = [[NSAlert alloc] init];
        chooser.messageText = @"Выберите браузер";
        chooser.informativeText = @"В каком браузере вы будете пользоваться расширением?";
        [chooser addButtonWithTitle:@"Далее →"];
        [chooser addButtonWithTitle:@"Отмена"];
        NSPopUpButton *menu = [[NSPopUpButton alloc] initWithFrame:NSMakeRect(0, 0, 320, 30) pullsDown:NO];
        for (NSDictionary *browser in browsers) [menu addItemWithTitle:browser[@"name"]];
        NSURL *defaultAppURL = [[NSWorkspace sharedWorkspace] URLForApplicationToOpenURL:[NSURL URLWithString:@"https://www.kinopoisk.ru"]];
        NSString *defaultId = [[NSBundle bundleWithURL:defaultAppURL] bundleIdentifier];
        if (defaultId) {
            for (NSUInteger i = 0; i < browsers.count; i++) {
                if ([defaultId isEqualToString:browsers[i][@"id"]]) [menu selectItemAtIndex:i];
            }
        }
        chooser.accessoryView = menu;
        if ([chooser runModal] != NSAlertFirstButtonReturn) return 0;
        NSDictionary *chosen = browsers[menu.indexOfSelectedItem];

        NSFileManager *fm = [NSFileManager defaultManager];
        NSURL *folder = [[fm homeDirectoryForCurrentUser] URLByAppendingPathComponent:@"Kino-SSpoisk/Extension" isDirectory:YES];
        NSURL *sourceFolder = [[[NSBundle mainBundle] resourceURL] URLByAppendingPathComponent:@"extension" isDirectory:YES];
        NSArray<NSString *> *assets = @[@"manifest.json", @"icon.png", @"links.js", @"popup.html", @"popup.js", @"content.js", @"content.css"];
        NSError *error = nil;
        BOOL success = [fm createDirectoryAtURL:folder withIntermediateDirectories:YES attributes:nil error:&error];
        if (success) {
            for (NSString *name in assets) {
                NSURL *src = [sourceFolder URLByAppendingPathComponent:name];
                NSURL *dst = [folder URLByAppendingPathComponent:name];
                if (![fm fileExistsAtPath:src.path]) {
                    error = [NSError errorWithDomain:@"KinoSSpoisk" code:1 userInfo:@{NSLocalizedDescriptionKey: [NSString stringWithFormat:@"В установщике отсутствует файл %@", name]}];
                    success = NO;
                    break;
                }
                if ([fm fileExistsAtPath:dst.path] && ![fm removeItemAtURL:dst error:&error]) { success = NO; break; }
                if (![fm copyItemAtURL:src toURL:dst error:&error]) { success = NO; break; }
            }
        }
        if (!success) {
            ask(@"Не удалось сохранить расширение", error.localizedDescription ?: @"Нет доступа к папке пользователя.", @"Закрыть", NO);
            return 1;
        }

        copyPath(folder.path);
        if (!openBrowser(chosen[@"app"], chosen[@"url"])) {
            copyPath(chosen[@"url"]);
            ask(@"Откройте браузер", [NSString stringWithFormat:@"Не удалось открыть %@ автоматически.\n\nЗапустите этот браузер и вставьте в адресную строку:\n%@\n\nАдрес скопирован.", chosen[@"name"], chosen[@"url"]], @"Продолжить", NO);
            copyPath(folder.path);
        }

        if ([chosen[@"name"] isEqualToString:@"Firefox"]) {
            ask(@"Шаг 1 из 2 — Firefox", @"✅ Файлы уже сохранены!\n\nВ Firefox откройте «Этот Firefox» → «Загрузить временное дополнение».\n\nFirefox удалит это дополнение после перезапуска. Для постоянной установки требуется подпись Mozilla.", @"Далее →", NO);
            [[NSWorkspace sharedWorkspace] activateFileViewerSelectingURLs:@[[folder URLByAppendingPathComponent:@"manifest.json"]]];
            ask(@"Шаг 2 из 2 — выберите файл", @"Выберите manifest.json из папки, показанной в Finder.\n\nЕсли нужно, нажмите ⌘⇧G в окне выбора файла, вставьте адрес папки (он скопирован), нажмите Enter и выберите manifest.json.", @"Готово", NO);
        } else {
            ask(@"Шаг 1 из 2 — включите режим разработчика", @"✅ Файлы уже сохранены!\n\nВ открывшейся вкладке «Расширения» включите «Режим разработчика» (Developer mode). Обычно переключатель находится справа вверху.\n\nЗатем вернитесь в это окно и нажмите «Далее».", @"Далее →", NO);
            [[NSWorkspace sharedWorkspace] openURL:folder];
            ask(@"Шаг 2 из 2 — выберите папку", @"В браузере нажмите «Загрузить распакованное расширение» (Load unpacked).\n\nВыберите открытую в Finder папку Extension. Если её не видно, нажмите ⌘⇧G, вставьте скопированный путь к папке и нажмите Enter.\n\nВ Яндекс Браузере установка локальных расширений может быть ограничена.", @"Готово", NO);
        }
        ask(@"🍿 Всё готово!", @"Откройте карточку фильма или сериала на Кинопоиске: справа сверху появится кнопка «▶ Смотреть онлайн».\n\nЗначок Kino → SSpoisk можно закрепить рядом с адресной строкой и искать фильмы через него.", @"Закрыть", NO);
    }
    return 0;
}
