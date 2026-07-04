"use strict";

/**
 * Vanilla-JS dashboard controller.
 *
 * The API key is read from the password input at click time and held only
 * in the `currentApiKey` module-level variable (never persisted to
 * localStorage/sessionStorage, never hardcoded here). All dynamic content
 * is inserted via `textContent`, never `innerHTML`, to prevent XSS.
 */

const STATUSES = ["approved", "flagged", "blocked", "rejected"];

let currentApiKey = "";

const runButton = document.getElementById("run-button");
const apiKeyInput = document.getElementById("api-key");
const statusMessage = document.getElementById("status-message");
const resultsBody = document.getElementById("results-body");
const emptyMessage = document.getElementById("empty-message");
const countElements = Object.fromEntries(
  STATUSES.map((status) => [status, document.getElementById(`count-${status}`)])
);

function setStatusMessage(text, tone) {
  statusMessage.textContent = text;
  if (tone) {
    statusMessage.setAttribute("data-tone", tone);
  } else {
    statusMessage.removeAttribute("data-tone");
  }
}

function updateCountTiles(countsByStatus) {
  const counts = countsByStatus || {};
  for (const status of STATUSES) {
    countElements[status].textContent = String(counts[status] || 0);
  }
}

function renderResults(data) {
  updateCountTiles(data.counts_by_status);

  resultsBody.textContent = "";
  const transactions = Array.isArray(data.transactions) ? data.transactions : [];

  emptyMessage.style.display = transactions.length === 0 ? "block" : "none";

  for (const txn of transactions) {
    const row = document.createElement("tr");

    const idCell = document.createElement("td");
    idCell.textContent = txn.transaction_id != null ? String(txn.transaction_id) : "";
    row.appendChild(idCell);

    const statusCell = document.createElement("td");
    const badge = document.createElement("span");
    const status = txn.status != null ? String(txn.status) : "unknown";
    badge.className = `status-badge ${status}`;
    badge.textContent = status;
    statusCell.appendChild(badge);
    row.appendChild(statusCell);

    const scoreCell = document.createElement("td");
    scoreCell.textContent = txn.score != null ? String(txn.score) : "-";
    row.appendChild(scoreCell);

    const reasonsCell = document.createElement("td");
    const reasons = Array.isArray(txn.reasons) ? txn.reasons : [];
    reasonsCell.textContent = reasons.length > 0 ? reasons.join("; ") : "-";
    row.appendChild(reasonsCell);

    resultsBody.appendChild(row);
  }
}

async function fetchResults() {
  try {
    const response = await fetch("/results", {
      method: "GET",
      headers: { "X-API-Key": currentApiKey },
    });
    if (!response.ok) {
      setStatusMessage("Unable to load results.", "error");
      return;
    }
    const data = await response.json();
    renderResults(data);
  } catch {
    setStatusMessage("Unable to load results.", "error");
  }
}

async function runPipeline() {
  currentApiKey = apiKeyInput.value;
  if (!currentApiKey) {
    setStatusMessage("Enter an API key first.", "error");
    return;
  }

  runButton.disabled = true;
  setStatusMessage("Running pipeline...", null);

  try {
    const response = await fetch("/run", {
      method: "POST",
      headers: { "X-API-Key": currentApiKey },
    });
    if (!response.ok) {
      setStatusMessage("Unable to run the pipeline.", "error");
      return;
    }
    setStatusMessage("Pipeline run complete.", "success");
    await fetchResults();
  } catch {
    setStatusMessage("Unable to run the pipeline.", "error");
  } finally {
    runButton.disabled = false;
  }
}

runButton.addEventListener("click", runPipeline);
