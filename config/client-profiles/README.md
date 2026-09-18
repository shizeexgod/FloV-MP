# FloV:MP client profiles

Each profile describes one GTA edition and its exact executable/RPF hashes. The
launcher must select a profile by `edition` and validate all hashes before
starting a client runtime. Profiles are intentionally not interchangeable.

`needs-version-adapter`, `launch-validation` and `needs-enhanced-runtime` are
honest development states; they are not release-ready support claims.
`launch-validation` means the launcher is allowed to use one exact,
hash-verified game profile, but the full two-player regression gate has not yet
passed. A profile may be changed to `supported` only after a live connection
test reaches the server and the regression checks pass.
