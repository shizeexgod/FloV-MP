/**
 * FloV:MP — клиентская точка входа (js-module / alt-client).
 *
 * Фаза 1: только диагностика.
 *   - лог при полном подключении к серверу;
 *   - приём события flovmp:client:welcome от C#-геймода (round-trip C# → JS);
 *   - попытка показать нативное уведомление, обёрнута в try/catch — если
 *     сигнатуры нативов разъедутся после патча GTA V, ресурс всё равно
 *     работает, а не падает.
 *
 * Никакой геймплейной логики здесь пока нет: взаимную видимость и движение
 * двух игроков обеспечивает сетевой движок alt:V сам, как только сервер
 * их заспавнил.
 */

import * as alt from 'alt-client';
import * as native from 'natives';

alt.log('[FloV:MP] client: ресурс flovmp-client загружен');

alt.on('connectionComplete', () => {
    alt.log('[FloV:MP] client: connectionComplete — вошли на сервер');
});

alt.on('disconnect', () => {
    alt.log('[FloV:MP] client: disconnect');
});

alt.onServer('flovmp:client:welcome', (name, index) => {
    alt.log(`[FloV:MP] client: welcome "${name}", точка спавна #${index}`);

    try {
        native.beginTextCommandThefeedPost('STRING');
        native.addTextComponentSubstringPlayerName(`FloV:MP — добро пожаловать, ${name}`);
        native.endTextCommandThefeedPostTicker(false, true);
    } catch (err) {
        alt.log(`[FloV:MP] client: нативное уведомление недоступно: ${err}`);
    }
});
