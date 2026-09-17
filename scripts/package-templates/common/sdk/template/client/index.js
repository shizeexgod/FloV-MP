// Клиентская часть вашего ресурса. Выполняется у каждого игрока.
// API: alt (события, окна WebView) и natives (функции GTA V).
import * as alt from 'alt-client';

alt.log('[Gamemode] клиентский скрипт загружен');

// Событие с сервера (GamemodeResource.cs, команда /hello).
alt.onServer('gamemode:notify', (text) => {
    alt.log(`[Gamemode] ${text}`);
});

// Пример: клавиша F6 отправляет событие на сервер.
alt.on('keyup', (key) => {
    if (key === 117) alt.emitServer('gamemode:hello', 'нажата F6');
});
