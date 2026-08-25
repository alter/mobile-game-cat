# Why this task exists at all

This task exists purely because 09-hidden-kinds invalidates the numbers
already measured in 04-difficulty-curve. The measured table - 98% / 87% / 66%
at 36 / 48 / 60 items - came from a policy that could see every kind in the
pile before choosing. Under hiding a player cannot plan ahead the same way,
and those rates will fall, possibly a long way.

The solver remains useful as a feasibility oracle ("does a solution exist")
but stops being a difficulty oracle once information is partial. **Order is
load-bearing:** hide first (09), measure second (this task), tune pile size
third (a follow-on tuning pass, not itself numbered in M3). Tuning against the
old numbers would be tuning against a game nobody will actually play, since no
real player has the solver's perfect information.

Source: cat-shelter-tasks.md lines 566-576; DECISIONS.md D3.
