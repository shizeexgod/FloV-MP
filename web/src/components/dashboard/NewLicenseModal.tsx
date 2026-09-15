'use client';

import React from 'react';
import { Check, Infinity as InfinityIcon } from 'lucide-react';
import { Spinner, Modal, FieldLabel } from '@/components/ui';
import { useDashboard } from './_ctx';

export function NewLicenseModal() {
  const {
    newLicOpen,
    setNewLicOpen,
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
          <div className="card no-lift p-5">
            <div className="flex items-start gap-4">
              <InfinityIcon className="mt-0.5 h-5 w-5 flex-none text-brand" />
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <strong className="text-sm text-white">{D.modal.lifetimeName}</strong>
                  <span className="text-xl font-extrabold text-white">{D.modal.lifetimePrice}</span>
                </div>
                <p className="mt-1.5 text-xs leading-relaxed text-white/50">{D.modal.lifetimeNote}</p>
                <div className="mt-3 flex items-center gap-2 text-xs text-white/70">
                  <Check className="h-3.5 w-3.5 text-brand" />
                  {D.modal.newLicDesc}
                </div>
              </div>
            </div>
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
          <hr className="rule-soft" />
          <div className="flex justify-end gap-3">
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
