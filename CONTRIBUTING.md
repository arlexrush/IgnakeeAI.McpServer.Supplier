# Contributing to IgnakeeAI MCP Server Supplier

Thank you for your interest in contributing! This project follows a **controlled contribution model**: 
all changes must be reviewed and explicitly approved by the project maintainer before being merged.
The maintainer's decision is final and does not require justification.

---

## Contributor License Agreement (CLA)

**Before your Pull Request can be accepted, you must sign the CLA.**

By submitting a contribution, you agree that:

1. You grant **IgnakeeAI** a perpetual, worldwide, non-exclusive, royalty-free license to use, reproduce, 
   modify, distribute, and sublicense your contributionas part of this project.
2. You confirm the contribution is your original work and you have the right to submit it.
3. You understand that **the maintainer decides** whether a contribution is accepted, rejected, or deferred 
   — without obligation to justify the decision.

>  Sign the CLA by including this exact line in your Pull Request description:
> `I have read and agree to the IgnakeeAI CLA.`

>  PRs without this statement will be closed automatically.

---

## How to Contribute

### Reporting Issues
- Open a GitHub Issue describing the problem, steps to reproduce, and expected behavior.
- Use the provided issue templates when available.
- Tag the issue appropriately: `bug`, `enhancement`, `question`, `documentation`.

### Proposing a Feature
- Open a GitHub Issue with the label `enhancement` **before** writing any code.
- Wait for maintainer feedback and explicit approval before investing implementation time.
- Unsolicited large PRs without prior discussion may be closed without review.

### Submitting a Pull Request

1. Fork the repository.
2. Create a branch from `develop`:
   ```
   git checkout -b feature/your-feature-name
   ```
3. Follow the coding standards defined in `.editorconfig`.
4. Ensure all existing tests pass: `dotnet test`.
5. Add or update tests covering your changes.
6. Sign the CLA in your PR description (see above).
7. Open the Pull Request targeting the `develop` branch.
8. **Do not open PRs directly against `main`** — they will be closed without review.

---
## Branch Strategy

| Branch       | Purpose                                               |
|--------------|-------------------------------------------------------|
| `main`       | Stable releases only. Merge restricted to maintainer. |
| `develop`    | Integration branch. All PRs target this branch.       |
| `feature/*`  | Feature development. Created from `develop`.          |
| `fix/*`      | Bug fixes. Created from `develop`.                    |

---

## Review Process

- The maintainer reviews all PRs. There is **no guaranteed response time**.
- A PR may be closed if it does not align with the project roadmap, even if technically correct.
- The maintainer may request changes, split a PR, or incorporate the idea independently.
- **Only the maintainer merges to `main`** after final validation.

---

## Coding Standards

| Topic             | Standard                                                                    |
|-------------------|-----------------------------------------------------------------------------|
| Runtime           | **.NET 8**                                                                  |
| Architecture      | Clean Architecture: `Domain → Application → Infrastructure → Api`           |
| Tools layer       | `McpTools` must delegate all logic to `Application` services                |
| New dependencies  | Open a GitHub Issue before adding any NuGet package                         |
| XML docs          | All `public` members must have XML documentation comments                   |
| Tests             | xUnit, placed under `tests/` folder                                         |
| ERP connectors    | Implement `IErpConnector`, register in `DependencyInjection.cs`             |
| Database          | Provider-agnostic via `ICatalogRepository`; no raw SQL in Application layer |

---

## Code of Conduct

Be respectful and constructive.  
Contributions that include offensive language, harassment, or bad-faith behavior
will be rejected and the contributor blocked permanently.