import { NextRequest, NextResponse } from 'next/server';
import { getSessionUser } from '@/lib/auth';
import { query } from '@/lib/db';
import { PLANS } from '@/lib/license';

export async function POST(req: NextRequest) {
  try {
    const session = getSessionUser();
    if (!session) {
      return NextResponse.json({ error: 'Не авторизован' }, { status: 401 });
    }

    const { licenseId, plan, period, paymentMethod } = await req.json();

    const planConfig = PLANS[plan || 'business'] || PLANS.business;

    let amount = planConfig.priceMonthly;
    let days = 30;

    if (period === 'halfYear') {
      amount = planConfig.priceHalfYear * 6;
      days = 180;
    } else if (period === 'year') {
      amount = planConfig.priceYearly * 12;
      days = 365;
    }

    const paymentId = `pay_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`;

    const res: any = await query(
      'INSERT INTO portal_invoices (user_id, license_id, amount_rub, plan, payment_method, payment_id, status) VALUES (?, ?, ?, ?, ?, ?, ?)',
      [
        session.userId,
        licenseId ? Number(licenseId) : null,
        amount,
        planConfig.id,
        paymentMethod || 'card',
        paymentId,
        'pending',
      ]
    );

    return NextResponse.json({
      success: true,
      invoiceId: res.insertId,
      amount,
      plan: planConfig.name,
      paymentId,
      paymentUrl: `/dashboard?pay=${res.insertId}`,
    });
  } catch (err: any) {
    console.error('Create invoice error:', err);
    return NextResponse.json({ error: 'Ошибка создания счёта' }, { status: 500 });
  }
}
