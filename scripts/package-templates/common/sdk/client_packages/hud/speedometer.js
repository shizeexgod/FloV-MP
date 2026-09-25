// Спидометр: показывается только в машине.

function draw(config) {
    const ped = mp.players.local.handle;
    if (!mp.game.ped.isPedInAnyVehicle(ped, false)) return;

    const vehicle = mp.game.ped.getVehiclePedIsIn(ped, false);
    const kmh = Math.round(mp.game.entity.getEntitySpeed(vehicle) * 3.6);

    // Подложка и число.
    mp.game.graphics.drawRect(0.9, 0.9, 0.08, 0.05, 10, 10, 14, 170);
    mp.game.graphics.drawText(`${kmh} km/h`, [0.9, 0.884], {
        font: 4,
        color: config.accent,
        scale: [0.5, 0.5],
    });
}

module.exports = { draw };
