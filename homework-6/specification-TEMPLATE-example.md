# Specification Templates for AI-Assisted Development

## Basic Specification Template

```markdown
# [Feature Name] Specification

> Ingest the information from this file, implement the Low-Level Tasks, and generate the code that will satisfy the High and Mid-Level Objectives.

## High-Level Objective
- [Clear, single sentence describing what you want to build]

## Mid-Level Objectives
- [List of 3-5 concrete, measurable objectives]
- [Each objective should be specific enough to be testable]
- [Focus on what the system should do, not how]

## Implementation Notes
- [Important technical details and constraints]
- [Dependencies and requirements]
- [Coding standards to follow]
- [Performance requirements]
- [Security considerations]

## Context

### Beginning context
- [List of files that exist at start]
- [Current system state]
- [Available resources]

### Ending context
- [List of files that will exist at end]
- [Expected system state]
- [Deliverables]

## Low-Level Tasks

### 1. [First task name]

What prompt would you run to complete this task?
[Specific prompt for AI]

What file do you want to CREATE or UPDATE?
[File path]

What function do you want to CREATE or UPDATE?
[Function/class name]

What are details you want to add to drive the code changes?
[Specific requirements and constraints]

### 2. [Second task name]

What prompt would you run to complete this task?
[Specific prompt for AI]

What file do you want to CREATE or UPDATE?
[File path]

What function do you want to CREATE or UPDATE?
[Function/class name]

What are details you want to add to drive the code changes?
[Specific requirements and constraints]

### 3. [Third task name]

What prompt would you run to complete this task?
[Specific prompt for AI]

What file do you want to CREATE or UPDATE?
[File path]

What function do you want to CREATE or UPDATE?
[Function/class name]

What are details you want to add to drive the code changes?
[Specific requirements and constraints]

## Banking-Specific Specification Template

# [Feature] Specification

> Ingest the information from this file, implement the Low-Level Tasks, and generate the code that will satisfy the High and Mid-Level Objectives.

## High-Level Objective
- [Clear description of the banking feature]

## Mid-Level Objectives
- [Compliance and regulatory requirements]
- [Security and data protection measures]
- [Audit and logging requirements]
- [Performance and scalability needs]
- [Integration requirements]

## Implementation Notes
- [Data privacy requirements (GDPR, CCPA)]
- [Audit trail requirements]
- [Error handling and logging]
- [Input validation and sanitization]
- [Use Decimal for all monetary calculations]
- [Include comprehensive testing]

## Context

### Beginning context
- [Existing banking systems]
- [Current data models]
- [Available APIs and services]

### Ending context
- [New banking components]
- [Updated data models]
- [Integration points]
- [Compliance documentation]

## Low-Level Tasks

### 1. [Compliance task]

What prompt would you run to complete this task?
[Specific compliance requirement]

What file do you want to CREATE or UPDATE?
[Compliance-related file]

What function do you want to CREATE or UPDATE?
[Compliance function]

What are details you want to add to drive the code changes?
[Specific compliance requirements]


### 2. [Security task]

What prompt would you run to complete this task?
[Security implementation]

What file do you want to CREATE or UPDATE?
[Security-related file]

What function do you want to CREATE or UPDATE?
[Security function]

What are details you want to add to drive the code changes?
[Security requirements]

### 3. [Business logic task]

What prompt would you run to complete this task?
[Business logic implementation]

What file do you want to CREATE or UPDATE?
[Business logic file]

What function do you want to CREATE or UPDATE?
[Business function]

What are details you want to add to drive the code changes?
[Business requirements]
