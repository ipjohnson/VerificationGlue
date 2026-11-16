# Writing style

These rules apply to every markdown file in the repository. CI checks the ones marked "Vale" with the rules in `eng/vale/VerificationGlue`.

## Rules

<!-- vale off -->

- Lead with the result. Put detail after it.
- One idea per sentence.
- Use plain words. Write "the test fails", not "the test explodes". (Vale: `PlainWords`)
- Do not add words for emphasis, such as "simply" or "seamlessly". (Vale: `Filler`)
- Do not use em dash asides. Split the sentence instead. (Vale: `NoEmDash`)
- No emoji. (Vale: `NoEmoji`)
- Use the identifier from the code. Do not invent a name for something that has one.
- Every guide starts with what the reader will be able to do at the end.
- Every claim about behaviour has an example that CI runs.

<!-- vale on -->

A page can turn Vale off around text that quotes a banned word, as this list does.

## Running the check

Install [Vale](https://docs.vale.sh/topics/installation) and run it from the repository root:

```sh
eng/check-docs.sh
```

The script also expands snippets and checks links. `docs/writing-docs.md` describes those checks.

To add a rule, add a YAML file to `eng/vale/VerificationGlue` and a line to the list above.
