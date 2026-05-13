# 🏦 Homework 1: Banking Transactions API

> **Student Name**: Vitalii Kulykivskyi
> **Date Submitted**: 2026-05-10
> **AI Tools Used**: 
  - VS Code + GitHub Copilot (model: Claude Haiku 4.5)
  - Cursor IDE (model: Composer 2)

---

## 📋 Project Overview

This is an **ASP.NET Core (.NET 10)** minimal REST API for creating and querying banking transactions. Data lives in an **in-memory SQLite** database (EF Core migrations), so nothing persists across process restarts. The design uses **MediatR** for request handlers and **FluentValidation** for input rules (positive amounts with at most two decimal places, `ACC-XXXXX` account format, supported ISO 4217 currencies).

**Core behavior:** create transactions (deposit, withdrawal, transfer), list them with optional filters (`accountId`, `type`, `from` / `to` date range), fetch by id, and get per-account **balance**. **Extras** include account **summary**, **CSV export** of transactions, **simple-interest** on the current balance, **per-IP rate limiting**, and **OpenAPI** (Scalar UI in development).

**Interest:** `GET /accounts/{accountId}/interest?rate=&days=` uses simple interest `principal × rate × days ÷ 365`, where `rate` is an annual decimal (e.g. `0.05` = 5% per year) and `principal` is the current completed-transaction balance.

---
<div align="center">

*This project was completed as part of the AI-Assisted Development course.*

</div>
