(() => {
  const buttonId = 'rusty-kino-watch-button';
  let lastUrl = '';

  function syncButton() {
    lastUrl = location.href;
    const playerUrl = KinoLinks.toPlayerUrl(lastUrl);
    const currentButton = document.getElementById(buttonId);

    if (!playerUrl) {
      currentButton?.remove();
      return;
    }

    if (currentButton) {
      currentButton.href = playerUrl;
      return;
    }

    if (!document.body) return;

    const button = document.createElement('a');
    button.id = buttonId;
    button.href = playerUrl;
    button.target = '_blank';
    button.rel = 'noopener noreferrer';
    button.textContent = '▶ Смотреть онлайн';
    button.title = 'Открыть на kinokino.win в новой вкладке';
    button.setAttribute('aria-label', 'Смотреть онлайн на kinokino.win (новая вкладка)');
    document.body.appendChild(button);
  }

  syncButton();

  // Kinopoisk may navigate between cards without refreshing the document.
  // We only touch the DOM when the URL changed or the site removed our button.
  const observer = new MutationObserver(() => {
    if (location.href !== lastUrl || (KinoLinks.toPlayerUrl(location.href) && !document.getElementById(buttonId))) {
      syncButton();
    }
  });
  observer.observe(document.documentElement, { childList: true, subtree: true });

  addEventListener('popstate', syncButton);
  addEventListener('hashchange', syncButton);
})();
