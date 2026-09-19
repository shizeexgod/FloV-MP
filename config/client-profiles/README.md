# FloV:MP client profiles

Each profile describes one GTA edition and its exact executable/RPF hashes. The
launcher must select a profile by `edition` and validate all hashes before
starting a client runtime. Profiles are intentionally not interchangeable.

The Enhanced `1.0.1158.13` profile is intentionally provisional:
`fingerprintStatus=pending-windows-capture` and empty hashes mean that a clean
Windows capture is still required. The launcher must not treat that profile as
supported or copy hashes from the Legacy profile.

`needs-native-adapter`, `needs-version-adapter` and `needs-enhanced-runtime`
are honest development states; they are not release-ready support claims. A
profile may be changed to `supported` only after a live connection test reaches
the server and the regression checks pass.
