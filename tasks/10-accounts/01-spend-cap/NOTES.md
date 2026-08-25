Source: `cat-shelter-tasks.md`, M1 rationale (lines 407-447).

This task replaces a deleted "request-signing" task. Request-signing would
have protected the backend from forged calls, but it does nothing about an
agent inside the trusted boundary running up usage on its own. The spend
cap is the one measure here that cannot be defeated by decompiling the app:
it caps money at the account level, not at the request level. Costs about
five minutes to set.
