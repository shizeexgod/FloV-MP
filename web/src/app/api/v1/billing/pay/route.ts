import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';
import { PLANS, generateLicenseKey } from '@/lib/license';

export async function POST(req: NextRequest) {
  try {
    const session = getSessionUser();
    if (!session) {
      return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
    }

    const { invoiceId } = await req.json();
    if (!invoiceId) {
      return NextResponse.json({ error: 'Укажите ID счёта' }, { status: 400 });
    }

    const invoices = await query(
      'SELECT id, user_id, license_id, amount_rub, plan, status FROM portal_invoices WHERE id = ? AND user_id = ? LIMIT 1',
      [Number(invoiceId), session.userId]
    );

    if (invoices.length === 0) {
      return NextResponse.json({ error: 'Счёт не найден' }, { status: 404 });
    }

    const inv = invoices[0];
    if (inv.status === 'paid') {
      return NextResponse.json({ success: true, message: 'Счёт уже был оплачен ранее' });
    }

    // Mark paid
    await query('UPDATE portal_invoices SET status = ? WHERE id = ?', ['paid', inv.id]);

    const planConfig = PLANS[inv.plan] || PLANS.business;
    const extensionDays = inv.amount_rub >= 36000 ? 365 : 30;

    if (inv.license_id) {
      // Extend existing license
      const lics = await query('SELECT id, expires_at FROM portal_licenses WHERE id = ? LIMIT 1', [inv.license_id]);
      if (lics.length > 0) {
        const currentExp = new Date(lics[0].expires_at).getTime();
        const baseTime = currentExp > Date.now() ? currentExp : Date.now();
        const newExpiry = new Date(baseTime + extensionDays * 24 * 3600 * 1000)
          .toISOString()
          .slice(0, 19)
          .replace('T', ' ');

        await query('UPDATE portal_licenses SET expires_at = ?, is_active = 1 WHERE id = ?', [
          newExpiry,
          inv.license_id,
        ]);
      }
    } else {
      // Create new license for user
      const key = generateLicenseKey('FLV');
      const newExpiry = new Date(Date.now() + extensionDays * 24 * 3600 * 1000)
        .toISOString()
        .slice(0, 19)
        .replace('T', ' ');

      await query(
        'INSERT INTO portal_licenses (user_id, license_key, server_name, bound_ip, plan, max_players, expires_at) VALUES (?, ?, ?, ?, ?, ?, ?)',
        [
          session.userId,
          key,
          `${session.username} RP Server`,
          '0.0.0.0',
          inv.plan,
          planConfig.maxPlayers,
          newExpiry,
        ]
      );
    }

    return NextResponse.json({
      success: true,
      message: `Оплата успешно подтверждена! Лицензия продлена на ${extensionDays} дней.`,
    });
  } catch (err: any) {
    console.error('Pay invoice error:', err);
    return NextResponse.json({ error: 'Ошибка проведения платежа' }, { status: 500 });
  }
}
