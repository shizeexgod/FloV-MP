import * as alt from 'alt-server';
import { createRageCompat } from './compat.mjs';

globalThis.mp = createRageCompat(alt);
alt.log('[FloV:MP] RAGE:MP compatibility: events/players (experimental)');
await import('./gamemode/index.js');
