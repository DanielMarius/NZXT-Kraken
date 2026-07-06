---
name: Feature request
about: Propose a new capability or design improvement
title: "[Feature] "
labels: enhancement
assignees: ""
---

## Summary

Describe the feature in one or two lines.

## Problem Statement

What limitation or pain point does this solve?

## Proposed Change

Describe the expected behavior.

## Hardware / Safety Considerations

Mention any risk to:

- single-process controller ownership
- LCD update stability
- fan/pump safety behavior
- supervisor design

## Rules Check

- [ ] This proposal does not require Python runtime dependencies.
- [ ] This proposal keeps single-process hardware ownership intact.
- [ ] This proposal does not introduce a second hardware controller/watchdog.
