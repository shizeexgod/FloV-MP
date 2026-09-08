'use client';

import React from 'react';
import { useI18n } from '@/lib/i18n';
import { Container, PageHero } from '@/components/site';

const BODY = {
  ru: [
    ['Какие данные мы собираем', 'Email, никнейм и Telegram при регистрации; технические данные проектов (IP-адреса серверов, метрики телеметрии, логи консоли, ключи лицензий).'],
    ['Зачем', 'Для работы личного кабинета, верификации лицензий, биллинга и отправки уведомлений о статусе серверов.'],
    ['Хранение', 'Данные хранятся в MariaDB на инфраструктуре под контролем владельца платформы. Пароли — только в виде bcrypt-хэшей.'],
    ['Передача третьим лицам', 'Не передаём. Исключение — вебхуки Discord / Telegram, которые вы настраиваете сами.'],
    ['Ваши права', 'Вы можете запросить экспорт или удаление данных аккаунта через поддержку в Telegram.'],
  ],
  en: [
    ['What we collect', 'Email, nickname and Telegram at sign-up; technical project data (server IPs, telemetry metrics, console logs, license keys).'],
    ['Why', 'To run the dashboard, verify licenses, handle billing and send server status notifications.'],
    ['Storage', 'Data is stored in MariaDB on infrastructure controlled by the platform owner. Passwords are kept only as bcrypt hashes.'],
    ['Third parties', 'We do not share data. The exception is Discord / Telegram webhooks that you configure yourself.'],
    ['Your rights', 'You can request an export or deletion of your account data via Telegram support.'],
  ],
};

export default function PrivacyPage() {
  const { lang } = useI18n();
  return (
    <div>
      <PageHero title={lang === 'ru' ? 'Политика конфиденциальности' : 'Privacy Policy'} />
      <Container className="py-14">
        <div className="mx-auto max-w-3xl space-y-6">
          {BODY[lang].map(([h, p]) => (
            <div key={h}>
              <h2 className="text-[15px] font-semibold text-white">{h}</h2>
              <p className="mt-2 text-[13px] leading-relaxed text-white/55">{p}</p>
            </div>
          ))}
        </div>
      </Container>
    </div>
  );
}
