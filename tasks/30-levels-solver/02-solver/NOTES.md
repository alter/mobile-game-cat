
## verify:passed → verify:failed — 2026-08-28

The OUTCOME names a test, `five_known_dead_ends`. `grep -rn "five_known_dead_ends" .`
returns exactly one line: the one in `task.txt` that asks for it. **The test was
never written**, and the label said the task was verified.

Two smaller findings from the same pass: item 3 asks the solver to handle a
60-item pile and `tools/tests/test_solver.py:86` exercises 45 — the size the
shipped levels actually use was never covered. And of the three dead-end
fixtures in the tree, only two reach `solve()`.

Speed is not the problem: the verifier timed a 60-item pile at 0.0006 s worst
over ten seeds. It is coverage.

Being written now. The label goes back to `passed` when the test exists and runs,
and not before.
