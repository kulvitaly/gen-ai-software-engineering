import { test } from 'node:test';
import assert from 'node:assert/strict';
import { STAGES, executionGroups, missingInputs, buildPrompt } from './pipeline-core.mjs';

test('defines the six stages in the required order', () => {
  assert.deepEqual(
    STAGES.map((s) => s.name),
    [
      'bug-researcher',
      'research-verifier',
      'bugfix-planner',
      'bug-fixer',
      'security-verifier',
      'unit-test-generator',
    ],
  );
});

test('assigns the documented per-agent models', () => {
  const byName = Object.fromEntries(STAGES.map((s) => [s.name, s.model]));
  assert.equal(byName['bug-researcher'], 'claude-opus-4-8');
  assert.equal(byName['research-verifier'], 'claude-opus-4-8');
  assert.equal(byName['bugfix-planner'], 'claude-opus-4-8');
  assert.equal(byName['bug-fixer'], 'claude-sonnet-4-6');
  assert.equal(byName['security-verifier'], 'claude-opus-4-8');
  assert.equal(byName['unit-test-generator'], 'claude-haiku-4-5');
});

test('every stage input is produced by an upstream stage (or is the seed)', () => {
  const produced = new Set(['bug-context.md']); // the seed input
  for (const stage of STAGES) {
    for (const req of stage.requires) {
      assert.ok(
        produced.has(req),
        `stage "${stage.name}" requires "${req}" which no upstream stage produces`,
      );
    }
    produced.add(stage.output);
  }
});

test('groups stages 5 & 6 into one parallel execution group', () => {
  const groups = executionGroups(STAGES);
  assert.equal(groups.length, 5);
  for (const g of groups.slice(0, 4)) {
    assert.equal(g.length, 1);
  }
  assert.deepEqual(
    groups[4].map((s) => s.name),
    ['security-verifier', 'unit-test-generator'],
  );
});

test('detects missing required inputs', () => {
  const stage = STAGES[0];
  assert.deepEqual(missingInputs(stage, () => false), stage.requires);
  assert.deepEqual(missingInputs(stage, () => true), []);
});

test('builds a prompt that references the agent, skill, inputs, and output', () => {
  const stage = STAGES.find((s) => s.name === 'research-verifier');
  const prompt = buildPrompt(stage, 'context/bugs/001');
  assert.ok(prompt.includes(stage.agent));
  assert.ok(prompt.includes(stage.skill));
  assert.ok(prompt.includes('context/bugs/001/research/codebase-research.md'));
  assert.ok(prompt.includes('context/bugs/001/research/verified-research.md'));
});
