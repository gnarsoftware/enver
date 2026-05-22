# Parsing rules and philosophy

Enver treats `.env` files as a **strict, unambiguous configuration format**.
The rules below describe how that strictness manifests in practice. The library's
defaults enforce all of them; the opt-outs at the bottom let you relax specific
restrictions.

The guiding principles:

- **Strict by default.** When two interpretations of a line are possible,
  Enver refuses the file rather than guessing.
- **Fail fast.** Ambiguity surfaces at parse time with a useful error and
  the offending position, not as a silent semantic drift at runtime.
- **Unambiguous patterns over convenience syntax.** Where two forms convey
  the same meaning, prefer the form that leaves the least to interpretation.
- **Stay inside the ecosystem.** A file written to these rules loads the
  same way in other `.env` parsers as much as possible.
  <br/>
  <small>
    \* Some caveats prevent this from applying 100% of time. Each library handles
    interpolation and quote types a little differently in ways that can't be
    expressed as a true subset of all of them. See
    [Ecosystem compatibility](#ecosystem-compatibility) for details.
  </small>

## File encoding

Enver expects UTF-8. A UTF-8 BOM at the start of the file is silently stripped.

## Keys

Keys must match the POSIX-shell identifier shape: **ASCII letters, digits,
and underscore, with no leading digit.**

```
GOOD:         DATABASE_URL   API_KEY_2   DEBUG_MODE
BAD:          2NDARY         MY-VAR      $FOO         MY.VAR
```

These are exactly the names a shell can export, so anything Enver accepts
round-trips cleanly through the system environment. Case-sensitivity
follows the host OS: case-insensitive on Windows, case-sensitive elsewhere.

**Always use uppercase keys.** Because case-sensitivity varies across platforms,
mixed-case keys create files that work on one OS and silently break on another
(`db_host` and `DB_HOST` collide on Windows but stay distinct on Linux).
Uppercase-only or lowercase-only keys are portable, but uppercase is the
established convention for environment variables.

## Value forms

There are four ways to spell a value. **Prefer the simplest form that
doesn't lose meaning**. Don't quote what you don't need to, but reach for
quotes the moment the value contains whitespace, special characters, or
anything you'd want a future reader not to second-guess.

### Bare: `KEY=value`

```
PORT=5432
LOG_LEVEL=info
```

Acceptable when the value is short and contains only `[a-zA-Z0-9_./-]`-ish
characters. Whitespace around `=` and trailing whitespace before a comment
or end of line is ignored.

`KEY=` with no value parses to an empty string. A line with no `=` is an error.

Supports `${VAR}` interpolation.

### Double-quoted: `KEY="value"` (the default for anything non-trivial)

```
GREETING="Hello, ${NAME}!"
PEM_HEADER="-----BEGIN CERTIFICATE-----"
```

- Multi-line capable.
- `${VAR}` interpolation is resolved.
- Backslash escapes: `\"`, `\\`, `\$` (to suppress interpolation).
- **This is the form to reach for when in doubt.**

### Single-quoted: `KEY='value'`

```
PATTERN='^[a-z]+\d+$'
```

- Multi-line capable.
- **No interpolation** (`${VAR}` is literal).
- Backslash escapes: `\'` and `\\`.
- Use when the value contains a literal `${…}`, a regex with `$`, or anything
  else where dollar-sign substitution would be wrong.

### Backtick-quoted: `` KEY=`value` ``

```
PRIVATE_KEY=`-----BEGIN RSA PRIVATE KEY-----
MIIEpAIBAAKCAQEA...
-----END RSA PRIVATE KEY-----`
```

- Multi-line capable.
- Verbatim content: no interpolation and no escape processing. A backslash is
  an ordinary value byte. Newlines are still normalized to LF like all
  multi-line values (see below).
- Because there is no escape mechanism, a backtick value **cannot contain a
  backtick**. Use single or double quotes for values that include one.
- For values that should be taken as-is: PEM-encoded keys, multi-line JSON
  blobs, anything pasted from elsewhere.

### Multi-line values

All three quote forms support newlines inside the value; only bare values do not:

| Form | Multi-line | Interpolation | Best for |
|---|---|---|---|
| `"…"` | yes | yes | text where you might still want `${VAR}` substitution |
| `` `…` `` | yes | no | secrets, PEM keys, JSON blobs - pasted-in verbatim text |
| `'…'` | yes | no | literals containing `${…}` or `$` |
| bare | no | yes | short alphanumeric values |

**Newlines are normalized to LF (`\n`).** Whether the file was authored on
Windows (CRLF), classic Mac (CR), or any modern Unix (LF), a literal line
ending inside a multi-line quoted value parses to a single `\n` byte. This
ensures the same file produces the same value regardless of where the code
runs or how Git's `core.autocrlf` happened to land it on disk. If you need
a specific byte sequence in a value, construct it in code rather than relying
on the file's line endings.

### Suggested precedence

Prefer double-quoted, then bare, then single-quoted, then backtick -
biased toward whichever expresses the value's intent most directly. If
you'd have to think about whether a character means something special,
quote it.

## Whitespace

Whitespace **outside** quotes is ignored:

```
KEY=value
KEY = value
   KEY=value
```

…all produce the same `KEY=value`. The canonical form is `KEY=value` (no
spaces); the others are tolerated but discouraged because the inconsistency
makes diffs noisier.

Whitespace **inside** quotes is preserved verbatim:

```
KEY=" leading and trailing "    # value is exactly " leading and trailing "
```

## Comments

A `#` outside quotes starts a comment **only if preceded by whitespace or a
closing quote.** This preserves URLs and fragments containing `#`:

```
# full-line comment - stripped
KEY=value           # trailing comment - stripped
KEY="value"#tail    # also stripped: the closing " terminates the value
KEY=value#stays     # the # is part of the value (no preceding space)
URL=https://example.com/page#section   # the # is part of the URL
```

This rule is the same for all three quote styles: after a closing `"`,
`'`, or `` ` ``, an immediately-following `#` begins a comment.

## Interpolation

`${VAR}` substitutes the value of `VAR` at parse time:

```
NAME=World
GREETING="Hello, ${NAME}!"        # "Hello, World!"
LITERAL='Hello, ${NAME}!'         # "Hello, ${NAME}!" - single quotes opt out
ESCAPED="Hello, \${NAME}!"        # "Hello, ${NAME}!" - backslash opts out
```

Key properties:

- **Backreading only.** The referenced key must already be defined
  earlier in the same file, in an earlier file in a chain load, or in the
  process environment. Forward references aren't supported.
- **Resolves at parse time.** Once a value is substituted in, later
  changes to the referenced key don't propagate. This keeps semantics
  predictable and avoids retroactive surprises in chain loads.
- **Works in bare and double-quoted values.** Single-quoted and backtick
  values treat `${VAR}` as literal.
- **Missing keys throw by default.** A `${KEY}` that resolves to nothing in any
  source raises `EnvInterpolationException`. This is the typo-catching path.
  A stray `${LOG_DR}` should fail at parse time, not silently become `""`. The
  process-env fallback means legitimate references like
  `CONFIG_PATH=${HOME}/.config/myapp` work as expected when `HOME` is set
  in the environment, even if the file itself doesn't define it.
- **Opt-in silent-empty for ecosystem compat.** Pass
  `EnvParseOptions { AllowMissingInterpolation = true }`
  to match the prevailing convention of substituting empty on miss.
- **Malformed syntax throws.** `${KEY` (unterminated), `${` (incomplete),
  `${BAD-NAME}` (invalid key character), and `${}` (empty key) all raise
  `EnvSyntaxException` at parse time. A typo'd `${KEY}` should surface as an
  error, not produce a config value that mysteriously equals `KEY` or `${`.
  To include a literal `${`, escape with `\$` in a double-quoted value or use
  single quotes.
- **Bare `$IDENTIFIER` is refused by default.** Only `${IDENTIFIER}` is
  recognized as a canonical interpolation. The bare form can easily be
  misinterpreted as an expansion when a literal `$` within a value is expected.
  Rather than assuming one way or another, the lexer raises `EnvSyntaxException`
  at the `$` and asks you to disambiguate by default. `$` followed by anything
  that *isn't* an identifier-start character (digit, punctuation, end-of-line)
  is unambiguous and stays literal in every mode (so `PRICE="$9.99"`, regex
  anchors, and `KEY=value$` all parse cleanly). To handle the ambiguous case, pick one:

  | `EnvParseOptions.OnUnbracedInterpolation` | What `KEY=val$ue` does |
  |---|---|
  | `Error` (default) | Raises `EnvSyntaxException` |
  | `Interpolate` | Resolves `$ue` against the consumer + process env |
  | `Literal` | Parses to the literal `val$ue` |

  In a bare or double-quoted value, `\$IDENTIFIER` always works as an explicit
  escape regardless of mode; inside single quotes and backticks the `$`
  is always literal.

## Duplicate keys

**One definition per key per file.** A second `KEY=` line in the same file
raises `EnvDuplicateKeyException` by default. The intent is that a single file is
self-consistent, so a reader scanning it linearly never has to track which
later line silently overrode something earlier.

Across **multiple** files (`.env` then `.env.local`, or walking up parent
directories), override is intentional. Closer files win; variants override
the bare file. That's a deliberate, declared mechanism rather than the
accident of duplicate lines.

## When you can loosen the rules

The strict defaults can be overridden per call. Every relaxation is a deliberate,
named choice in your code:

| Behavior | How to opt out |
|---|---|
| Duplicate-key throws | `EnvParseOptions { AllowDuplicateKeys = true }` (later definition silently overwrites earlier) |
| Missing interpolation throws | `EnvParseOptions { AllowMissingInterpolation = true }` |
| Bare `$VAR` throws | `EnvParseOptions { OnUnbracedInterpolation = UnbracedInterpolationBehavior.Literal }` (keep `$` literal) or `… = UnbracedInterpolationBehavior.Interpolate` (expand variable) |
| Process env preserves existing values | `LoadDotEnv*(overrideExisting: true)` |
| Missing file throws | `LoadDotEnv*(throwIfMissing: false)` (already the default for directory variants) |
| Missing key throws | `GetOptional*` (returns `null`) or `Get*(key, defaultValue)` |

## Ecosystem compatibility

These rules are a tight subset of what other `.env`-consuming tools accept.
Cross-parser behavior is verified against:

- `dotenv` (Node.js), with the standard `dotenv-expand` companion
- `python-dotenv`
- `godotenv` (Go)
- Docker Compose

Compatibility checking is located in [`compat/`](compat/). Running `run.sh`
produces a per-fixture matrix.

### Known divergences

| Behavior | Example | Enver | dotenv + dotenv-expand (node) | python-dotenv | godotenv (go) | docker compose |
|---|---|---|---|---|---|---|
| Single-quoted literal `${VAR}` references | `KEY='${VALUE}'` | Literal | **Expanded** | **Expanded** | Literal | Literal |
| Comment delimiter without a space | `KEY=#VALUE` | Value | **Comment** | Value | Value | Value |
| Empty value before a spaced comment | `KEY= # note` | Empty | Empty | **Value** | **Value** | **Value** |
| Backtick-quoted values | `` KEY=`VALUE` `` | Supported | Supported | **Unsupported** | **Unsupported** | **Unsupported** |
| `\$` escapes interpolation | `KEY="\${VALUE}"` | Literal | Literal | **Expanded** | Literal | Literal <sup>1</sup> |
| Bare `$IDENTIFIER` interpolation | `KEY=$VALUE` | Error <sup>2</sup> | **Expanded** | **Expanded** | **Expanded** | **Expanded** |

<small>
1. Compose also supports $$ to escape interpolation. This is unsupported by Enver.
</small>

<small>
2. By default Enver throws an error for KEY=$VALUE. This is intentional
  and diverges from every other tested parser. This behavior can be configured
  to match the rest of the ecosystem.
</small>

### Why strictness still wins

Conversely, Enver rejects some files those tools tolerate. That asymmetry
is the point: **strictness in produces portability out.** A file that passes
Enver can be trusted not to be quietly reinterpreted by most other parsers.
