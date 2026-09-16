// Заглушка модуля 'natives'.
//
// Любая нативная функция существует и ничего не делает. Исключения —
// функции, от ответа которых зависит логика входа: без них спавн никогда не
// «завершился» бы, и симуляция проверяла бы не то.

const handler = {
    get(target, prop) {
        if (prop in target) return target[prop];
        return () => undefined;
    },
};

const base = {
    // Земля найдена сразу — спавн завершается на первой итерации опроса.
    getGroundZFor3dCoord: (x, y, z) => [true, z - 14],
    hasCollisionLoadedAroundEntity: () => true,
    getGameTimer: () => Date.now(),
    isControlPressed: () => false,
    isControlJustPressed: () => false,
    isDisabledControlPressed: () => false,
    getEntityCoords: () => ({ x: 0, y: 0, z: 0 }),
    getGameplayCamRot: () => ({ x: 0, y: 0, z: 0 }),
};

const proxy = new Proxy(base, handler);

// Модуль экспортирует функции по именам; через Proxy отдаём любые.
export default proxy;
export const {
    getGroundZFor3dCoord, hasCollisionLoadedAroundEntity, getGameTimer,
    isControlPressed, isControlJustPressed, isDisabledControlPressed,
    getEntityCoords, getGameplayCamRot,
} = base;

// Остальные имена, которые встречаются в клиентском скрипте, объявляются
// явно: ESM не умеет «любой экспорт по запросу». Список пополняется, если
// скрипт начнёт использовать новые нативы.
const noop = () => undefined;
export const displayRadar = noop, displayHud = noop, doScreenFadeOut = noop,
    doScreenFadeIn = noop, freezeEntityPosition = noop, loadScene = noop,
    requestCollisionAtCoord = noop, setFocusPosAndVel = noop, clearFocus = noop,
    setEntityCoordsNoOffset = noop, setEntityVelocity = noop,
    clearPedTasksImmediately = noop, setRunSprintMultiplierForPlayer = noop,
    setPedCanRagdoll = noop, setWeatherTypeOverTimePersist = noop,
    setClockTime = noop, setEntityInvincible = noop, setPlayerInvincible = noop,
    setEntityCollision = noop, setEntityVisible = noop, setEntityAlpha = noop,
    resetEntityAlpha = noop, disableControlAction = noop, enableControlAction = noop,
    setPedMoveRateOverride = noop, drawRect = noop, beginTextCommandDisplayText = noop,
    endTextCommandDisplayText = noop, addTextComponentSubstringPlayerName = noop,
    setTextFont = noop, setTextScale = noop, setTextColour = noop,
    setTextOutline = noop, setTextCentre = noop, setTextDropShadow = noop,
    setEntityHeading = noop, getEntityHeading = () => 0, setVehicleEngineOn = noop,
    setVehicleDoorsLocked = noop, setVehicleFixed = noop, setVehicleDeformationFixed = noop,
    setVehicleUndriveable = noop, setPedConfigFlag = noop, getPedConfigFlag = () => false,
    setPlayerControl = noop, networkIsGameInProgress = () => true, setCamActive = noop,
    renderScriptCams = noop, destroyCam = noop, createCam = () => 1,
    setCamCoord = noop, setCamRot = noop, pointCamAtEntity = noop, getGroundZExcludingObjectsFor3dCoord = () => [true, 0],
    startAudioScene = noop, stopAudioScene = noop, playSoundFrontend = noop,
    getEntitySpeed = () => 0, isPedInAnyVehicle = () => false,
    getVehiclePedIsIn = () => 0, setPedToRagdoll = noop,
    getScreenCoordFromWorldCoord = () => [false, 0, 0], setDrawOrigin = noop,
    clearDrawOrigin = noop, drawMarker = noop, setMinimapComponent = noop,
    setBigmapActive = noop, hideHudComponentThisFrame = noop,
    getFirstBlipInfoId = () => 0, doesBlipExist = () => false, getBlipInfoIdCoord = () => ({ x: 0, y: 0, z: 0 });
