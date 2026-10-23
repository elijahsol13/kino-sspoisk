const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = (file) => fs.readFileSync(path.join(__dirname, '..', file), 'utf8');

function execute(file, context) {
  vm.runInNewContext(source(file), context, { filename: file });
}

function links() {
  const context = { URL };
  execute('links.js', context);
  return context.KinoLinks;
}

test('строит ссылку плеера для фильма и сериала на Кинопоиске', () => {
  const { toPlayerUrl } = links();
  assert.equal(toPlayerUrl('https://www.kinopoisk.ru/film/123456/'), 'https://kinokino.win/film/123456/');
  assert.equal(toPlayerUrl('https://kinopoisk.ru/series/464963/'), 'https://kinokino.win/series/464963/');
  assert.equal(toPlayerUrl('https://www.kinopoisk.ru/film/42?utm_source=x#part'), 'https://kinokino.win/film/42');
});

test('не создает ссылку для посторонних адресов или не-карточек', () => {
  const { toPlayerUrl } = links();
  for (const url of [
    'https://notkinopoisk.ru/film/123/',
    'https://kinopoisk.ru.evil.example/film/123/',
    'http://kinopoisk.ru/film/123/',
    'https://www.kinopoisk.ru/',
    'https://www.kinopoisk.ru/film/123/cast/',
    'https://www.kinopoisk.ru/series/not-a-number/',
  ]) {
    assert.equal(toPlayerUrl(url), null, url);
  }
});

test('кодирует поиск на Кинопоиске и не ищет пустую строку', () => {
  const { toSearchUrl } = links();
  const url = new URL(toSearchUrl('  Дюна: Часть вторая  '));
  assert.equal(url.origin, 'https://www.kinopoisk.ru');
  assert.equal(url.pathname, '/index.php');
  assert.equal(url.searchParams.get('kp_query'), 'Дюна: Часть вторая');
  assert.equal(toSearchUrl('  '), null);
});

function fakePage(href) {
  const nodes = new Map();
  const listeners = new Map();
  let mutation;
  const body = {
    appendChild(node) {
      nodes.set(node.id, node);
      node.remove = () => nodes.delete(node.id);
      return node;
    },
  };
  const document = {
    body,
    documentElement: {},
    createElement: (tag) => ({
      tagName: tag.toUpperCase(),
      setAttribute(name, value) { this[name] = value; },
    }),
    getElementById: (id) => nodes.get(id) ?? null,
  };
  const location = { href };
  const context = {
    URL, document, location,
    addEventListener: (event, callback) => listeners.set(event, callback),
    MutationObserver: class {
      constructor(callback) { mutation = callback; }
      observe() {}
    },
  };
  execute('links.js', context);
  return { context, location, document, nodes, listeners, mutation: () => mutation() };
}

test('кнопка появляется на карточке и открывает адрес kinokino.win в новой вкладке', () => {
  const page = fakePage('https://www.kinopoisk.ru/film/123456/');
  execute('content.js', page.context);
  const anchor = page.document.getElementById('rusty-kino-watch-button');
  assert.ok(anchor);
  assert.equal(anchor.href, 'https://kinokino.win/film/123456/');
  assert.equal(anchor.target, '_blank');
  assert.match(anchor.rel, /noopener/);
});

test('кнопка обновляется и исчезает при SPA-переходах', () => {
  const page = fakePage('https://www.kinopoisk.ru/series/464963/');
  execute('content.js', page.context);
  assert.equal(page.nodes.size, 1);
  page.location.href = 'https://www.kinopoisk.ru/film/42/';
  page.mutation();
  assert.equal(page.document.getElementById('rusty-kino-watch-button').href, 'https://kinokino.win/film/42/');
  page.location.href = 'https://www.kinopoisk.ru/lists/';
  page.mutation();
  assert.equal(page.nodes.size, 0);
});

test('popup открывает поиск на Кинопоиске из введенного названия', () => {
  const listeners = {};
  const form = { addEventListener: (name, callback) => { listeners[name] = callback; } };
  const input = { value: '  Интерстеллар  ' };
  const opened = [];
  let closed = false;
  const context = {
    URL,
    document: { getElementById: (id) => id === 'search-form' ? form : input },
    chrome: { tabs: { create: (details) => opened.push(details.url) } },
    window: { close: () => { closed = true; } },
  };
  execute('links.js', context);
  execute('popup.js', context);
  let prevented = false;
  listeners.submit({ preventDefault: () => { prevented = true; } });
  assert.equal(prevented, true);
  assert.equal(new URL(opened[0]).searchParams.get('kp_query'), 'Интерстеллар');
  assert.equal(closed, true);
});
