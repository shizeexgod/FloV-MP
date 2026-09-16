import { notFound } from 'next/navigation';
import DashboardConcept, { type DashboardConceptVariant } from '@/components/dashboard-concepts/DashboardConcept';

const VARIANTS: DashboardConceptVariant[] = ['precision', 'command', 'studio'];

export function generateStaticParams() {
  return VARIANTS.map((variant) => ({ variant }));
}

export default function DashboardConceptPage({ params }: { params: { variant: string } }) {
  if (!VARIANTS.includes(params.variant as DashboardConceptVariant)) notFound();
  return <DashboardConcept variant={params.variant as DashboardConceptVariant} />;
}
