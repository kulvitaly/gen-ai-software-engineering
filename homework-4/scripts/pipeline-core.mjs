// Pure, side-effect-free pipeline logic. Imported by run-pipeline.mjs (which
// adds the claude invocation + filesystem) and by the unit tests.

/**
 * Ordered pipeline stages. Each stage names its agent definition, the skill it
 * must auto-load (if any), the input artifacts it requires, the model to run,
 * and the artifact it produces. Paths are relative to a bug context directory.
 */
export const STAGES = [
  {
    name: 'bug-researcher',
    agent: 'agents/bug-researcher.agent.md',
    skill: null,
    model: 'claude-opus-4-8',
    requires: ['bug-context.md'],
    output: 'research/codebase-research.md',
  },
  {
    name: 'research-verifier',
    agent: 'agents/research-verifier.agent.md',
    skill: 'skills/research-quality-measurement.md',
    model: 'claude-opus-4-8',
    requires: ['research/codebase-research.md'],
    output: 'research/verified-research.md',
  },
  {
    name: 'bugfix-planner',
    agent: 'agents/bugfix-planner.agent.md',
    skill: null,
    model: 'claude-opus-4-8',
    requires: ['research/verified-research.md'],
    output: 'implementation-plan.md',
  },
  {
    name: 'bug-fixer',
    agent: 'agents/bug-fixer.agent.md',
    skill: null,
    model: 'claude-sonnet-4-6',
    requires: ['implementation-plan.md'],
    output: 'fix-summary.md',
  },
  {
    name: 'security-verifier',
    agent: 'agents/security-verifier.agent.md',
    skill: null,
    model: 'claude-opus-4-8',
    requires: ['fix-summary.md'],
    output: 'security-report.md',
  },
  {
    name: 'unit-test-generator',
    agent: 'agents/unit-test-generator.agent.md',
    skill: 'skills/unit-tests-FIRST.md',
    model: 'claude-haiku-4-5',
    requires: ['fix-summary.md'],
    output: 'test-report.md',
    // Independent of security-verifier (both consume only fix-summary.md and
    // write distinct outputs), so it runs alongside the preceding stage.
    parallel: true,
  },
];

/**
 * Group the ordered stages into execution groups. A stage marked `parallel`
 * joins the current group (runs concurrently with the preceding stage); any
 * other stage starts a new group. For the current STAGES the last group is
 * [security-verifier, unit-test-generator].
 */
export function executionGroups(stages) {
  const groups = [];
  for (const stage of stages) {
    if (stage.parallel && groups.length > 0) {
      groups[groups.length - 1].push(stage);
    } else {
      groups.push([stage]);
    }
  }
  return groups;
}

/**
 * Return the list of required input artifacts that are missing for a stage.
 * `exists` is an injectable predicate (relPath -> boolean) so this stays pure
 * and testable without touching the filesystem.
 */
export function missingInputs(stage, exists) {
  return stage.requires.filter((rel) => !exists(rel));
}

/**
 * Build the headless prompt for a stage. The prompt instructs the model to act
 * as the agent defined in its file, load its skill, consume its inputs, and
 * write its output to the bug context folder.
 */
export function buildPrompt(stage, bugDir) {
  const skillLine = stage.skill
    ? `Load and follow the skill at "${stage.skill}".`
    : 'No skill is required for this stage.';
  return [
    `You are the "${stage.name}" agent. Read your full role definition at "${stage.agent}" and act strictly as specified there.`,
    skillLine,
    `The active bug context directory is "${bugDir}".`,
    `Required input artifact(s): ${stage.requires
      .map((r) => `"${bugDir}/${r}"`)
      .join(', ')}.`,
    `Write your result to "${bugDir}/${stage.output}" following the contract for that artifact.`,
    `Do only what your role permits.`,
  ].join('\n');
}
