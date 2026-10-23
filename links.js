// Shared, dependency-free URL helpers for the popup and the Kinopoisk page.
globalThis.KinoLinks = Object.freeze({
  toPlayerUrl(originalUrl) {
    let url;
    try {
      url = new URL(originalUrl);
    } catch {
      return null;
    }

    if (url.protocol !== 'https:' || !['kinopoisk.ru', 'www.kinopoisk.ru'].includes(url.hostname)) {
      return null;
    }

    if (!/^\/(?:film|series)\/\d+\/?$/.test(url.pathname)) {
      return null;
    }

    return `https://sspoisk.ru${url.pathname}`;
  },

  toSearchUrl(query) {
    const term = query.trim();
    return term ? `https://www.kinopoisk.ru/index.php?kp_query=${encodeURIComponent(term)}` : null;
  },
});
