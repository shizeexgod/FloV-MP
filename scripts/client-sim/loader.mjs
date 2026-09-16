// Подмена модулей alt:V для симуляции клиентского скрипта вне игры.
// Импорты 'alt-client' и 'natives' перенаправляются на локальные заглушки.

import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));

const STUBS = {
    'alt-client': pathToFileURL(path.join(here, 'alt-client.mjs')).href,
    'natives': pathToFileURL(path.join(here, 'natives.mjs')).href,
};

export async function resolve(specifier, context, nextResolve) {
    if (STUBS[specifier]) {
        return { url: STUBS[specifier], shortCircuit: true };
    }
    return nextResolve(specifier, context);
}
