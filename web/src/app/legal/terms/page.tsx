'use client';

import React from 'react';
import { useI18n } from '@/lib/i18n';
import { Container, PageHero } from '@/components/site';

const BODY = {
  ru: [
    ['1. Предмет', 'FloV:MP предоставляет лицензию на использование мультиплеерного движка и доступ к SaaS-панели управления игровыми проектами. Лицензия закрепляется за одним проектом.'],
    ['2. Использование', 'Клиент разворачивает движок на собственной инфраструктуре (VDS / Dedicated). Запрещены перепродажа лицензии, обход механизмов верификации и распространение бинарных файлов третьим лицам.'],
    ['3. Оплата и продление', 'Тарифы Pro и Enterprise оплачиваются помесячно за проект. При недоступности портала сервер продолжает работу до 7 дней (grace period).'],
    ['4. Ответственность', 'Платформа предоставляется «как есть». FloV:MP не несёт ответственности за контент игровых проектов клиентов и решения их администраций.'],
    ['5. Расторжение', 'Клиент может прекратить использование в любой момент. При нарушении условий доступ к панели и верификации ключа может быть приостановлен.'],
  ],
  en: [
    ['1. Subject', 'FloV:MP grants a license to use the multiplayer engine and access to the SaaS management panel for game projects. A license is tied to a single project.'],
    ['2. Usage', 'The client deploys the engine on their own infrastructure (VDS / Dedicated). Reselling the license, bypassing verification and distributing binaries to third parties are prohibited.'],
    ['3. Payment and renewal', 'Pro and Enterprise plans are billed monthly per project. If the portal is unreachable, the server keeps running for up to 7 days (grace period).'],
    ['4. Liability', 'The platform is provided "as is". FloV:MP is not responsible for the content of client game projects or the decisions of their administrations.'],
    ['5. Termination', 'The client may stop using the service at any time. In case of a breach, access to the panel and key verification may be suspended.'],
  ],
};

export default function TermsPage() {
  const { lang } = useI18n();
  return (
    <div>
      <PageHero title={lang === 'ru' ? 'Условия использования' : 'Terms of Service'} />
      <Container className="py-14">
        <div className="mx-auto max-w-3xl space-y-6">
          {BODY[lang].map(([h, p]) => (
            <div key={h}>
              <h2 className="text-[15px] font-semibold text-white">{h}</h2>
              <p className="mt-2 text-[13px] leading-relaxed text-white/55">{p}</p>
            </div>
          ))}
          <p className="pt-4 font-mono text-[11px] text-white/30">
            {lang === 'ru' ? 'Редакция от 08.09.2026' : 'Revision of 2026-09-08'}
          </p>
        </div>
      </Container>
    </div>
  );
}
