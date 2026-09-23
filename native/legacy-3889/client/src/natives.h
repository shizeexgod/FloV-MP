// Сгенерировано tools/gen_natives.py — не править руками.
// Хэши — исходные хэши PC; ScriptHookV сам переводит их для 1.0.3889.0.
#pragma once
#include "invoke.h"

namespace n {

inline Player PLAYER_ID() { return nv::invoke<Player>(0x4F8644AF03D0E0D6); }
inline Ped PLAYER_PED_ID() { return nv::invoke<Ped>(0xD80958FC74E988A6); }
inline char* GET_PLAYER_NAME(Player player) { return nv::invoke<char*>(0x6D0DE6A7B5DA71F8, player); }
inline void SET_PLAYER_MODEL(Player player, Hash model) { nv::invoke<void>(0x00A1CADD00108836, player, model); }
inline void SET_PLAYER_CONTROL(Player player, BOOL toggle, int possiblyFlags) { nv::invoke<void>(0x8D32347D6D4C40A2, player, toggle, possiblyFlags); }
inline void SET_PLAYER_INVINCIBLE(Player player, BOOL toggle) { nv::invoke<void>(0x239528EACDC3E7DE, player, toggle); }
inline void SET_MAX_WANTED_LEVEL(int maxWantedLevel) { nv::invoke<void>(0xAA5F02DB48D704B9, maxWantedLevel); }
inline void CLEAR_PLAYER_WANTED_LEVEL(Player player) { nv::invoke<void>(0xB302540597885499, player); }
inline void SET_EVERYONE_IGNORE_PLAYER(Player player, BOOL toggle) { nv::invoke<void>(0x8EEDA153AD141BA4, player, toggle); }
inline void SET_POLICE_IGNORE_PLAYER(Player player, BOOL toggle) { nv::invoke<void>(0x32C62AA929C2DA6A, player, toggle); }
inline void SET_DISPATCH_COPS_FOR_PLAYER(Player player, BOOL toggle) { nv::invoke<void>(0xDB172424876553F4, player, toggle); }
inline void SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER(Player player, float multiplier) { nv::invoke<void>(0x6DB47AA77FD94E09, player, multiplier); }
inline BOOL IS_PLAYER_FREE_AIMING(Player player) { return nv::invoke<BOOL>(0x2E397FD2ECD37C87, player); }
inline BOOL GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(Player player, Entity* entity) { return nv::invoke<BOOL>(0x2975C866E6713290, player, entity); }
inline BOOL IS_PLAYER_SWITCH_IN_PROGRESS() { return nv::invoke<BOOL>(0xD9D2CFFF49FAB35F); }
inline void SET_PLAYER_WANTED_LEVEL(Player player, int wantedLevel, BOOL p2) { nv::invoke<void>(0x39FF19C64EF7DA5B, player, wantedLevel, p2); }
inline void SET_PLAYER_WANTED_LEVEL_NOW(Player player, BOOL p1) { nv::invoke<void>(0xE0A7D1E497FFCD6F, player, p1); }
inline Vector3 GET_ENTITY_COORDS(Entity entity, BOOL alive) { return nv::invoke<Vector3>(0x3FEF770D40960D5A, entity, alive); }
inline float GET_ENTITY_HEADING(Entity entity) { return nv::invoke<float>(0xE83D4F9BA2A38914, entity); }
inline void SET_ENTITY_HEADING(Entity entity, float heading) { nv::invoke<void>(0x8E2530AA8ADA980E, entity, heading); }
inline void SET_ENTITY_COORDS(Entity entity, float X, float Y, float Z, BOOL xAxis, BOOL yAxis, BOOL zAxis, BOOL p7) { nv::invoke<void>(0x06843DA7060A026B, entity, X, Y, Z, xAxis, yAxis, zAxis, p7); }
inline void SET_ENTITY_COORDS_NO_OFFSET(Entity entity, float X, float Y, float Z, BOOL p4, BOOL p5, BOOL p6) { nv::invoke<void>(0x239A3351AC1DA385, entity, X, Y, Z, p4, p5, p6); }
inline Vector3 GET_ENTITY_VELOCITY(Entity entity) { return nv::invoke<Vector3>(0x4805D2B1D8CF94A9, entity); }
inline void SET_ENTITY_VELOCITY(Entity entity, float x, float y, float z) { nv::invoke<void>(0x1C99BB7B6E96D16F, entity, x, y, z); }
inline Vector3 GET_ENTITY_ROTATION(Entity entity, int p1) { return nv::invoke<Vector3>(0xAFBD61CC738D9EB9, entity, p1); }
inline void SET_ENTITY_ROTATION(Entity entity, float pitch, float roll, float yaw, int p4, BOOL p5) { nv::invoke<void>(0x8524A8B0171D5E07, entity, pitch, roll, yaw, p4, p5); }
inline int GET_ENTITY_HEALTH(Entity entity) { return nv::invoke<int>(0xEEF059FAD016D209, entity); }
inline void SET_ENTITY_HEALTH(Entity entity, int health) { nv::invoke<void>(0x6B76DC1F3AE6E6A3, entity, health); }
inline int GET_ENTITY_MAX_HEALTH(Entity entity) { return nv::invoke<int>(0x15D757606D170C3C, entity); }
inline void SET_ENTITY_MAX_HEALTH(Entity entity, int value) { nv::invoke<void>(0x166E7CF68597D8B5, entity, value); }
inline BOOL IS_ENTITY_DEAD(Entity entity) { return nv::invoke<BOOL>(0x5F9532F3B5CC2551, entity); }
inline BOOL DOES_ENTITY_EXIST(Entity entity) { return nv::invoke<BOOL>(0x7239B21A38F536BA, entity); }
inline void DELETE_ENTITY(Entity* entity) { nv::invoke<void>(0xAE3CBE5BF394C9C9, entity); }
inline void SET_ENTITY_AS_MISSION_ENTITY(Entity entity, BOOL value, BOOL byThisScript) { nv::invoke<void>(0xAD738C3085FE7E11, entity, value, byThisScript); }
inline void SET_ENTITY_INVINCIBLE(Entity entity, BOOL toggle) { nv::invoke<void>(0x3882114BDE571AD4, entity, toggle); }
inline void FREEZE_ENTITY_POSITION(Entity entity, BOOL toggle) { nv::invoke<void>(0x428CA6DBD1094446, entity, toggle); }
inline void SET_ENTITY_COLLISION(Entity entity, BOOL toggle, BOOL keepPhysics) { nv::invoke<void>(0x1A9205C1B9EE827F, entity, toggle, keepPhysics); }
inline void SET_ENTITY_VISIBLE(Entity entity, BOOL toggle, BOOL unkb) { nv::invoke<void>(0xEA1C610A04DB6BBB, entity, toggle, unkb); }
inline void SET_ENTITY_ALPHA(Entity entity, int alphaLevel, BOOL skin) { nv::invoke<void>(0x44A0870B7E92D7C0, entity, alphaLevel, skin); }
inline void RESET_ENTITY_ALPHA(Entity entity) { nv::invoke<void>(0x9B1E824FFBB7027A, entity); }
inline BOOL HAS_ENTITY_BEEN_DAMAGED_BY_ENTITY(Entity entity1, Entity entity2, BOOL p2) { return nv::invoke<BOOL>(0xC86D67D52A707CF8, entity1, entity2, p2); }
inline void CLEAR_ENTITY_LAST_DAMAGE_ENTITY(Entity entity) { nv::invoke<void>(0xA72CD9CA74A5ECBA, entity); }
inline float GET_ENTITY_SPEED(Entity entity) { return nv::invoke<float>(0xD5037BA82E12416F, entity); }
inline Hash GET_ENTITY_MODEL(Entity entity) { return nv::invoke<Hash>(0x9F47B058362C84B5, entity); }
inline void SET_ENTITY_PROOFS(Entity entity, BOOL bulletProof, BOOL fireProof, BOOL explosionProof, BOOL collisionProof, BOOL meleeProof, BOOL p6, BOOL p7, BOOL drownProof) { nv::invoke<void>(0xFAEE099C6F890BB8, entity, bulletProof, fireProof, explosionProof, collisionProof, meleeProof, p6, p7, drownProof); }
inline void SET_ENTITY_CAN_BE_DAMAGED(Entity entity, BOOL toggle) { nv::invoke<void>(0x1760FFA8AB074D66, entity, toggle); }
inline void SET_ENTITY_DYNAMIC(Entity entity, BOOL toggle) { nv::invoke<void>(0x1718DE8E3F2823CA, entity, toggle); }
inline void SET_ENTITY_LOAD_COLLISION_FLAG(Entity entity, BOOL toggle) { nv::invoke<void>(0x0DC7CABAB1E9B67E, entity, toggle); }
inline void SET_ENTITY_NO_COLLISION_ENTITY(Entity entity1, Entity entity2, BOOL toggle) { nv::invoke<void>(0xA53ED5520C07654A, entity1, entity2, toggle); }
inline Vector3 GET_ENTITY_FORWARD_VECTOR(Entity entity) { return nv::invoke<Vector3>(0x0A794A5A57F8DF91, entity); }
inline Vector3 GET_OFFSET_FROM_ENTITY_IN_WORLD_COORDS(Entity entity, float offsetX, float offsetY, float offsetZ) { return nv::invoke<Vector3>(0x1899F328B0E12848, entity, offsetX, offsetY, offsetZ); }
inline void SET_ENTITY_AS_NO_LONGER_NEEDED(Entity* entity) { nv::invoke<void>(0xB736A491E64A32CF, entity); }
inline float GET_ENTITY_HEIGHT_ABOVE_GROUND(Entity entity) { return nv::invoke<float>(0x1DD55701034110E5, entity); }
inline BOOL IS_ENTITY_IN_WATER(Entity entity) { return nv::invoke<BOOL>(0xCFB0A0D8EDD145A3, entity); }
inline void SET_ENTITY_HAS_GRAVITY(Entity entity, BOOL toggle) { nv::invoke<void>(0x4A4722448F18EEF5, entity, toggle); }
inline BOOL IS_PED_IN_ANY_VEHICLE(Ped ped, BOOL atGetIn) { return nv::invoke<BOOL>(0x997ABD671D25CA0B, ped, atGetIn); }
inline Vehicle GET_VEHICLE_PED_IS_IN(Ped ped, BOOL getLastVehicle) { return nv::invoke<Vehicle>(0x9A9112A0FE9A4713, ped, getLastVehicle); }
inline BOOL IS_PED_SHOOTING(Ped ped) { return nv::invoke<BOOL>(0x34616828CD07F1A1, ped); }
inline BOOL IS_PED_DUCKING(Ped ped) { return nv::invoke<BOOL>(0xD125AE748725C6BC, ped); }
inline BOOL IS_PED_JUMPING(Ped ped) { return nv::invoke<BOOL>(0xCEDABC5900A0BF97, ped); }
inline BOOL IS_PED_RAGDOLL(Ped ped) { return nv::invoke<BOOL>(0x47E4E977581C5B55, ped); }
inline Hash GET_PED_CAUSE_OF_DEATH(Ped ped) { return nv::invoke<Hash>(0x16FFE42AB2D2DC59, ped); }
inline Entity GET_PED_SOURCE_OF_DEATH(Ped ped) { return nv::invoke<Entity>(0x93C8B64DEB84728C, ped); }
inline Ped CREATE_PED(int pedType, Hash modelHash, float x, float y, float z, float heading, BOOL networkHandle, BOOL pedHandle) { return nv::invoke<Ped>(0xD49F9B0955C367DE, pedType, modelHash, x, y, z, heading, networkHandle, pedHandle); }
inline void SET_PED_INTO_VEHICLE(Ped ped, Vehicle vehicle, int seatIndex) { nv::invoke<void>(0xF75B0D629E1C063D, ped, vehicle, seatIndex); }
inline void TASK_WARP_PED_INTO_VEHICLE(Ped ped, Vehicle vehicle, int seat) { nv::invoke<void>(0x9A7D091411C5F684, ped, vehicle, seat); }
inline void SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(Ped ped, BOOL toggle) { nv::invoke<void>(0x9F8AA94D6D97DBF4, ped, toggle); }
inline void SET_PED_CAN_RAGDOLL(Ped ped, BOOL toggle) { nv::invoke<void>(0xB128377056A54E2A, ped, toggle); }
inline void SET_PED_CONFIG_FLAG(Ped ped, int flagId, BOOL value) { nv::invoke<void>(0x1913FE4CBF41C463, ped, flagId, value); }
inline void TASK_GO_STRAIGHT_TO_COORD(Ped ped, float x, float y, float z, float speed, int timeout, float targetHeading, float distanceToSlide) { nv::invoke<void>(0xD76B57B44F1E6F8B, ped, x, y, z, speed, timeout, targetHeading, distanceToSlide); }
inline void TASK_STAND_STILL(Ped ped, int time) { nv::invoke<void>(0x919BE13EED931959, ped, time); }
inline void CLEAR_PED_TASKS(Ped ped) { nv::invoke<void>(0xE1EF3C1216AFF2CD, ped); }
inline void CLEAR_PED_TASKS_IMMEDIATELY(Ped ped) { nv::invoke<void>(0xAAA34F8A7CB32098, ped); }
inline void TASK_AIM_GUN_AT_COORD(Ped ped, float x, float y, float z, int time, BOOL p5, BOOL p6) { nv::invoke<void>(0x6671F3EEC681BDA1, ped, x, y, z, time, p5, p6); }
inline void TASK_SHOOT_AT_COORD(Ped ped, float x, float y, float z, int duration, Hash firingPattern) { nv::invoke<void>(0x46A6CC01E0826106, ped, x, y, z, duration, firingPattern); }
inline void SET_PED_DEFAULT_COMPONENT_VARIATION(Ped ped) { nv::invoke<void>(0x45EEE61580806D63, ped); }
inline void GIVE_WEAPON_TO_PED(Player ped, Hash weaponHash, int ammoCount, BOOL p4, BOOL equipNow) { nv::invoke<void>(0xBF0FD6E56C964FCB, ped, weaponHash, ammoCount, p4, equipNow); }
inline void SET_CURRENT_PED_WEAPON(Ped ped, Hash weaponHash, BOOL equipNow) { nv::invoke<void>(0xADF692B254977C0C, ped, weaponHash, equipNow); }
inline Hash GET_SELECTED_PED_WEAPON(Ped ped) { return nv::invoke<Hash>(0x0A6DB4965674D243, ped); }
inline void REMOVE_ALL_PED_WEAPONS(Ped ped, BOOL toggle) { nv::invoke<void>(0xF25DF915FA38C5F3, ped, toggle); }
inline void SET_PED_ARMOUR(Ped ped, int amount) { nv::invoke<void>(0xCEA04D83135264CC, ped, amount); }
inline int GET_PED_ARMOUR(Ped ped) { return nv::invoke<int>(0x9483AF821605B1D8, ped); }
inline void SET_PED_CAN_BE_TARGETTED(Ped ped, BOOL toggle) { nv::invoke<void>(0x63F58F7C80513AAD, ped, toggle); }
inline void SET_PED_SUFFERS_CRITICAL_HITS(Ped ped, BOOL toggle) { nv::invoke<void>(0xEBD76F2359F190AC, ped, toggle); }
inline void SET_PED_DIES_WHEN_INJURED(Ped ped, BOOL toggle) { nv::invoke<void>(0x5BA7919BED300023, ped, toggle); }
inline void SET_PED_KEEP_TASK(Ped ped, BOOL toggle) { nv::invoke<void>(0x971D38760FBC02EF, ped, toggle); }
inline void SET_PED_CAN_SWITCH_WEAPON(Ped ped, BOOL toggle) { nv::invoke<void>(0xED7F7EFE9FABF340, ped, toggle); }
inline void SET_PED_RELATIONSHIP_GROUP_HASH(Ped ped, Hash hash) { nv::invoke<void>(0xC80A74AC829DDD92, ped, hash); }
inline Any ADD_RELATIONSHIP_GROUP(char* name, Hash* groupHash) { return nv::invoke<Any>(0xF372BC22FCB88606, name, groupHash); }
inline void SET_RELATIONSHIP_BETWEEN_GROUPS(int relationship, Hash group1, Hash group2) { nv::invoke<void>(0xBF25EB89375A37AD, relationship, group1, group2); }
inline void SET_PED_AMMO(Ped ped, Hash weaponHash, int ammo) { nv::invoke<void>(0x14E56BC5B5DB6A19, ped, weaponHash, ammo); }
inline Ped GET_PED_IN_VEHICLE_SEAT(Vehicle vehicle, int index) { return nv::invoke<Ped>(0xBB40DD2270B65366, vehicle, index); }
inline BOOL IS_PED_DEAD_OR_DYING(Ped ped, BOOL p1) { return nv::invoke<BOOL>(0x3317DEDB88C95038, ped, p1); }
inline void TASK_ENTER_VEHICLE(Ped ped, Vehicle vehicle, int timeout, int seat, float speed, int p5, Any p6) { nv::invoke<void>(0xC20E50AA46D09CA8, ped, vehicle, timeout, seat, speed, p5, p6); }
inline void TASK_LEAVE_VEHICLE(Ped ped, Vehicle vehicle, int flags) { nv::invoke<void>(0xD3DBCE61A490BE02, ped, vehicle, flags); }
inline Vehicle GET_VEHICLE_PED_IS_TRYING_TO_ENTER(Ped ped) { return nv::invoke<Vehicle>(0x814FA8BE5449445D, ped); }
inline int GET_SEAT_PED_IS_TRYING_TO_ENTER(Ped ped) { return nv::invoke<int>(0x6F4C85ACD641BCD2, ped); }
inline BOOL IS_PED_GETTING_INTO_A_VEHICLE(Ped ped) { return nv::invoke<BOOL>(0xBB062B2B5722478E, ped); }
inline void SET_PED_CAN_BE_DRAGGED_OUT(Ped ped, BOOL toggle) { nv::invoke<void>(0xC1670E958EEE24E5, ped, toggle); }
inline void SET_PED_CAN_BE_KNOCKED_OFF_VEHICLE(Ped ped, int state) { nv::invoke<void>(0x7A6535691B477C48, ped, state); }
inline BOOL IS_PED_RELOADING(Ped ped) { return nv::invoke<BOOL>(0x24B100C68C645951, ped); }
inline void SET_PED_INFINITE_AMMO_CLIP(Ped ped, BOOL toggle) { nv::invoke<void>(0x183DADC6AA953186, ped, toggle); }
inline void SET_PED_DESIRED_HEADING(Ped ped, float heading) { nv::invoke<void>(0xAA5A7ECE2AA8FE70, ped, heading); }
inline void SET_PED_FLEE_ATTRIBUTES(Ped ped, Any p1, BOOL p2) { nv::invoke<void>(0x70A2D1137C8ED7C9, ped, p1, p2); }
inline void SET_PED_COMBAT_ATTRIBUTES(Ped ped, int attributeIndex, BOOL enabled) { nv::invoke<void>(0x9F7794730795E019, ped, attributeIndex, enabled); }
inline void TASK_SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(Ped ped, BOOL toggle) { nv::invoke<void>(0x90D2156198831D69, ped, toggle); }
inline void SET_PED_MOVE_RATE_OVERRIDE(Ped ped, float value) { nv::invoke<void>(0x085BF80FA50A39D1, ped, value); }
inline void SET_PED_GRAVITY(Ped ped, BOOL toggle) { nv::invoke<void>(0x9FF447B6B6AD960A, ped, toggle); }
inline Vector3 GET_PED_BONE_COORDS(Ped ped, int boneId, float offsetX, float offsetY, float offsetZ) { return nv::invoke<Vector3>(0x17C07FC640E86B4E, ped, boneId, offsetX, offsetY, offsetZ); }
inline void SET_PED_HEAD_BLEND_DATA(Ped ped, int shapeFirstID, int shapeSecondID, int shapeThirdID, int skinFirstID, int skinSecondID, int skinThirdID, float shapeMix, float skinMix, float thirdMix, BOOL isParent) { nv::invoke<void>(0x9414E18B9434C2FE, ped, shapeFirstID, shapeSecondID, shapeThirdID, skinFirstID, skinSecondID, skinThirdID, shapeMix, skinMix, thirdMix, isParent); }
inline void TASK_JUMP(Ped ped, BOOL p1) { nv::invoke<void>(0x0AE4086104E067B1, ped, p1); }
inline void SET_PED_ACCURACY(Ped ped, int accuracy) { nv::invoke<void>(0x7AEFB85C1D49DEB6, ped, accuracy); }
inline void REQUEST_MODEL(Hash model) { nv::invoke<void>(0x963D27A58DF860AC, model); }
inline BOOL HAS_MODEL_LOADED(Hash model) { return nv::invoke<BOOL>(0x98A4EB5D89A0C952, model); }
inline void SET_MODEL_AS_NO_LONGER_NEEDED(Hash model) { nv::invoke<void>(0xE532F5D78798DAAB, model); }
inline BOOL IS_MODEL_IN_CDIMAGE(Hash model) { return nv::invoke<BOOL>(0x35B9E0803292B641, model); }
inline BOOL IS_MODEL_A_VEHICLE(Hash model) { return nv::invoke<BOOL>(0x19AAC8F07BFEC53E, model); }
inline BOOL IS_MODEL_VALID(Hash model) { return nv::invoke<BOOL>(0xC0296A2EDF545E92, model); }
inline void REQUEST_COLLISION_AT_COORD(float x, float y, float z) { nv::invoke<void>(0x07503F7948F491A7, x, y, z); }
inline void SET_PED_DENSITY_MULTIPLIER_THIS_FRAME(float multiplier) { nv::invoke<void>(0x95E3D6257B166CF2, multiplier); }
inline void SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(float multiplier) { nv::invoke<void>(0x245A6883D966D537, multiplier); }
inline void SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(float multiplier) { nv::invoke<void>(0xB3B3359379FE77D3, multiplier); }
inline void SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(float multiplier) { nv::invoke<void>(0xEAE6DCC7EEE3DB1D, multiplier); }
inline void SET_SCENARIO_PED_DENSITY_MULTIPLIER_THIS_FRAME(float p0, float p1) { nv::invoke<void>(0x7A556143A1C03898, p0, p1); }
inline void SET_CREATE_RANDOM_COPS(BOOL toggle) { nv::invoke<void>(0x102E68B2024D536D, toggle); }
inline void SET_RANDOM_TRAINS(BOOL unk) { nv::invoke<void>(0x80D9F74197EA47D9, unk); }
inline void SET_RANDOM_BOATS(BOOL toggle) { nv::invoke<void>(0x84436EC293B1415F, toggle); }
inline void SET_GARBAGE_TRUCKS(BOOL toggle) { nv::invoke<void>(0x2AFD795EEAC8D30D, toggle); }
inline void SET_NUMBER_OF_PARKED_VEHICLES(int value) { nv::invoke<void>(0xCAA15F13EBD417FF, value); }
inline void SET_PED_POPULATION_BUDGET(int p0) { nv::invoke<void>(0x8C95333CFC3340F3, p0); }
inline void SET_VEHICLE_POPULATION_BUDGET(int p0) { nv::invoke<void>(0xCB9E1EB3BE2AF4E9, p0); }
inline void CLEAR_AREA_OF_PEDS(float x, float y, float z, float radius, BOOL unk) { nv::invoke<void>(0xBE31FD6CE464AC59, x, y, z, radius, unk); }
inline void CLEAR_AREA_OF_VEHICLES(float x, float y, float z, float radius, BOOL p4, BOOL p5, BOOL p6, BOOL p7, BOOL p8) { nv::invoke<void>(0x01C7B9B38428AEB6, x, y, z, radius, p4, p5, p6, p7, p8); }
inline void CLEAR_AREA_OF_COPS(float x, float y, float z, float radius, BOOL unk) { nv::invoke<void>(0x04F8FC8FCF58F88D, x, y, z, radius, unk); }
inline void SET_WEATHER_TYPE_NOW_PERSIST(char* weatherType) { nv::invoke<void>(0xED712CA327900C8A, weatherType); }
inline void SET_OVERRIDE_WEATHER(char* weatherType) { nv::invoke<void>(0xA43D5C6FE51ADBEF, weatherType); }
inline void CLEAR_OVERRIDE_WEATHER() { nv::invoke<void>(0x338D2E3477711050); }
inline void CLEAR_WEATHER_TYPE_PERSIST() { nv::invoke<void>(0xCCC39339BEF76CF5); }
inline void SET_CLOCK_TIME(int hour, int minute, int second) { nv::invoke<void>(0x47C3B5848C3E45D8, hour, minute, second); }
inline void PAUSE_CLOCK(BOOL toggle) { nv::invoke<void>(0x4055E40BD2DBEC1D, toggle); }
inline void NETWORK_OVERRIDE_CLOCK_TIME(int Hours, int Minutes, int Seconds) { nv::invoke<void>(0xE679E3E06E363892, Hours, Minutes, Seconds); }
inline int GET_CLOCK_HOURS() { return nv::invoke<int>(0x25223CA6B4D20B7F); }
inline int GET_CLOCK_MINUTES() { return nv::invoke<int>(0x13D2B8ADD79640F2); }
inline void NETWORK_RESURRECT_LOCAL_PLAYER(Any p0, Any p1, Any p2, Any p3, Any p4, Any p5) { nv::invoke<void>(0xEA23C49EAA83ACFB, p0, p1, p2, p3, p4, p5); }
inline void TERMINATE_ALL_SCRIPTS_WITH_THIS_NAME(char* scriptName) { nv::invoke<void>(0x9DC711BC69C548DF, scriptName); }
inline void SET_FADE_OUT_AFTER_DEATH(BOOL toggle) { nv::invoke<void>(0x4A18E01DF2C87B86, toggle); }
inline void SET_FADE_OUT_AFTER_ARREST(BOOL toggle) { nv::invoke<void>(0x1E0B4DC0D990A4E7, toggle); }
inline void SET_FADE_IN_AFTER_DEATH_ARREST(BOOL toggle) { nv::invoke<void>(0xDA66D2796BA33F12, toggle); }
inline void IGNORE_NEXT_RESTART(BOOL toggle) { nv::invoke<void>(0x21FFB63D8C615361, toggle); }
inline void PAUSE_DEATH_ARREST_RESTART(BOOL disableRespawn) { nv::invoke<void>(0x2C2B3493FBF51C71, disableRespawn); }
inline void RESET_LOCALPLAYER_STATE() { nv::invoke<void>(0xC0AA53F866B3134D); }
inline void DO_SCREEN_FADE_IN(int duration) { nv::invoke<void>(0xD4E8E24955024033, duration); }
inline void DO_SCREEN_FADE_OUT(int duration) { nv::invoke<void>(0x891B5B39AC6302AF, duration); }
inline BOOL IS_SCREEN_FADED_OUT() { return nv::invoke<BOOL>(0xB16FCE9DDC7BA182); }
inline BOOL IS_SCREEN_FADED_IN() { return nv::invoke<BOOL>(0x5A859503B0C08678); }
inline void SHUTDOWN_LOADING_SCREEN() { nv::invoke<void>(0x078EBE9809CCD637); }
inline BOOL GET_IS_LOADING_SCREEN_ACTIVE() { return nv::invoke<BOOL>(0x10D0A8F259E93EC9); }
inline void ANIMPOSTFX_STOP_ALL() { nv::invoke<void>(0xB4EDDC19532BFB85); }
inline void DISABLE_CONTROL_ACTION(int controlGroup, int control, BOOL disable) { nv::invoke<void>(0xFE99B66D079CF6BC, controlGroup, control, disable); }
inline void DISABLE_ALL_CONTROL_ACTIONS(int controlGroup) { nv::invoke<void>(0x5F4B6931816E599B, controlGroup); }
inline void ENABLE_ALL_CONTROL_ACTIONS(int controlGroup) { nv::invoke<void>(0xA5FFE9B05F199DE7, controlGroup); }
inline BOOL IS_CONTROL_JUST_PRESSED(int index, int control) { return nv::invoke<BOOL>(0x580417101DDB492F, index, control); }
inline BOOL IS_DISABLED_CONTROL_PRESSED(int index, int control) { return nv::invoke<BOOL>(0xE2587F8CBBD87B1D, index, control); }
inline BOOL IS_CONTROL_PRESSED(int index, int control) { return nv::invoke<BOOL>(0xF3A21BCD95725A4A, index, control); }
inline float GET_DISABLED_CONTROL_NORMAL(int control, int variable) { return nv::invoke<float>(0x11E65974A982637C, control, variable); }
inline float GET_CONTROL_NORMAL(int index, int control) { return nv::invoke<float>(0xEC3C9B8D5327B563, index, control); }
inline Blip GET_FIRST_BLIP_INFO_ID(Blip blip) { return nv::invoke<Blip>(0x1BEDE233E6CD2A1F, blip); }
inline Vector3 GET_BLIP_INFO_ID_COORD(Any p0) { return nv::invoke<Vector3>(0xFA7C7F0AADF25D09, p0); }
inline BOOL IS_WAYPOINT_ACTIVE() { return nv::invoke<BOOL>(0x1DD1F58F493F1DA5); }
inline BOOL DOES_BLIP_EXIST(Blip blip) { return nv::invoke<BOOL>(0xA6DB27D19ECBB7DA, blip); }
inline BOOL GET_GROUND_Z_FOR_3D_COORD(float x, float y, float z, float* groundZ, Any b) { return nv::invoke<BOOL>(0xC906A7DAB05C8D2B, x, y, z, groundZ, b); }
inline BOOL GET_SCREEN_COORD_FROM_WORLD_COORD(float x3d, float y3d, float z3d, float* x2d, float* y2d) { return nv::invoke<BOOL>(0x34E82F05DF2974F5, x3d, y3d, z3d, x2d, y2d); }
inline int ADD_BLIP_FOR_ENTITY(Entity entity) { return nv::invoke<int>(0x5CDE92C702A8FCE7, entity); }
inline void REMOVE_BLIP(Blip* blip) { nv::invoke<void>(0x86A652570E5F25DD, blip); }
inline void SET_BLIP_COLOUR(Blip blip, int color) { nv::invoke<void>(0x03D7FB09E75D6B7E, blip, color); }
inline void SET_BLIP_SPRITE(Blip blip, int spriteId) { nv::invoke<void>(0xDF735600A4696DAF, blip, spriteId); }
inline void SET_BLIP_AS_SHORT_RANGE(Blip blip, BOOL p1) { nv::invoke<void>(0xBE8BE4FE60E27B72, blip, p1); }
inline void SET_BLIP_SCALE(Blip blip, float scale) { nv::invoke<void>(0xD38744167B2FA257, blip, scale); }
inline void SET_VEHICLE_FIXED(Vehicle vehicle) { nv::invoke<void>(0x115722B1B9C14C1C, vehicle); }
inline void SET_VEHICLE_ENGINE_ON(Vehicle vehicle, BOOL value, BOOL instantly, BOOL unk) { nv::invoke<void>(0x2497C4717C8B881E, vehicle, value, instantly, unk); }
inline BOOL GET_IS_VEHICLE_ENGINE_RUNNING(Vehicle vehicle) { return nv::invoke<BOOL>(0xAE31E7DF9B5B132E, vehicle); }
inline void SET_VEHICLE_NUMBER_PLATE_TEXT(Vehicle vehicle, char* plateText) { nv::invoke<void>(0x95A88F0B409CDA47, vehicle, plateText); }
inline BOOL IS_VEHICLE_SEAT_FREE(Vehicle vehicle, int seatIndex) { return nv::invoke<BOOL>(0x22AC59A870E6A669, vehicle, seatIndex); }
inline int GET_VEHICLE_MAX_NUMBER_OF_PASSENGERS(Vehicle vehicle) { return nv::invoke<int>(0xA7C4F2C6E744A550, vehicle); }
inline void SET_VEHICLE_DOORS_LOCKED(Vehicle vehicle, int doorLockStatus) { nv::invoke<void>(0xB664292EAECF7FA6, vehicle, doorLockStatus); }
inline BOOL SET_VEHICLE_ON_GROUND_PROPERLY(Vehicle vehicle) { return nv::invoke<BOOL>(0x49733E92263139D1, vehicle); }
inline void SET_VEHICLE_FORWARD_SPEED(Vehicle vehicle, float speed) { nv::invoke<void>(0xAB54A438726D25D5, vehicle, speed); }
inline Vehicle CREATE_VEHICLE(Hash modelHash, float x, float y, float z, float heading, BOOL networkHandle, BOOL vehiclehandle) { return nv::invoke<Vehicle>(0xAF35D0D2583051B0, modelHash, x, y, z, heading, networkHandle, vehiclehandle); }
inline void SET_VEHICLE_SIREN(Vehicle vehicle, BOOL toggle) { nv::invoke<void>(0xF4924635A19EB37D, vehicle, toggle); }
inline BOOL IS_VEHICLE_SIREN_ON(Vehicle vehicle) { return nv::invoke<BOOL>(0x4C9BF537BE2634B2, vehicle); }
inline void SET_VEHICLE_DIRT_LEVEL(Vehicle vehicle, float dirtLevel) { nv::invoke<void>(0x79D3B596FE44EE8B, vehicle, dirtLevel); }
inline void SET_VEHICLE_ENGINE_HEALTH(Vehicle vehicle, float health) { nv::invoke<void>(0x45F6D8EEF34ABEF1, vehicle, health); }
inline void SET_VEHICLE_BODY_HEALTH(Vehicle vehicle, float value) { nv::invoke<void>(0xB77D05AC8C78AADB, vehicle, value); }
inline void SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER(Vehicle vehicle, BOOL owned) { nv::invoke<void>(0x2B5F9D2AF1F1722D, vehicle, owned); }
inline void SET_VEHICLE_NEEDS_TO_BE_HOTWIRED(Vehicle vehicle, BOOL toggle) { nv::invoke<void>(0xFBA550EA44404EE6, vehicle, toggle); }
inline void SET_VEHICLE_IS_STOLEN(Vehicle vehicle, BOOL isStolen) { nv::invoke<void>(0x67B2C79AA7FF5738, vehicle, isStolen); }
inline void SET_VEHICLE_STEER_BIAS(Vehicle vehicle, float value) { nv::invoke<void>(0x42A8EC77D5150CBE, vehicle, value); }
inline void DISPLAY_RADAR(BOOL Toggle) { nv::invoke<void>(0xA0EBB943C300E693, Toggle); }
inline void DISPLAY_HUD(BOOL Toggle) { nv::invoke<void>(0xA6294919E56FF02A, Toggle); }
inline void HIDE_HUD_AND_RADAR_THIS_FRAME() { nv::invoke<void>(0x719FF505F097FD20); }
inline void HIDE_HUD_COMPONENT_THIS_FRAME(int id) { nv::invoke<void>(0x6806C51AD12B83B8, id); }
inline Vector3 GET_GAMEPLAY_CAM_ROT(Any p0) { return nv::invoke<Vector3>(0x837765A25378F0BB, p0); }
inline Vector3 GET_GAMEPLAY_CAM_COORD() { return nv::invoke<Vector3>(0x14D6F5678D8F1B37); }
inline Hash GET_HASH_KEY(char* value) { return nv::invoke<Hash>(0xD24D37CC275948CC, value); }
inline Any GET_GAME_TIMER() { return nv::invoke<Any>(0x9CD27B0045628463); }
inline float GET_FRAME_TIME() { return nv::invoke<float>(0x15C40837039FFAF7); }
inline void DESTROY_MOBILE_PHONE() { nv::invoke<void>(0x3BC861DF703E5097); }
inline void SET_AUDIO_FLAG(char* flagName, BOOL toggle) { nv::invoke<void>(0xB9EFD5C25018725A, flagName, toggle); }
inline void SET_CAN_ATTACK_FRIENDLY(Ped ped, BOOL toggle, BOOL p2) { nv::invoke<void>(0xB3B1CB349FF9C75D, ped, toggle, p2); }
inline BOOL IS_PAUSE_MENU_ACTIVE() { return nv::invoke<BOOL>(0xB0034A223497FFCB); }
inline void SET_GAME_PAUSED(BOOL toggle) { nv::invoke<void>(0x577D1284D6873711, toggle); }
inline void ACTIVATE_FRONTEND_MENU(Hash menuhash, BOOL p1, int p2) { nv::invoke<void>(0xEF01D36B9C9D0C7B, menuhash, p1, p2); }
inline void SET_PAUSE_MENU_ACTIVE(BOOL toggle) { nv::invoke<void>(0xDF47FC56C71569CF, toggle); }
inline void SET_RADAR_BIGMAP_ENABLED(BOOL toggleBigMap, BOOL showFullMap) { nv::invoke<void>(0x231C8F89D0539D8F, toggleBigMap, showFullMap); }
inline BOOL IS_PAUSE_MENU_RESTARTING() { return nv::invoke<BOOL>(0x1C491717107431C7); }
inline void SET_BLIP_FLASHES(Blip blip, BOOL toggle) { nv::invoke<void>(0xB14552383D39CE3E, blip, toggle); }
inline void SET_BLIP_AS_FRIENDLY(Blip blip, BOOL toggle) { nv::invoke<void>(0x6F6F290102C02AB4, blip, toggle); }
inline void SET_BLIP_PRIORITY(Blip blip, Any p1) { nv::invoke<void>(0xAE9FC9EF6A9FAC79, blip, p1); }
inline void SET_MOUSE_CURSOR_ACTIVE_THIS_FRAME() { nv::invoke<void>(0xAAE7CE1D63167423); }
inline void DISPLAY_AREA_NAME(BOOL toggle) { nv::invoke<void>(0x276B6CE369C33678, toggle); }
inline void SPECIAL_ABILITY_DEACTIVATE_FAST(Player player) { nv::invoke<void>(0x9CB5CE07A3968D5A, player); }
inline void SET_PLAYER_HEALTH_RECHARGE_MULTIPLIER(Player player, float regenRate) { nv::invoke<void>(0x5DB660B38DD98A31, player, regenRate); }
inline BOOL IS_DISABLED_CONTROL_JUST_PRESSED(int index, int control) { return nv::invoke<BOOL>(0x91AEF906BCA88877, index, control); }
inline BOOL IS_DISABLED_CONTROL_JUST_RELEASED(int index, int control) { return nv::invoke<BOOL>(0x305C8DCD79DA8B0F, index, control); }
inline BOOL IS_ENTITY_ON_SCREEN(Entity entity) { return nv::invoke<BOOL>(0xE659E47AF827484B, entity); }
inline BOOL HAS_ENTITY_CLEAR_LOS_TO_ENTITY(Entity entity1, Entity entity2, int traceType) { return nv::invoke<BOOL>(0xFCDFF7B72D23A1AC, entity1, entity2, traceType); }
inline BOOL IS_ENTITY_IN_AIR(Entity entity) { return nv::invoke<BOOL>(0x886E37EC497200B6, entity); }
inline void TERMINATE_THREAD(int id) { nv::invoke<void>(0xC8B189ED9138BCD4, id); }
inline BOOL IS_THREAD_ACTIVE(int threadId) { return nv::invoke<BOOL>(0x46E9AE36D8FA6417, threadId); }
inline char* GET_NAME_OF_SCRIPT_WITH_THIS_ID(int threadId) { return nv::invoke<char*>(0x05A42BA9FC8DA96B, threadId); }
inline void SCRIPT_THREAD_ITERATOR_RESET() { nv::invoke<void>(0xDADFADA5A20143A8); }
inline int SCRIPT_THREAD_ITERATOR_GET_NEXT_THREAD_ID() { return nv::invoke<int>(0x30B4FA1C82DD4B9F); }
inline int GET_ID_OF_THIS_THREAD() { return nv::invoke<int>(0xC30338E8088E2E21); }
inline void REMOVE_CUTSCENE() { nv::invoke<void>(0x440AF51A3462B86F); }
inline void STOP_CUTSCENE_IMMEDIATELY() { nv::invoke<void>(0xD220BDD222AC4A1E); }
inline BOOL IS_CUTSCENE_ACTIVE() { return nv::invoke<BOOL>(0x991251AFC3981F84); }
inline BOOL IS_CUTSCENE_PLAYING() { return nv::invoke<BOOL>(0xD3C2E180A40F031E); }
inline void RENDER_SCRIPT_CAMS(BOOL render, BOOL ease, Any camera, BOOL p3, BOOL p4) { nv::invoke<void>(0x07E5B515DB0636FC, render, ease, camera, p3, p4); }
inline void DESTROY_ALL_CAMS(BOOL destroy) { nv::invoke<void>(0x8E5FB15663F79120, destroy); }
inline void SET_WIDESCREEN_BORDERS(BOOL p0, int p1) { nv::invoke<void>(0xDCD4EA924F42D01A, p0, p1); }
inline void CLEAR_TIMECYCLE_MODIFIER() { nv::invoke<void>(0x0F07E7745A236711); }
inline void SET_MISSION_FLAG(BOOL toggle) { nv::invoke<void>(0xC4301E5121A0ED73, toggle); }
inline Any STOP_SCRIPTED_CONVERSATION(BOOL p0) { return nv::invoke<Any>(0xD79DEEFB53455EBA, p0); }
inline void CLEAR_PRINTS() { nv::invoke<void>(0xCC33FA791322B9D9); }
inline void CLEAR_ALL_HELP_MESSAGES() { nv::invoke<void>(0x6178F68A87A4D3A0); }
inline void SET_CINEMATIC_MODE_ACTIVE(BOOL p0) { nv::invoke<void>(0xDCF0754AC3D6FD4E, p0); }
inline BOOL IS_PLAYER_CONTROL_ON(Player player) { return nv::invoke<BOOL>(0x49C32D60007AFA47, player); }
inline void STOP_AUDIO_SCENES() { nv::invoke<void>(0xBAC7FC81A75EC1A1); }
inline BOOL HAS_COLLISION_LOADED_AROUND_ENTITY(Entity entity) { return nv::invoke<BOOL>(0xE9676F61BC0B3321, entity); }
inline Any REQUEST_SCALEFORM_MOVIE(char* scaleformName) { return nv::invoke<Any>(0x11FE353CF9733E6F, scaleformName); }
inline BOOL HAS_SCALEFORM_MOVIE_LOADED(int scaleform) { return nv::invoke<BOOL>(0x85F01B8D5B90570E, scaleform); }
inline BOOL BEGIN_SCALEFORM_MOVIE_METHOD(int scaleform, char* functionName) { return nv::invoke<BOOL>(0xF6E48914C7A8694E, scaleform, functionName); }
inline void SCALEFORM_MOVIE_METHOD_ADD_PARAM_INT(int value) { nv::invoke<void>(0xC3D0841A0CC546A6, value); }
inline void END_SCALEFORM_MOVIE_METHOD() { nv::invoke<void>(0xC6796A8FFA375E53); }
inline void ATTACH_ENTITY_TO_ENTITY(Entity entity1, Entity entity2, int boneIndexEnt2, float posX, float posY, float posZ, float rotX, float rotY, float rotZ, BOOL p9, BOOL isRelative, BOOL collision, BOOL allowRotation, int boneIndexEnt1, BOOL fixedRot) { nv::invoke<void>(0x6B9BBD38AB0796DF, entity1, entity2, boneIndexEnt2, posX, posY, posZ, rotX, rotY, rotZ, p9, isRelative, collision, allowRotation, boneIndexEnt1, fixedRot); }
inline void DETACH_ENTITY(Entity entity, BOOL p1, BOOL p2) { nv::invoke<void>(0x961AC54BF0613F5D, entity, p1, p2); }
inline char* GET_VEHICLE_NUMBER_PLATE_TEXT(Vehicle vehicle) { return nv::invoke<char*>(0x7CE1CCB9B293020E, vehicle); }
inline float GET_VEHICLE_ENGINE_HEALTH(Vehicle vehicle) { return nv::invoke<float>(0xC45D23BAF168AAB8, vehicle); }
inline Object CREATE_OBJECT_NO_OFFSET(Hash objectHash, float posX, float posY, float posZ, BOOL networkHandle, BOOL createHandle, BOOL dynamic) { return nv::invoke<Object>(0x9A294B2138ABB884, objectHash, posX, posY, posZ, networkHandle, createHandle, dynamic); }
inline Blip ADD_BLIP_FOR_COORD(float x, float y, float z) { return nv::invoke<Blip>(0x5A039BB0BCA604B6, x, y, z); }
inline void BEGIN_TEXT_COMMAND_SET_BLIP_NAME(char* gxtentry) { nv::invoke<void>(0xF9113A30DE5C6670, gxtentry); }
inline void END_TEXT_COMMAND_SET_BLIP_NAME(Blip blip) { nv::invoke<void>(0xBC38B49BCB83BC9B, blip); }
inline void SET_VEHICLE_ENGINE_POWER_MULTIPLIER(Vehicle vehicle, float value) { nv::invoke<void>(0x93A3996368C94158, vehicle, value); }
inline void SET_VEHICLE_ENGINE_TORQUE_MULTIPLIER(Vehicle vehicle, float value) { nv::invoke<void>(0xB59E4BD37AE292DB, vehicle, value); }
inline BOOL SET_PED_TO_RAGDOLL(Ped ped, int time1, int time2, int ragdollType, BOOL p4, BOOL p5, BOOL p6) { return nv::invoke<BOOL>(0xAE99FB955581844A, ped, time1, time2, ragdollType, p4, p5, p6); }
inline void SET_PED_DUCKING(Ped ped, BOOL toggle) { nv::invoke<void>(0x030983CA930B692D, ped, toggle); }
inline void SET_VEHICLE_PETROL_TANK_HEALTH(Vehicle vehicle, float health) { nv::invoke<void>(0x70DB57649FA8D0D8, vehicle, health); }
inline BOOL IS_WEAPON_VALID(Hash weaponHash) { return nv::invoke<BOOL>(0x937C71165CF334B3, weaponHash); }
inline void ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(char* text) { nv::invoke<void>(0x6C188BE134E074AA, text); }
inline void DRAW_MARKER(int type, float x, float y, float z, float dirX, float dirY, float dirZ, float rotX, float rotY, float rotZ, float scaleX, float scaleY, float scaleZ, int colorR, int colorG, int colorB, int alpha, BOOL bobUpAndDown, BOOL faceCamera, int p19, BOOL rotate, char* textureDict, char* textureName, BOOL drawOnEnts) { nv::invoke<void>(0x28477EC23D892089, type, x, y, z, dirX, dirY, dirZ, rotX, rotY, rotZ, scaleX, scaleY, scaleZ, colorR, colorG, colorB, alpha, bobUpAndDown, faceCamera, p19, rotate, textureDict, textureName, drawOnEnts); }
inline void TASK_START_SCENARIO_IN_PLACE(Ped ped, char* scenarioName, int unkDelay, BOOL playEnterAnim) { nv::invoke<void>(0x142A02425FF02BD9, ped, scenarioName, unkDelay, playEnterAnim); }
inline void ENABLE_DISPATCH_SERVICE(int dispatchService, BOOL toggle) { nv::invoke<void>(0xDC0F817884CDD856, dispatchService, toggle); }
inline void SET_MINIMAP_HIDE_FOW(BOOL toggle) { nv::invoke<void>(0xF8DEE0A5600CBB93, toggle); }
inline void SET_ABILITY_BAR_VISIBILITY(BOOL visible) { nv::invoke<void>(0x1DFEDD15019315A9, visible); }

} // namespace n
