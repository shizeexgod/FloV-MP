'use client';

import React from 'react';
import { Spinner, Modal, FieldLabel } from '@/components/ui';
import { useDashboard } from './_ctx';

export function NewProjectModal() {
  const {
    newProjOpen,
    setNewProjOpen,
    newProjName,
    setNewProjName,
    newProjSlug,
    setNewProjSlug,
    newProjPlan,
    setNewProjPlan,
    creatingProj,
    createProjectHandler,
    D,
  } = useDashboard();
  return (
      <Modal
        open={newProjOpen}
        onClose={() => setNewProjOpen(false)}
        title={D.modal.newProjTitle}
        description={D.modal.newProjDesc}
      >
        <form onSubmit={createProjectHandler} className="space-y-5">
          <div className="grid grid-cols-2 gap-3">
            {[
              { id: 'business', name: 'RP', slots: `512 ${D.proj.slots}`, tone: 'brand' },
              { id: 'enterprise', name: 'Enterprise', slots: `1500+ ${D.proj.slots}`, tone: 'cyber' },
            ].map((p) => (
              <button
                type="button"
                key={p.id}
                onClick={() => setNewProjPlan(p.id)}
                className={`rounded-2xl border p-4 text-center transition-all ${
                  newProjPlan === p.id
                    ? p.tone === 'cyber'
                      ? 'border-cyber/60 bg-cyber/10'
                      : 'border-brand/60 bg-brand/10 shadow-neon-pink'
                    : 'border-white/10 bg-white/[0.02]'
                }`}
              >
                <div className="text-xs font-bold text-white">{p.name}</div>
                <div className="mt-1 text-[10px] text-slate-400">{p.slots}</div>
              </button>
            ))}
          </div>
          <div>
            <FieldLabel>{D.builder.projectName}</FieldLabel>
            <input
              required
              value={newProjName}
              onChange={(e) => {
                setNewProjName(e.target.value);
                if (!newProjSlug || newProjSlug === newProjName.toLowerCase().replace(/\s+/g, '-')) {
                  setNewProjSlug(e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '-'));
                }
              }}
              placeholder="Florida V RolePlay"
              className="field h-11 px-4"
            />
          </div>
          <div>
            <FieldLabel>{D.modal.slugLabel}</FieldLabel>
            <input
              required
              value={newProjSlug}
              onChange={(e) => setNewProjSlug(e.target.value)}
              placeholder="florida-v"
              className="field h-11 px-4 font-mono"
            />
          </div>
          <div className="flex justify-end gap-3 border-t border-white/[0.08] pt-4">
            <button
              type="button"
              onClick={() => setNewProjOpen(false)}
              className="px-4 py-2 text-xs text-slate-400 transition hover:text-white"
            >
              {D.cancel}
            </button>
            <button
              type="submit"
              disabled={creatingProj}
              className="btn btn-primary h-10 px-5 text-xs disabled:opacity-50"
            >
              {creatingProj ? <Spinner className="h-4 w-4" /> : null}
              {D.proj.create}
            </button>
          </div>
        </form>
      </Modal>
  );
}
