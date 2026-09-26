// Замените этот файл своим серверным ресурсом RAGE:MP.
mp.events.add('playerJoin', (player) => {
  player.call('flovmp:welcome', [`Привет, ${player.name}!`]);
});
