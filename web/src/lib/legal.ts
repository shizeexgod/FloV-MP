/**
 * Юридические данные FloV:MP — единый источник для реквизитов, оферты и футера.
 * Заполнить поля с пометкой TODO перед подключением Robokassa.
 * Структура повторяет legal-config.js из проекта ICS.
 */

export interface LegalConfig {
  /** «Самозанятый (НПД)» | «Индивидуальный предприниматель» | «ООО …» */
  status: string;
  /** ФИО (самозанятый / ИП) или полное наименование ООО */
  ownerName: string;
  /** ИНН исполнителя */
  inn: string;
  /** ОГРНИП (для ИП) или ОГРН (для ООО). Для самозанятого — пустая строка */
  ogrn: string;
  /** Адрес регистрации / места нахождения (для ИП и ООО). Для самозанятого можно оставить пустым */
  address: string;

  email: string;
  /** Телефон в формате для tel: — «+79990000000» */
  phone: string;
  /** Тот же телефон для показа — «+7 999 000-00-00» */
  phoneDisplay: string;

  telegramUrl: string;
  telegramHandle: string;
  /** Публичный канал / чат сообщества (необязательно) */
  telegramChannelUrl: string;

  /** Часы работы поддержки, напр. «Пн–Пт, 10:00–19:00 (МСК)» */
  supportHours: string;

  serviceName: string;
  serviceDescription: string;

  /** Стоимость Lifetime-лицензии в рублях РФ */
  priceRub: number;
  /** Платёжный провайдер, отображается в оферте */
  paymentProvider: string;

  /** Основной домен сайта без слэша на конце — напр. «https://flovmp.ru» */
  siteUrl: string;

  /** Дата публикации действующей редакции оферты, напр. «9 сентября 2026 г.» */
  offerDate: string;
  /** Срок возврата в днях с момента оплаты */
  refundDays: number;
}

export const LEGAL: LegalConfig = {
  // --- TODO: заполнить из опросника ---
  status: '',
  ownerName: '',
  inn: '',
  ogrn: '',
  address: '',
  email: '',
  phone: '',
  phoneDisplay: '',
  telegramUrl: '',
  telegramHandle: '',
  telegramChannelUrl: '',
  supportHours: 'Пн–Пт, 10:00–19:00 (МСК)',

  // --- известно по проекту ---
  serviceName: 'FloV:MP',
  serviceDescription:
    'Автономный мультиплеерный движок для GTA V и SaaS-платформа управления игровыми проектами: сетевой рантайм, лицензирование, облачная консоль серверов, телеметрия, биллинг, FastDL CDN и сборщик лаунчера.',
  priceRub: 25000,
  paymentProvider: 'Robokassa',
  siteUrl: 'https://flovmp.ru',
  offerDate: '9 сентября 2026 г.',
  refundDays: 7,
};

/** Значение или тире, если поле ещё не заполнено. */
export const lv = (v: string | number): string => {
  const s = String(v ?? '').trim();
  return s === '' ? '—' : s;
};
