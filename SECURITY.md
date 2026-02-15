# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 2.x     | :white_check_mark: |
| 1.x     | :x:                |

## Reporting a Vulnerability

We take the security of this project seriously. If you discover a security vulnerability, please report it responsibly using **GitHub's Private Vulnerability Reporting**.

### How to Report

1. Navigate to the [Security Advisories](https://github.com/csharp-compound-engineering/csharp-compound-engineering/security/advisories/new) page
2. Click **"Report a vulnerability"**
3. Fill in the details of the vulnerability

This creates a private advisory visible only to the maintainers. Do **not** open a public issue for security vulnerabilities.

### What to Include

- A description of the vulnerability
- Steps to reproduce the issue
- The potential impact
- Any suggested fixes (if applicable)

### Response Timeline

- **Acknowledgment**: Within 48 hours of report submission
- **Assessment**: Within 5 business days
- **Resolution target**: Within 30 days for confirmed vulnerabilities

### What to Expect

1. You will receive an acknowledgment that your report has been received
2. We will investigate and determine the impact and severity
3. We will develop and test a fix
4. We will release a patch and publish a security advisory
5. You will be credited in the advisory (unless you prefer to remain anonymous)

### Scope

The following are in scope for security reports:

- The MCP server application (`CompoundDocs.McpServer`)
- Authentication and authorization mechanisms
- Data handling and storage (Neptune, OpenSearch)
- Dependencies with known vulnerabilities
- Infrastructure as Code misconfigurations (`opentofu/`, `charts/`)

### Out of Scope

- Issues in upstream dependencies that are already publicly disclosed
- Denial of service attacks
- Social engineering
- Issues requiring physical access to a server

## Security Best Practices for Contributors

- Never commit secrets, API keys, or credentials
- Use environment variables or secret managers for sensitive configuration
- Follow the principle of least privilege in IAM policies
- Keep dependencies up to date via Dependabot
