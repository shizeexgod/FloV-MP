'use client';

import React from 'react';
import { Spinner, Modal, FieldLabel } from '@/components/ui';
import { useDashboard } from './_ctx';

export function NewLicenseModal() {
  const {
    newLicOpen,
    setNewLicOpen,
    newPlan,
    setNewPlan,
    newName,
    setNewName,
    newIp,
    setNewIp,
    creatingLic,
    createLicense,
    D,
  } = useDashboard();
  return (
      <Modal
        open={newLicOpen}
        onClose={() => setNewLicOpen(false)}
        title={D.modal.newLicTitle}
        description={D.modal.newLicDesc}
      >
        <form onSubmit={createLicense} className="space-y-5">
          <div className="grid grid-cols-3 gap-3">
            {[
              { id: 'indie', name: D.modal.indie, slots: `128 ${D.proj.slots}`, price: D.modal.free, tone: 'slate' },
              { id: 'business', name: 'RP', slots: `512 ${D.proj.slots}`, price: '14 900 ₽', tone: 'brand' },
              { id: 'enterprise', name: 'Enterprise', slots: `1500+ ${D.proj.slots}`, price: '49 000 ₽', tone: 'cyber' },
            ].map((p) => (
              <button
                type="button"
                key={p.id}
                onClick={() => setNewPlan(p.id)}
                className={`rounded-2xl border p-4 text-center transition-all ${
                  newPlan === p.id
                    ? p.tone === 'cyber'
                      ? 'border-cyber/60 bg-cyber/10'
                      : p.tone === 'brand'
                      ? 'border-brand/60 bg-brand/10 shadow-neon-pink'
                      : 'border-white/30 bg-white/10'
                    : 'border-white/10 bg-white/[0.02]'
                }`}
              >
                <div className="text-xs font-bold text-white">{p.name}</div>
                <div className="mt-1 text-[10px] text-slate-400">{p.slots}</div>
                <div className="mt-2 text-xs font-bold text-white">{p.price}</div>
              </button>
            ))}
          </div>
          <div>
            <FieldLabel>{D.modal.yourServerName}</FieldLabel>
            <input
              required
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder="Moscow Night RP"
              className="field h-11 px-4"
            />
          </div>
          <div>
            <FieldLabel>{D.modal.ipLater}</FieldLabel>
            <input value={newIp} onChange={(e) => setNewIp(e.target.value)} placeholder="0.0.0.0" className="field h-11 px-4 font-mono" />
          </div>
          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button type="button" onClick={() => setNewLicOpen(false)} className="px-4 py-2 text-xs text-slate-400 transition hover:text-white">
              {D.cancel}
            </button>
            <button type="submit" disabled={creatingLic} className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50">
              {creatingLic ? <Spinner className="h-4 w-4" /> : null}
              {D.modal.activateLic}
            </button>
          </div>
        </form>
      </Modal>
  );
}
