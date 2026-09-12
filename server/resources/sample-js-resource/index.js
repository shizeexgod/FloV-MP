import * as alt from 'alt-server';

alt.log('[FloV:MP Node.js Engine] JavaScript resource initialized successfully!');

alt.on('playerConnect', (player) => {
    alt.log(`[JS Resource] Player connected: ${player.name} (ID: ${player.id})`);
    
    // Спавн игрока через Node.js API alt:V
    player.spawn(198.8, -935.6, 30.7, 0);
    player.model = 'mp_m_freemode_01';
    player.health = 200;
    player.armour = 100;

    alt.emitClient(player, 'chat:addMessage', '{ff3d8a}[FloV:MP JS Engine]{ffffff} Сервер поддерживает Node.js и C# одновременно!');
});

alt.onClient('chat:message', (player, msg) => {
    alt.log(`[JS Chat] [${player.id}] ${player.name}: ${msg}`);
});
