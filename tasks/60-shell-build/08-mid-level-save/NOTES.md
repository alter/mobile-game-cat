# Notes - this task was under-specified, and the gap punishes exactly our player

Source: cat-shelter-tasks.md, "6.7 was under-specified..." (lines 858-882).

The original acceptance line read "close and reopen preserves progress," and
the `Player` entity in the MVP holds `levels_done, current_level` - level
granularity only. Nothing stored which items had been taken or what sat on
the shelf, and `Board._taken` was private with no way out. So, as specified:
leave mid-level, lose the room.

The audience is defined as playing "10-20 minutes in gaps between other
things." Interruption is their normal case, not an edge case. Riding the
metro, the stop arrives, the app closes - and a half-cleared room evaporates.
That is a punishment, and the MVP's own rule forbids punishments ("the kitten
doesn't get sick").

Three things this task requires, restated as scope above:

1. Serialise the board, not the level number: taken items, shelf contents,
   current level, shelf capacity (the booster can change it, even if the
   MVP's booster never actually fires - see 07-lose-screen-fake-door).
2. Write on every move, not on OnApplicationPause. iOS kills backgrounded
   apps without warning; the pause callback is not a reliable last chance.
3. Make Board reconstructable from that state - today it can only be built
   fresh from a Level.

Cheap to build, and it removes the single most common way this audience will
lose work.
