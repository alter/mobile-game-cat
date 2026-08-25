
## verify downgraded to failed, 2026-08-25

The Unity project exists and the editor builds it, but the acceptance also
requires the tests to run, and from a clean checkout they do not: the
solver-bridge points at a `.csproj` this task deleted, and the Core test project
is now a Unity-generated file excluded by `game/.gitignore`.

Both are tracked in `10-accounts/07-build-wiring-fix`. This task returns to
`verify:passed` when that one closes.
