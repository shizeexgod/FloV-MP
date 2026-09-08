'use client';

import React from 'react';
import { Spinner, Modal, FieldLabel } from '@/components/ui';
import { useDashboard } from './_ctx';

export function IpBindModal() {
  const {
    ipLicense,
    setIpLicense,
    ipValue,
    setIpValue,
    ipName,
    setIpName,
    ipErr,
    savingIp,
    saveIp,
    D,
  } = useDashboard();
  return (
      <Modal
        open={!!ipLicense}
        onClose={() => setIpLicense(null)}
        title={D.modal.bindIpTitle}
        description={D.modal.bindIpDesc}
        maxWidth="max-w-md"
      >
        {ipErr && (
          <div className="mb-4 rounded-xl border border-red-500/30 bg-red-500/10 px-3 py-2.5 text-xs text-red-400">
            {ipErr}
          </div>
        )}
        <form onSubmit={saveIp} className="space-y-4">
          <div>
            <FieldLabel>{D.modal.serverName}</FieldLabel>
            <input required value={ipName} onChange={(e) => setIpName(e.target.value)} className="field h-11 px-4" />
          </div>
          <div>
            <FieldLabel>{D.modal.ipv4}</FieldLabel>
            <input
              required
              value={ipValue}
              onChange={(e) => setIpValue(e.target.value)}
              placeholder="188.127.229.224"
              className="field h-11 px-4 font-mono"
            />
          </div>
          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button type="button" onClick={() => setIpLicense(null)} className="px-4 py-2 text-xs text-slate-400 transition hover:text-white">
              {D.cancel}
            </button>
            <button type="submit" disabled={savingIp} className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50">
              {savingIp ? <Spinner className="h-4 w-4" /> : null}
              {D.modal.saveBinding}
            </button>
          </div>
        </form>
      </Modal>
  );
}
