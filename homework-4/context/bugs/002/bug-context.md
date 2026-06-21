# Bug Context: 002

## Title
Classification of severity boundaries is incorrect is case-insensitive.

## Reported behavior
1. **Functional**: Create a new ticket via UI. Check the database. The ticket is stored twice with autoclassify flag.
Provide a ticket text all words uppercase. The ticket is classified with wrong category and priority.
Create another ticket with the same text but all words lowercase. The ticket is classified with correct priority and category.

## Expected behavior
1. **Functional**: Classification result should be identical.