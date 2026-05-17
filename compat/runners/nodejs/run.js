#!/usr/bin/env node
// Compat runner: parse argv[2] via Node's `dotenv` + `dotenv-expand`. The
// base `dotenv` package doesn't expand `${VAR}` references; `dotenv-expand`
// is the standard companion that does, and is what most real-world Node
// codebases install alongside dotenv. Running them together reflects the
// typical ecosystem usage rather than the quirk of the base package alone.
import { readFileSync } from 'node:fs';
import { parse } from 'dotenv';
import { expand } from 'dotenv-expand';

const path = process.argv[2];
if (!path) {
  console.error('usage: run.js <fixture.env>');
  process.exit(2);
}

// dotenvExpand.expand mutates the `parsed` field in place to resolve any
// ${VAR} references against entries earlier in the file and the process env.
const result = { parsed: parse(readFileSync(path)) };
expand(result);
process.stdout.write(JSON.stringify(result.parsed));
