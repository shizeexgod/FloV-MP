'use client';

import React from 'react';
import { FolderKanban } from 'lucide-react';
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
          <div className="flex items-start gap-3 text-sm text-white/65">
            <FolderKanban className="mt-0.5 h-4 w-4 flex-none text-brand" />
            <p className="leading-relaxed">{D.modal.newProjDesc}</p>
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
              {D.proj.create}
            </button>
          </div>
        </form>
      </Modal>
  );
}
