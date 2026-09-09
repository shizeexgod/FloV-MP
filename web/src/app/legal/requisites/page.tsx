'use client';

import React from 'react';
import Link from 'next/link';
import { Container, PageHero } from '@/components/site';
import { LEGAL, lv } from '@/lib/legal';

interface Row {
  dt: string;
  dd: React.ReactNode;
}

function Card({ title, rows }: { title: string; rows: Row[] }) {
  return (
    <div className="card p-6 sm:p-7">
      <h2 className="text-[15px] font-bold text-white">{title}</h2>
      <dl className="mt-4 divide-y divide-white/[0.06]">
        {rows.map((r) => (
          <div key={r.dt} className="grid grid-cols-1 gap-1 py-3 sm:grid-cols-[190px_1fr] sm:gap-4">
            <dt className="text-[12.5px] text-white/40">{r.dt}</dt>
            <dd className="text-[13.5px] leading-relaxed text-white/80">{r.dd}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

export default function RequisitesPage() {
  const performer: Row[] = [
    { dt: 'Статус', dd: lv(LEGAL.status) },
    { dt: LEGAL.status.toLowerCase().includes('ооо') ? 'Наименование' : 'ФИО', dd: lv(LEGAL.ownerName) },
    { dt: 'ИНН', dd: lv(LEGAL.inn) },
    ...(LEGAL.ogrn ? [{ dt: LEGAL.status.toLowerCase().includes('предприн') ? 'ОГРНИП' : 'ОГРН', dd: LEGAL.ogrn }] : []),
    ...(LEGAL.address ? [{ dt: 'Адрес', dd: LEGAL.address }] : []),
  ];

  const service: Row[] = [
    { dt: 'Наименование', dd: LEGAL.serviceName },
    { dt: 'Описание', dd: LEGAL.serviceDescription },
    {
      dt: 'Стоимость',
      dd: (
        <>
          {LEGAL.priceRub.toLocaleString('ru-RU')} ₽ — единоразовая Lifetime-лицензия на один проект.
          Ежемесячных платежей и автосписаний нет.
        </>
      ),
    },
    {
      dt: 'Получение услуги',
      dd: 'Доступ к личному кабинету на сайте и к дистрибутиву движка сразу после регистрации и подтверждения оплаты.',
    },
  ];

  const contacts: Row[] = [
    {
      dt: 'Email',
      dd: LEGAL.email ? <a href={`mailto:${LEGAL.email}`} className="text-brand hover:underline">{LEGAL.email}</a> : '—',
    },
    {
      dt: 'Телефон',
      dd: LEGAL.phone ? (
        <a href={`tel:${LEGAL.phone}`} className="text-brand hover:underline">{lv(LEGAL.phoneDisplay)}</a>
      ) : (
        '—'
      ),
    },
    {
      dt: 'Telegram',
      dd: LEGAL.telegramUrl ? (
        <a href={LEGAL.telegramUrl} target="_blank" rel="noopener noreferrer" className="text-brand hover:underline">
          {lv(LEGAL.telegramHandle)}
        </a>
      ) : (
        '—'
      ),
    },
    { dt: 'Поддержка', dd: lv(LEGAL.supportHours) },
  ];

  return (
    <div>
      <PageHero
        eyebrow="Документы"
        title="Реквизиты"
        sub="Контактная и платёжная информация исполнителя сервиса FloV:MP."
      />

      <Container className="py-14 sm:py-16">
        <div className="mx-auto grid max-w-3xl gap-4">
          <Card title="Исполнитель" rows={performer} />
          <Card title="Услуга" rows={service} />
          <Card title="Контакты" rows={contacts} />

          <p className="mt-2 text-[12.5px] leading-relaxed text-white/40">
            Оплата Lifetime-лицензии проходит через платёжный сервис {LEGAL.paymentProvider}. Полные условия — в{' '}
            <Link href="/legal/offer" className="text-brand hover:underline">
              публичной оферте
            </Link>
            .
          </p>
        </div>
      </Container>
    </div>
  );
}
