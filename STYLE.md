## General
- Prefer explicit types over `var`
- Use early returns to reduce nesting
- One class per file

## Naming
- PascalCase for methods and classes
- camelCase for locals
- _camelCase for private fields

## Methods
- Max ~30 lines
- Extract complex logic into private methods

## Collections
- Prefer LINQ for simple queries
- Use loops for complex logic

## Error handling
- No empty catch blocks
- Always log or rethrow

## Comments
- Only explain WHY, not WHAT