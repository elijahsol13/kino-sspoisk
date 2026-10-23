document.getElementById('search-form').addEventListener('submit', (event) => {
  event.preventDefault();
  const query = document.getElementById('search-input').value;
  const searchUrl = KinoLinks.toSearchUrl(query);
  if (!searchUrl) return;

  chrome.tabs.create({ url: searchUrl });
  window.close();
});
