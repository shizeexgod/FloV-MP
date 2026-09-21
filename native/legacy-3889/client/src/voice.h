#pragma once
#include <cstdint>
#include <string>
#include <vector>

namespace flov::voice
{
    /// Голосовой чат: микрофон → Opus (opus.dll из папки игры) → UDP на сервер,
    /// приём голосов ближайших игроков → сведение с громкостью по расстоянию.
    /// Все звуковые операции в своих потоках; игровой поток только передаёт
    /// «говорю/не говорю» и громкость каждого собеседника.

    /// Запустить после WELCOME. false — голос недоступен (причина в журнале).
    bool Start(const std::string& host, int port, const std::string& tokenHex, float radius);
    void Stop();
    bool Running();

    /// Кнопка разговора нажата (игровой поток, каждый кадр).
    void SetTalking(bool talking);
    bool Talking();
    /// Микрофон действительно открыт (иначе — причина в LastError).
    bool MicrophoneOk();
    std::string LastError();

    /// Громкость (0..1) и панорама (-1 левый .. 1 правый) для игрока id.
    void SetGain(uint32_t id, float gain, float pan);
    /// Игрок id говорил за последние ms миллисекунд.
    bool IsSpeaking(uint32_t id, uint32_t ms = 300);
    void Forget(uint32_t id);
}
