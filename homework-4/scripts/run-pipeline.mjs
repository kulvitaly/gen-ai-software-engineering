#!/usr/bin/env node
// Single-command orchestrator for the 6-agent bug-fixing pipeline.
//
//   npm run pipeline            # runs bug 001
//   npm run pipeline -- 002     # runs another bug id
//   npm run pipeline -- 001 --dry-run   # print the plan without invoking claude
//
// Runs the stages in fixed order, auto-loads each stage's skill, hands off via
// artifacts in the bug context folder, and halts if a required input is missing.
// Independent stages (security-verifier + unit-test-generator) run concurrently
// as a single execution group.

import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import {
  STAGES,
  executionGroups,
  missingInputs,
  buildPrompt,
} from './pipeline-core.mjs';

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const bugId = args.find((a) => !a.startsWith('--')) ?? '001';
const bugDir = join('context', 'bugs', bugId);

function log(msg) {
  console.log(`[pipeline] ${msg}`);
}

// Forward a child stream to `out`, prefixing each complete line with the stage
// name so concurrently-running stages stay readable. Returns flush() for the
// trailing partial line emitted at close.
function lineForwarder(stageName, out) {
  let buffer = '';
  const onData = (chunk) => {
    buffer += chunk;
    let nl;
    while ((nl = buffer.indexOf('\n')) !== -1) {
      out.write(`[${stageName}] ${buffer.slice(0, nl)}\n`);
      buffer = buffer.slice(nl + 1);
    }
  };
  const flush = () => {
    if (buffer.length > 0) {
      out.write(`[${stageName}] ${buffer}\n`);
      buffer = '';
    }
  };
  return { onData, flush };
}

// Run a single stage to completion. Resolves true on success, false on a HALT
// condition. Output is streamed live with a per-stage line prefix so parallel
// stages do not interleave unlabeled.
function runStage(stage, index) {
  return new Promise((resolve) => {
    log(`Stage ${index + 1}/${STAGES.length}: ${stage.name} (model: ${stage.model})`);

    const missing = missingInputs(stage, (rel) => existsSync(join(bugDir, rel)));
    if (missing.length > 0) {
      log(`  HALT — missing required input(s): ${missing.join(', ')}`);
      resolve(false);
      return;
    }

    if (stage.skill) log(`  loading skill: ${stage.skill}`);
    const prompt = buildPrompt(stage, bugDir);

    // The prompt is fed via stdin (not argv) so its newlines/quotes are never
    // mangled by the shell. bypassPermissions lets fix/test stages run the shell
    // (dotnet test) and write files without interactive approval. shell:true keeps
    // the `claude` launcher resolvable on Windows (it is a .cmd shim). stdout/err
    // are piped (not inherited) so concurrent stages can be line-prefixed.
    const child = spawn(
      'claude',
      ['-p', '--model', stage.model, '--permission-mode', 'bypassPermissions'],
      { stdio: ['pipe', 'pipe', 'pipe'], shell: true },
    );

    const outFwd = lineForwarder(stage.name, process.stdout);
    const errFwd = lineForwarder(stage.name, process.stderr);
    child.stdout.on('data', outFwd.onData);
    child.stderr.on('data', errFwd.onData);

    child.stdin.write(prompt);
    child.stdin.end();

    child.on('error', (err) => {
      log(`  HALT — stage "${stage.name}" failed to start: ${err.message}`);
      resolve(false);
    });

    child.on('close', (code) => {
      outFwd.flush();
      errFwd.flush();
      if (code !== 0) {
        log(`  HALT — stage "${stage.name}" exited with code ${code}`);
        resolve(false);
        return;
      }
      if (!existsSync(join(bugDir, stage.output))) {
        log(`  WARNING — expected output not found: ${stage.output}`);
      } else {
        log(`  produced: ${stage.output}`);
      }
      resolve(true);
    });
  });
}

// Print what a stage would do without invoking claude.
function previewStage(stage, index) {
  log(`Stage ${index + 1}/${STAGES.length}: ${stage.name} (model: ${stage.model})`);
  const missing = missingInputs(stage, (rel) => existsSync(join(bugDir, rel)));
  if (missing.length > 0) {
    log(`  [dry-run] note — input(s) not yet present (a prior stage produces them): ${missing.join(', ')}`);
  }
  if (stage.skill) log(`  loading skill: ${stage.skill}`);
  log('  [dry-run] would invoke:');
  log(`    claude -p --model ${stage.model} --permission-mode bypassPermissions  (prompt via stdin)`);
}

async function main() {
  if (!existsSync(bugDir)) {
    log(`ERROR — bug context directory not found: ${bugDir}`);
    process.exit(1);
  }

  const groups = executionGroups(STAGES);
  log(`Running pipeline for bug ${bugId}${dryRun ? ' (dry-run)' : ''}`);
  log(`Order: ${STAGES.map((s) => s.name).join(' -> ')}`);

  let stageNo = 0; // 0-based index into STAGES, advanced across groups
  for (const group of groups) {
    const start = stageNo;
    if (group.length > 1) {
      log(`Stages ${start + 1}-${start + group.length} run IN PARALLEL: ${group.map((s) => s.name).join(', ')}`);
    }

    if (dryRun) {
      group.forEach((stage, i) => previewStage(stage, start + i));
      stageNo += group.length;
      continue;
    }

    // Launch every stage in the group at once, then await them together.
    const results = await Promise.all(
      group.map((stage, i) => runStage(stage, start + i)),
    );
    stageNo += group.length;

    if (results.some((ok) => !ok)) {
      log('Pipeline stopped.');
      process.exit(1);
    }
  }

  if (dryRun) {
    log('Pipeline dry-run complete. No models were invoked.');
  } else {
    log('Pipeline complete. All artifacts produced.');
  }
}

main();
