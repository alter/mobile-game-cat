# Built, 2026-08-26

A third control on the capture screen — "Not now — give me a kitten" — next to
the two photo buttons, not hidden behind them. The share of players who skip is
one of the numbers this project watches (cat-shelter-mvp.md section 5), and a
skip control that has to be hunted for measures the hunt, not the preference.

`CatTraits.Default` is a **plain grey short-haired tabby with green eyes**:
the most ordinary cat there is. Deliberately not a rare or striking one — a
player who skips should feel she got the same game, not a consolation prize.

Fixed, never random. Two players who skipped must be able to talk about the
same kitten, and a player who skips twice must not meet two different cats.
A test asserts that every field comes out identical across calls.

The path touches no camera, no gallery, no network and no permission: it is a
constant in `Core`, reached by one call. That is what makes VERIFY 2 —
"airplane mode and camera permission denied" — true by construction rather
than by luck.

## Left open

VERIFY names PlayMode tests that tap the control and walk on to
`09-meet-your-cat`, which does not exist yet — there is no screen to walk to.
What is verified today: the traits are constant and complete
(`Tests/Core/CatTraitsTests.cs`), and the button is on screen. Tapping it is
for `14-testflight`.
