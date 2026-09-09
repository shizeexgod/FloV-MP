export default function Loading() {
  return (
    <div className="mx-auto w-full max-w-6xl px-5 py-20 sm:px-6">
      <div className="h-9 w-2/3 max-w-md animate-pulse rounded-lg bg-white/[0.05]" />
      <div className="mt-4 h-4 w-full max-w-xl animate-pulse rounded bg-white/[0.03]" />
      <div className="mt-2 h-4 w-4/5 max-w-lg animate-pulse rounded bg-white/[0.03]" />
      <div className="mt-10 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="h-32 animate-pulse rounded-2xl border border-white/[0.06] bg-white/[0.02]" />
        ))}
      </div>
    </div>
  );
}
