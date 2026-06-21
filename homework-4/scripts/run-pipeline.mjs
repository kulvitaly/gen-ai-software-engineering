#!/usr/bin/env node
// Single-command orchestrator for the 4-agent bug-fixing pipeline.
//
//   npm run pipeline            # runs bug 001
//   npm run pipeline -- 002     # runs another bug id
//   npm run pipeline -- 001 --dry-run   # print the plan without invoking claude
//
// Runs the stages in fixed order, auto-loads each stage's skill, hands off via
// artifacts in the bug context folder, and halts if a required input is missing.

import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { STAGES, missingInputs, buildPrompt } from './pipeline-core.mjs';

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const bugId = args.find((a) => !a.startsWith('--')) ?? '001';
const bugDir = join('context', 'bugs', bugId);

function log(msg) {
  console.log(`[pipeline] ${msg}`);
}

function runStage(stage, index) {
  log(`Stage ${index + 1}/${STAGES.length}: ${stage.name} (model: ${stage.model})`);

  const missing = missingInputs(stage, (rel) => existsSync(join(bugDir, rel)));
  if (missing.length > 0) {
    if (dryRun) {
      log(`  [dry-run] note — input(s) not yet present (a prior stage produces them): ${missing.join(', ')}`);
    } else {
      log(`  HALT — missing required input(s): ${missing.join(', ')}`);
      return false;
    }
  }

  if (stage.skill) log(`  loading skill: ${stage.skill}`);
  const prompt = buildPrompt(stage, bugDir);

  if (dryRun) {
    log('  [dry-run] would invoke:');
    log(`    claude -p --model ${stage.model} --permission-mode bypassPermissions  (prompt via stdin)`);
    return true;
  }

  // The prompt is fed via stdin (not argv) so its newlines/quotes are never
  // mangled by the shell. bypassPermissions lets fix/test stages run the shell
  // (dotnet test) and write files without interactive approval. shell:true keeps
  // the `claude` launcher resolvable on Windows (it is a .cmd shim).
  const result = spawnSync(
    'claude',
    ['-p', '--model', stage.model, '--permission-mode', 'bypassPermissions'],
    { input: prompt, stdio: ['pipe', 'inherit', 'inherit'], shell: true },
  );

  if (result.status !== 0) {
    log(`  HALT — stage "${stage.name}" exited with code ${result.status}`);
    return false;
  }

  if (!existsSync(join(bugDir, stage.output))) {
    log(`  WARNING — expected output not found: ${stage.output}`);
  } else {
    log(`  produced: ${stage.output}`);
  }
  return true;
}

function main() {
  if (!existsSync(bugDir)) {
    log(`ERROR — bug context directory not found: ${bugDir}`);
    process.exit(1);
  }

  log(`Running pipeline for bug ${bugId}${dryRun ? ' (dry-run)' : ''}`);
  log(`Order: ${STAGES.map((s) => s.name).join(' -> ')}`);

  for (let i = 0; i < STAGES.length; i++) {
    const ok = runStage(STAGES[i], i);
    if (!ok) {
      log('Pipeline stopped.');
      process.exit(1);
    }
  }

  log('Pipeline complete. All artifacts produced.');
}

main();
