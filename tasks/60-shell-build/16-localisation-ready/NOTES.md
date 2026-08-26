# Why this is P2 and last, not P0 and now

Raised by the owner on 2026-08-26, while the notification copy was being
written: are the strings written so a translation can be dropped in, or are
they hardcoded?

They are hardcoded. `const string Title` / `const string Body` in
`EveningReminder`, and card titles and bodies inline in `DebugGameView`
("Room clean!", "Shelf jammed", "Would you keep playing if this were the real
game?").

That is deliberate for now and should stay deliberate:

- The MVP ships in English only, to an English-speaking test audience, on about
  a hundred paid installs. A second language before gate 3 would be work spent
  on an audience that does not exist yet.
- The copy is still moving. `12-copy-english` has not run, and half these
  strings will be rewritten by it. Extracting text that is about to change
  twice means doing the extraction three times.
- The count is small — roughly thirty strings — so the cost of extracting later
  is hours, not days.

**What makes it worth writing down rather than forgetting:** the cost of
extraction grows with every new screen, and the photo phase (`50-photo`) adds
the four capture-outcome messages, the meet-your-cat screen and the skip path.
If this is still undone when those land, it stops being an afternoon.

The one string that has a claim to being extracted early is the notification
body: it is what someone sees before ever opening the app, and it is the only
text that reaches a player who has stopped playing.
