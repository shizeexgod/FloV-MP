'use client';

import React from 'react';
import { FolderKanban, Infinity as InfinityIcon } from 'lucide-react';
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
    creatingProj,
    createProjectHandler,
    licenses,
    D,
  } = useDashboard();
  const needsPurchase = licenses.length === 0;
  return (
      <Modal
        open={newProjOpen}
        onClose={() => setNewProjOpen(false)}
        title={D.modal.newProjTitle}
        description={D.modal.newProjDesc}
      >
        <form onSubmit={createProjectHandler} className="space-y-5">
          {needsPurchase ? (
            <div className="rounded-2xl border border-brand/20 bg-brand/[0.055] p-4">
              <div className="flex items-start gap-4">
                <InfinityIcon className="mt-0.5 h-5 w-5 flex-none text-brand" />
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-baseline justify-between gap-2">
                    <strong className="text-sm text-white">{D.modal.lifetimeName}</strong>
                    <span className="text-xl font-extrabold text-white">{D.modal.lifetimePrice}</span>
                  </div>
                  <p className="mt-1.5 text-xs leading-relaxed text-white/50">{D.modal.lifetimeNote}</p>
                  <p className="mt-2 text-[11px] leading-relaxed text-white/35">{D.modal.purchaseBeforeCreate}</p>
                </div>
              </div>
            </div>
          ) : (
            <div className="flex items-start gap-3 text-sm text-white/65">
              <FolderKanban className="mt-0.5 h-4 w-4 flex-none text-brand" />
              <p className="leading-relaxed">{D.modal.newProjDesc}</p>
            </div>
          )}
          <div>
            <FieldLabel htmlFor="new-project-name">{D.builder.projectName}</FieldLabel>
            <input
              id="new-project-name"
              required
              name="projectName"
              autoComplete="off"
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
            <FieldLabel htmlFor="new-project-slug">{D.modal.slugLabel}</FieldLabel>
            <input
              id="new-project-slug"
              required
              name="projectSlug"
              autoComplete="off"
              spellCheck={false}
              value={newProjSlug}
              onChange={(e) => setNewProjSlug(e.target.value)}
              placeholder="florida-v"
              className="field h-11 px-4 font-mono"
            />
          </div>
          <hr className="rule-soft" />
          <div className="flex justify-end gap-3">
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
              {needsPurchase ? D.modal.continueToPurchase : D.proj.create}
            </button>
          </div>
        </form>
      </Modal>
  );
}
